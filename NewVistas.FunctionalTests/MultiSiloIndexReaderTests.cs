// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Multi-silo regression tests for the pull-through StatelessWorker readers.
///
/// THE bug these guard against (Performance.md §2.4): the old push-based
/// drug-interaction cache was only populated on the silo hosting the dataset
/// grain, and an empty cache silently reported "no interactions" — on any
/// 2+ silo cluster, checkers on the other silos passed every medication
/// order unchecked (fail-open). The pull-through design populates each silo
/// on demand and fails CLOSED when the dataset is not loaded.
///
/// Runs on its own 2-silo cluster (NOT SharedCluster): multi-silo topology is
/// the point, and the "dataset not loaded" test needs deterministic control
/// of the DI-DATASET singleton.
/// </summary>
[TestFixture, NonParallelizable]
public class MultiSiloIndexReaderTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var builder = new TestClusterBuilder(2);
        builder.AddSiloBuilderConfigurator<SharedCluster.AllStoresConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    private IDrugInteractionDatasetGrain Dataset()
        => _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");

    private IDrugInteractionCheckerGrain Checker()
        => _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>("CHECKER");

    private static DrugIngredient Ingredient(string ien, string name)
        => new() { IngredientIen = ien, Name = name };

    private static DrugInteractionPair Pair(string ien1, string name1, string ien2, string name2)
    {
        bool ordered = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0;
        return new DrugInteractionPair
        {
            IngredientIen1 = ordered ? ien1 : ien2,
            IngredientName1 = ordered ? name1 : name2,
            IngredientIen2 = ordered ? ien2 : ien1,
            IngredientName2 = ordered ? name2 : name1,
            Severity = InteractionSeverity.Significant,
            Description = "Multi-silo regression pair"
        };
    }

    // ─── Fail-closed: dataset not loaded blocks, never passes ────────────────

    [Test]
    public async Task DrugInteractionChecker_DatasetNotLoaded_BlocksWithExplicitOutcome()
    {
        // Deterministic unloaded state on this dedicated cluster.
        await Dataset().ClearAsync();

        // 1. Checker reports DataUnavailable — not an empty "no interactions".
        DrugInteractionCheckResponse response = await Checker().CheckInteractionsAsync(
        [
            Ingredient("IEN-NL-1", "WARFARIN"),
            Ingredient("IEN-NL-2", "ASPIRIN")
        ]);
        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.DataUnavailable));

        // 2. Prescription screening blocks the fill.
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string rxId = $"RX-NL-{Guid.NewGuid()}";
        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "WARFARIN 5MG",
            [Ingredient("IEN-NL-1", "WARFARIN")],
            [Ingredient("IEN-NL-2", "ASPIRIN")],
            "PHARM-001");

        InteractionScreeningState screening = await wf.GetInteractionScreeningAsync(screeningId);
        Assert.That(screening.Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));
        Assert.That(await wf.IsPrescriptionClearedForFillAsync(rxId), Is.False);

        // 3. DUR records an Unavailable (blocking, non-overridable) outcome.
        string durId = await wf.PerformDurAsync(
            $"RX-DUR-{Guid.NewGuid()}", "WARFARIN 5MG", null, null,
            null, null, null, null, null, null, null,
            false, null, "PHARM-001",
            ingredientIens: ["IEN-NL-1", "IEN-NL-2"]);

        DurAssessmentState dur = await wf.GetDurAssessmentAsync(durId);
        DurCheckResult? interactionCheck = dur.Checks
            .FirstOrDefault(c => c.CheckType == DurCheckType.DrugInteraction);

        Assert.That(interactionCheck, Is.Not.Null);
        Assert.That(interactionCheck!.Outcome, Is.EqualTo(DurOutcome.Unavailable));
        Assert.That(dur.Status, Is.EqualTo(DurAssessmentStatus.Failed));
    }

    // ─── Multi-silo correctness: every silo detects after one load ───────────

    [Test]
    public async Task DrugInteractionChecker_DetectsInteractionsOnAllSilos_AfterLoadViaOneSilo()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienW = $"IEN-{run}-MS-W";
        string ienA = $"IEN-{run}-MS-A";

        await Dataset().AddInteractionsAsync([Pair(ienW, "WARFARIN", ienA, "ASPIRIN")]);

        // StatelessWorker checkers activate on whichever silo receives each
        // call; repeated calls exercise activations on both silos. EVERY call
        // must find the pair — the old push-based cache returned empty on the
        // silo that didn't host the dataset grain.
        for (int i = 0; i < 20; i++)
        {
            DrugInteractionCheckResponse response = await Checker().CheckInteractionsAsync(
            [
                Ingredient(ienW, "WARFARIN"),
                Ingredient(ienA, "ASPIRIN")
            ]);

            Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok),
                $"Check #{i} reported the dataset unavailable.");
            Assert.That(response.Results, Has.Count.EqualTo(1),
                $"Check #{i} missed the seeded interaction — silo-local cache not populated.");
        }

        // Clearing must be detected by version check on every silo — checks
        // fail closed instead of using the stale snapshot.
        await Dataset().ClearAsync();

        for (int i = 0; i < 10; i++)
        {
            DrugInteractionCheckResponse cleared = await Checker().CheckInteractionsAsync(
            [
                Ingredient(ienW, "WARFARIN"),
                Ingredient(ienA, "ASPIRIN")
            ]);
            Assert.That(cleared.Status, Is.EqualTo(DrugInteractionCheckStatus.DataUnavailable),
                $"Check #{i} after Clear did not fail closed.");
        }
    }

    // ─── Patient search reader: works and stays fresh on every silo ──────────

    [Test]
    public async Task PatientSearchGrain_FindsPatientOnAllSilos_AfterRegistration()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string patientId = $"PATIENT-{run}";

        IPatientIndexGrain index = _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId,
            Name = $"MULTISILO,FIND{run}",
            Sex = "M",
            IsActive = true
        });

        IPatientSearchGrain search = _cluster.GrainFactory.GetGrain<IPatientSearchGrain>("PATIENT-SEARCH");

        for (int i = 0; i < 20; i++)
        {
            List<PatientIndexEntry> results = await search.SearchAsync($"MULTISILO,FIND{run}");
            Assert.That(results, Has.Count.EqualTo(1),
                $"Search #{i} missed the registered patient — silo-local snapshot not populated/refreshed.");
            Assert.That(results[0].PatientId, Is.EqualTo(patientId));
        }
    }

    [Test]
    public async Task PatientSearchGrain_SeesNewPatientAfterIndexMutation()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        IPatientIndexGrain index = _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");
        IPatientSearchGrain search = _cluster.GrainFactory.GetGrain<IPatientSearchGrain>("PATIENT-SEARCH");

        // Warm the silo-local snapshots with a first patient.
        string firstId = $"PATIENT-{run}-1";
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = firstId, Name = $"DELTA,WARM{run}", Sex = "F", IsActive = true
        });
        Assert.That(await search.SearchAsync($"DELTA,WARM{run}"), Has.Count.EqualTo(1));

        // Mutate: add a second patient and rename the first. Freshness is
        // version-exact — no sleeps, the very next search must see both.
        string secondId = $"PATIENT-{run}-2";
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = secondId, Name = $"DELTA,NEW{run}", Sex = "M", IsActive = true
        });
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = firstId, Name = $"DELTA,RENAMED{run}", Sex = "F", IsActive = true
        });

        for (int i = 0; i < 10; i++)
        {
            Assert.That(await search.SearchAsync($"DELTA,NEW{run}"), Has.Count.EqualTo(1),
                $"Search #{i} did not see the newly added patient.");
            Assert.That(await search.SearchAsync($"DELTA,RENAMED{run}"), Has.Count.EqualTo(1),
                $"Search #{i} did not see the renamed patient (delta not applied).");
            Assert.That(await search.SearchAsync($"DELTA,WARM{run}"), Is.Empty,
                $"Search #{i} still returned the pre-rename name (stale snapshot).");
        }
    }
}
