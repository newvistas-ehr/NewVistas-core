// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Regression tests for the PatientState capping migration: each clinical
/// domain keeps only the most recent N item IDs (site parameter
/// RecentItemsDisplayCount, default 5) on the hot patient blob, with the
/// COMPLETE history preserved in per-domain IPatientHistoryIndexGrain.
///
/// Owner-decided invariants under test:
///   - Allergies are NEVER capped — complete allergy data travels with the patient.
///   - No trim may ever occur before the full legacy list is flushed to the
///     history index (crash-safety of the lazy migration).
///   - Clinical complete-set reads (active meds, due reminders, CWAD advance
///     directives) must see items beyond the capped recent window.
/// </summary>
[TestFixture]
public class PatientHistoryCappingTests
{
    private TestCluster _cluster = default!;

    private const int DefaultCap = 5; // SiteParametersState.RecentItemsDisplayCount default

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain Patient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IPatientHistoryIndexGrain History(string patientId, string domain)
        => _cluster.GrainFactory.GetGrain<IPatientHistoryIndexGrain>($"{patientId}:{domain}");

    // ─── Site parameter ──────────────────────────────────────────────────────

    [Test]
    public async Task SiteParametersGrain_RecentItemsDisplayCountDefaultsToFive()
    {
        ISiteParametersGrain site = _cluster.GrainFactory
            .GetGrain<ISiteParametersGrain>($"SITE:TEST-{Guid.NewGuid()}");

        Assert.That(await site.GetRecentItemsDisplayCountAsync(), Is.EqualTo(5));
    }

    // ─── PatientGrain capped-list machinery ──────────────────────────────────

    [Test]
    public async Task PatientGrain_CappedListDoesNotTrimBeforeMigrationFlag()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        // Pre-migration: capped adds must NOT trim — the list is still the
        // only copy of the history.
        for (int i = 0; i < DefaultCap + 7; i++)
            await patient.AddDomainIdCappedAsync(PatientHistoryDomains.Consult, $"CONSULT-{i}", DefaultCap);

        List<string> ids = await patient.GetDomainIdsAsync(PatientHistoryDomains.Consult);
        Assert.That(ids, Has.Count.EqualTo(DefaultCap + 7),
            "List was trimmed before the migration flag was set — un-flushed history lost.");
        Assert.That(await patient.IsDomainMigratedAsync(PatientHistoryDomains.Consult), Is.False);
    }

    [Test]
    public async Task PatientGrain_CappedListTrimsToSiteParameter()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        for (int i = 0; i < 9; i++)
            await patient.AddDomainIdCappedAsync(PatientHistoryDomains.Surgery, $"SURG-{i}", DefaultCap);

        await patient.MarkDomainMigratedAndTrimAsync(PatientHistoryDomains.Surgery, DefaultCap);

        List<string> afterTrim = await patient.GetDomainIdsAsync(PatientHistoryDomains.Surgery);
        Assert.That(afterTrim, Has.Count.EqualTo(DefaultCap));
        Assert.That(afterTrim, Is.EqualTo(new[] { "SURG-4", "SURG-5", "SURG-6", "SURG-7", "SURG-8" }),
            "Trim must keep the most recent entries (lists append chronologically).");

        // Post-migration: capped adds trim on every append.
        await patient.AddDomainIdCappedAsync(PatientHistoryDomains.Surgery, "SURG-NEW", DefaultCap);
        List<string> afterAdd = await patient.GetDomainIdsAsync(PatientHistoryDomains.Surgery);
        Assert.That(afterAdd, Has.Count.EqualTo(DefaultCap));
        Assert.That(afterAdd[^1], Is.EqualTo("SURG-NEW"));
        Assert.That(afterAdd, Does.Not.Contain("SURG-4"));
    }

    [Test]
    public async Task PatientGrain_Allergies_NeverCapped()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Record well past the recent-window cap — every allergy must survive.
        for (int i = 0; i < DefaultCap * 4; i++)
        {
            await wf.RecordAllergyAsync(
                $"ALLERGEN-{i}", "DRUG", null, "OBSERVED",
                ["HIVES"], "Moderate", null, null, null);
        }

        List<AllergySummary> allergies = await wf.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(DefaultCap * 4),
            "Allergies must NEVER be capped — the complete allergy list travels with the patient.");
    }

    // ─── PatientHistoryIndexGrain ────────────────────────────────────────────

    [Test]
    public async Task PatientHistoryIndexGrain_AddRangeIsIdempotent()
    {
        IPatientHistoryIndexGrain history = History($"PATIENT-{Guid.NewGuid()}", PatientHistoryDomains.Lab);

        List<HistoryRef> batch =
        [
            new HistoryRef { ItemId = "LAB-1", Date = null },
            new HistoryRef { ItemId = "LAB-2", Date = null },
            new HistoryRef { ItemId = "LAB-3", Date = null }
        ];

        await history.AddRangeAsync(batch);
        await history.AddRangeAsync(batch); // retry (e.g., crashed migration re-run)
        await history.AddEntryAsync(new HistoryRef { ItemId = "LAB-2", Date = DateTime.UtcNow });

        Assert.That(await history.GetCountAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task PatientHistoryIndexGrain_GetPageReturnsNewestFirst()
    {
        IPatientHistoryIndexGrain history = History($"PATIENT-{Guid.NewGuid()}", PatientHistoryDomains.Radiology);

        DateTime baseDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 8; i++)
            await history.AddEntryAsync(new HistoryRef { ItemId = $"RAD-{i}", Date = baseDate.AddDays(i) });

        List<string> firstPage = await history.GetPageAsync(0, 3);
        Assert.That(firstPage, Is.EqualTo(new[] { "RAD-7", "RAD-6", "RAD-5" }));

        List<string> secondPage = await history.GetPageAsync(3, 3);
        Assert.That(secondPage, Is.EqualTo(new[] { "RAD-4", "RAD-3", "RAD-2" }));

        Assert.That(await history.GetCountAsync(), Is.EqualTo(8));
    }

    // ─── Lazy migration through the workflow grain ───────────────────────────

    [Test]
    public async Task PatientWorkflowGrain_MigrationOverflowFlushedToHistoryIndexBeforeTrim()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        // Simulate a pre-capping persisted blob: 20 consult IDs via the legacy
        // unbounded append.
        for (int i = 0; i < 20; i++)
            await patient.AddConsultIdAsync($"CONSULT-LEGACY-{i}");

        // First workflow write triggers the lazy migration.
        string newConsultId = await Workflow(patientId).RequestConsultAsync(
            "CARDIOLOGY", null, "PRIMARY CARE", null, "Routine",
            null, null, null, null, "Eval", null, null, null, null);

        // Every legacy ID plus the new one must be in the history index...
        IPatientHistoryIndexGrain history = History(patientId, PatientHistoryDomains.Consult);
        List<string> allIds = await history.GetAllIdsAsync();
        Assert.That(allIds, Has.Count.EqualTo(21),
            "Migration must flush ALL legacy IDs to the history index before any trim.");
        Assert.That(allIds, Does.Contain("CONSULT-LEGACY-0"));
        Assert.That(allIds, Does.Contain(newConsultId));

        // ...while the hot patient list is trimmed to the recent window.
        List<string> windowIds = await patient.GetDomainIdsAsync(PatientHistoryDomains.Consult);
        Assert.That(windowIds, Has.Count.EqualTo(DefaultCap));
        Assert.That(windowIds[^1], Is.EqualTo(newConsultId));
        Assert.That(await patient.IsDomainMigratedAsync(PatientHistoryDomains.Consult), Is.True);
    }

    // ─── Clinical complete-set reads beyond the window ───────────────────────

    [Test]
    public async Task PatientWorkflowGrain_ActiveMedications_CompleteDespiteCappedPharmacyIds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);
        const int activeCount = 10; // double the recent window

        for (int i = 0; i < activeCount; i++)
        {
            string rxId = $"RX-CAP-{patientId}-{i}";
            IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
            await rx.CreatePrescriptionAsync(
                patientId, $"DRUG-{i} 10MG", null, null, null, null, null,
                30, 30, 3, null, null, null, null, null, null);
            await patient.AddPharmacyIdAsync(rxId);
        }

        // PATIENT SAFETY: interaction screening consumes this — all 10 active
        // prescriptions must be present even though the recent window holds 5.
        List<MedicationSummary> meds = await Workflow(patientId).GetActiveMedicationsAsync();
        Assert.That(meds, Has.Count.EqualTo(activeCount),
            "Active medication set must be COMPLETE — it feeds drug-interaction screening.");
    }

    [Test]
    public async Task PatientWorkflowGrain_ActiveMedications_BackfillsEmptyPrescriptionIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        for (int i = 0; i < 7; i++)
        {
            string rxId = $"RX-BF-{patientId}-{i}";
            IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
            await rx.CreatePrescriptionAsync(
                patientId, $"BACKFILL-{i} 5MG", null, null, null, null, null,
                30, 30, 3, null, null, null, null, null, null);
            await patient.AddPharmacyIdAsync(rxId);
        }

        // Simulate a pre-index deployment: wipe the PSO index so only the
        // legacy ID list knows these prescriptions.
        IPatientPrescriptionIndexGrain pso = _cluster.GrainFactory
            .GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");
        await pso.ClearAsync();

        List<MedicationSummary> meds = await Workflow(patientId).GetActiveMedicationsAsync();
        Assert.That(meds, Has.Count.EqualTo(7),
            "Empty PSO index must self-heal by backfilling from the complete legacy ID set.");
        Assert.That(await pso.GetTotalCountAsync(), Is.EqualTo(7));
    }

    [Test]
    public async Task PatientWorkflowGrain_Reminders_DueReminderBeyondCapStillReturned()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // First reminder will fall out of the recent window after cap+N more.
        string oldest = await wf.CreateReminderAsync(
            "OLDEST-REMINDER", null, "PREVENTION", "HIGH", null, DateTime.UtcNow.AddDays(-30));

        for (int i = 0; i < DefaultCap + 3; i++)
            await wf.CreateReminderAsync($"REMINDER-{i}", null, "PREVENTION", null, null, DateTime.UtcNow.AddDays(i));

        List<ReminderSummary> reminders = await wf.GetRemindersAsync();
        Assert.That(reminders.Select(r => r.ReminderId), Does.Contain(oldest),
            "A reminder beyond the capped recent window must still be evaluated/returned.");
        Assert.That(reminders, Has.Count.EqualTo(DefaultCap + 4));
    }

    [Test]
    public async Task PatientWorkflowGrain_CwadFlags_AdvanceDirectiveBeyondCapStillFlagged()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Old advance directive, then push it past the recent window.
        await wf.CreateNoteAsync(
            "ADVANCE DIRECTIVE", null, "Living will on file.", "AD",
            null, "DR SMITH", null, null, null, null, null, DateTime.UtcNow.AddYears(-2));

        for (int i = 0; i < DefaultCap + 3; i++)
            await wf.CreateNoteAsync(
                "PROGRESS NOTE", null, $"Routine visit {i}.", $"Visit {i}",
                null, "DR SMITH", null, null, null, null, null, DateTime.UtcNow);

        CoverSheetState coverSheet = await wf.GetCoverSheetAsync();
        Assert.That(coverSheet.Cwad.HasAdvanceDirectives, Is.True,
            "An advance directive must never fall off the CWAD flags, however old.");
    }

    [Test]
    public async Task PatientWorkflowGrain_ConsultHistory_PagedBeyondRecentCap()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        const int total = 12;
        var created = new List<string>();
        for (int i = 0; i < total; i++)
        {
            created.Add(await wf.RequestConsultAsync(
                $"SERVICE-{i}", null, "PRIMARY CARE", null, "Routine",
                null, null, null, null, $"Eval {i}", null, null, null, null));
        }

        // Recent window holds only the cap...
        List<string> windowIds = await Patient(patientId).GetDomainIdsAsync(PatientHistoryDomains.Consult);
        Assert.That(windowIds, Has.Count.EqualTo(DefaultCap));

        // ...but paging walks the full history.
        List<ConsultSummary> page1 = await wf.GetConsultHistoryAsync(0, 5);
        List<ConsultSummary> page2 = await wf.GetConsultHistoryAsync(5, 5);
        List<ConsultSummary> page3 = await wf.GetConsultHistoryAsync(10, 5);

        var pagedIds = page1.Concat(page2).Concat(page3).Select(c => c.ConsultId).ToList();
        Assert.That(pagedIds, Has.Count.EqualTo(total));
        Assert.That(pagedIds, Is.EquivalentTo(created),
            "Paged history must cover every consult ever created, not just the recent window.");
    }

    [Test]
    public async Task OrderCheckGrain_DrugInteractionUsesCompleteActiveMedicationSet()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        // Ensure the interaction dataset is loaded (fail-closed checker).
        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
        string run = Guid.NewGuid().ToString("N")[..8];
        await dataset.AddInteractionsAsync(
        [
            new DrugInteractionPair
            {
                IngredientIen1 = $"IEN-{run}-A",
                IngredientName1 = "A",
                IngredientIen2 = $"IEN-{run}-B",
                IngredientName2 = "B",
                Severity = InteractionSeverity.Minor,
                Description = "Order-check seed pair"
            }
        ]);

        // 8 active prescriptions — more than the recent window holds.
        for (int i = 0; i < 8; i++)
        {
            string rxId = $"RX-OC-{patientId}-{i}";
            IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
            await rx.CreatePrescriptionAsync(
                patientId, $"OCDRUG-{i} 10MG", null, null, null, null, null,
                30, 30, 3, null, null, null, null, null, null);
            await patient.AddPharmacyIdAsync(rxId);
        }

        IOrderCheckGrain checker = _cluster.GrainFactory.GetGrain<IOrderCheckGrain>("ORDER-CHECK");
        List<OrderCheckResult> results = await checker.CheckOrderAsync(
            patientId, "Pharmacy", "NEWDRUG 20MG", null);

        OrderCheckResult? drugDrug = results.FirstOrDefault(r => r.CheckType == "DRUG_DRUG");
        Assert.That(drugDrug, Is.Not.Null, "Pharmacy order should produce a drug-drug check result.");
        Assert.That(drugDrug!.Message, Does.Contain("8 active medications"),
            "Order checking must see the COMPLETE active set, not the capped recent window.");
    }

    [Test]
    public async Task PatientMergeGrain_MergePreservesFullHistoryAcrossIndexes()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string sourceId = $"PATIENT-SRC-{run}";
        string targetId = $"PATIENT-TGT-{run}";

        await Workflow(sourceId).UpdateDemographicsAsync("MERGE,SOURCE", "M", new DateTime(1960, 1, 1), null);
        await Workflow(targetId).UpdateDemographicsAsync("MERGE,TARGET", "M", new DateTime(1960, 1, 1), null);

        // Source has consults beyond the recent window (forces migration first).
        var sourceConsults = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            sourceConsults.Add(await Workflow(sourceId).RequestConsultAsync(
                $"SVC-{i}", null, null, null, "Routine",
                null, null, null, null, $"Eval {i}", null, null, null, null));
        }

        IPatientMergeGrain merge = _cluster.GrainFactory.GetGrain<IPatientMergeGrain>($"MERGE:{run}");
        PatientMergeResult result = await merge.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate (capping test)", "ADMIN1", "Admin");
        Assert.That(result.Success, Is.True, result.ErrorMessage);

        // Every source consult must be reachable from the target's full history.
        List<string> targetHistory = await History(targetId, PatientHistoryDomains.Consult).GetAllIdsAsync();
        List<string> targetWindow = await Patient(targetId).GetDomainIdsAsync(PatientHistoryDomains.Consult);
        var reachable = targetHistory.Concat(targetWindow).ToHashSet();

        foreach (string consultId in sourceConsults)
            Assert.That(reachable, Does.Contain(consultId),
                $"Source consult {consultId} was lost in the merge — full history must survive.");
    }
}
