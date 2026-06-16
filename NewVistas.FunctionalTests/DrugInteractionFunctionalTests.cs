// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional / end-to-end tests for the drug interaction subsystem.
///
/// These tests exercise complete workflows spanning:
///   - IDrugInteractionDatasetGrain  (persistent singleton store)
///   - IDrugInteractionCheckerGrain  (StatelessWorker, reads silo cache)
///   - IDrugInteractionCacheService  (silo-level singleton, immutable swap)
///
/// Critical setup pattern: IDrugInteractionCacheService must be registered
/// via siloBuilder.Services inside ISiloConfigurator. Orleans TestCluster
/// resolves grain constructor dependencies from the silo's IServiceCollection,
/// so the singleton must live there — not in a separate host configurator.
///
/// Every test is self-contained: it uses GUID-based ingredient IENs to
/// avoid interference with other tests that share the "DI-DATASET" singleton.
/// </summary>
// NonParallelizable: these tests exercise replace-all loads, Clear, and
// global count/status assertions on the shared DI-DATASET singleton. Running
// them concurrently with other fixtures (which merge-seed the same singleton)
// races both directions.
[TestFixture, NonParallelizable]
public class DrugInteractionWorkflowTests
{
    private TestCluster _cluster = default!;

    private const string DatasetKey = "DI-DATASET";
    private const string CheckerKey = "CHECKER";

    // ─── Test Cluster Configuration ───────────────────────────────────────────

    /// <summary>
    /// Configures the test silo with the grain store AND the DI singleton cache service.
    ///
    /// Critical pattern: IDrugInteractionCacheService must be registered via
    /// siloBuilder.Services inside ISiloConfigurator — NOT in a separate
    /// IHostConfigurator. Orleans TestCluster resolves grain constructor
    /// dependencies from the silo's IServiceCollection, so both the store
    /// and the singleton must be registered in the same Configure call.
    /// </summary>
    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static DrugIngredient Ingredient(string ien, string name) =>
        new() { IngredientIen = ien, Name = name };

    private static DrugInteractionPair Pair(
        string ien1, string name1,
        string ien2, string name2,
        InteractionSeverity severity,
        string description,
        string? effects = null,
        string? mechanism = null,
        string? management = null) =>
        new()
        {
            IngredientIen1 = ien1,
            IngredientName1 = name1,
            IngredientIen2 = ien2,
            IngredientName2 = name2,
            Severity = severity,
            Description = description,
            ClinicalEffects = effects,
            Mechanism = mechanism,
            Management = management
        };

    // ─── Dataset Grain: Load & Retrieval ──────────────────────────────────────

    /// <summary>
    /// LoadInteractionsAsync persists all pairs; GetAllInteractionsAsync returns them.
    /// </summary>
    [Test]
    public async Task Dataset_LoadInteractions_PersistsAllPairs()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienA = $"IEN-{run}-A";
        string ienB = $"IEN-{run}-B";
        string ienC = $"IEN-{run}-C";

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair(ienA, "WARFARIN", ienB, "ASPIRIN", InteractionSeverity.Contraindicated,
                "Concomitant use increases bleeding risk"),
            Pair(ienA, "WARFARIN", ienC, "IBUPROFEN", InteractionSeverity.Significant,
                "NSAIDs enhance anticoagulant effect")
        ]);

        List<DrugInteractionPair> all = await dataset.GetAllInteractionsAsync();
        Assert.That(all, Has.Count.GreaterThanOrEqualTo(2));

        bool warf_asp = all.Any(p =>
            (p.IngredientIen1 == ienA || p.IngredientIen2 == ienA) &&
            (p.IngredientIen1 == ienB || p.IngredientIen2 == ienB));
        bool warf_ibu = all.Any(p =>
            (p.IngredientIen1 == ienA || p.IngredientIen2 == ienA) &&
            (p.IngredientIen1 == ienC || p.IngredientIen2 == ienC));

        Assert.That(warf_asp, Is.True, "WARFARIN-ASPIRIN pair not found");
        Assert.That(warf_ibu, Is.True, "WARFARIN-IBUPROFEN pair not found");
    }

    /// <summary>
    /// GetInteractionAsync returns the pair regardless of the argument order passed.
    /// </summary>
    [Test]
    public async Task Dataset_GetInteraction_OrderIndependent()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienA = $"IEN-{run}-OI-A";
        string ienB = $"IEN-{run}-OI-B";

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair(ienA, "WARFARIN", ienB, "ASPIRIN",
                InteractionSeverity.Contraindicated, "Bleeding risk",
                effects: "Hemorrhage", mechanism: "Platelet inhibition")
        ]);

        DrugInteractionPair? forward = await dataset.GetInteractionAsync(ienA, ienB);
        DrugInteractionPair? reverse = await dataset.GetInteractionAsync(ienB, ienA);

        Assert.That(forward, Is.Not.Null, "Forward lookup failed");
        Assert.That(reverse, Is.Not.Null, "Reverse lookup failed");
        Assert.That(forward!.Severity, Is.EqualTo(InteractionSeverity.Contraindicated));
        Assert.That(forward.Description, Is.EqualTo(reverse!.Description));
        Assert.That(forward.ClinicalEffects, Is.EqualTo("Hemorrhage"));
    }

    /// <summary>
    /// GetInteractionAsync returns null when no interaction is registered for the pair.
    /// </summary>
    [Test]
    public async Task Dataset_GetInteraction_ReturnsNullForUnknownPair()
    {
        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        DrugInteractionPair? result = await dataset.GetInteractionAsync(
            $"IEN-UNKNOWN-{Guid.NewGuid():N}",
            $"IEN-UNKNOWN-{Guid.NewGuid():N}");

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// GetStatusAsync reflects the loaded state and correct pair count.
    /// </summary>
    [Test]
    public async Task Dataset_GetStatus_ReflectsLoadedState()
    {
        string run = Guid.NewGuid().ToString("N")[..8];

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair($"IEN-{run}-X", "DRUGX", $"IEN-{run}-Y", "DRUGY",
                InteractionSeverity.Moderate, "Test interaction")
        ]);

        DrugInteractionStatus status = await dataset.GetStatusAsync();

        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.LastLoadedDate, Is.Not.Null);
        Assert.That(status.TotalPairs, Is.GreaterThan(0));
        Assert.That(status.IsCachePopulated, Is.True);
    }

    /// <summary>
    /// ClearAsync removes all pairs, resets the dataset, and empties the cache.
    /// </summary>
    [Test]
    public async Task Dataset_Clear_RemovesAllPairs()
    {
        string run = Guid.NewGuid().ToString("N")[..8];

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair($"IEN-{run}-P", "P", $"IEN-{run}-Q", "Q",
                InteractionSeverity.Minor, "Minor test")
        ]);

        await dataset.ClearAsync();

        List<DrugInteractionPair> all = await dataset.GetAllInteractionsAsync();
        DrugInteractionStatus status = await dataset.GetStatusAsync();

        Assert.That(all, Is.Empty);
        Assert.That(status.IsLoaded, Is.False);
        Assert.That(status.TotalPairs, Is.EqualTo(0));

        // Restore a loaded dataset: the fail-closed checker blocks screening
        // when IsLoaded is false, and this DI-DATASET singleton is shared with
        // every other fixture on the SharedCluster.
        await dataset.LoadInteractionsAsync(
        [
            Pair($"IEN-{run}-RESTORE-A", "A", $"IEN-{run}-RESTORE-B", "B",
                InteractionSeverity.Minor, "Post-clear restore pair")
        ]);
    }

    /// <summary>
    /// Duplicate pair entries (same key, different order) collapse to a single entry.
    /// </summary>
    [Test]
    public async Task Dataset_LoadInteractions_DeduplicatesByCanonicalKey()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienA = $"IEN-{run}-DUP-A";
        string ienB = $"IEN-{run}-DUP-B";

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        // Both orderings of the same pair — must collapse to one entry.
        await dataset.LoadInteractionsAsync(
        [
            Pair(ienA, "DRUG-A", ienB, "DRUG-B", InteractionSeverity.Moderate, "First"),
            Pair(ienB, "DRUG-B", ienA, "DRUG-A", InteractionSeverity.Significant, "Second")
        ]);

        List<DrugInteractionPair> all = await dataset.GetAllInteractionsAsync();
        int count = all.Count(p =>
            (p.IngredientIen1 == ienA || p.IngredientIen1 == ienB) &&
            (p.IngredientIen2 == ienA || p.IngredientIen2 == ienB));

        // Dictionary semantics: last write wins; only one entry survives.
        Assert.That(count, Is.EqualTo(1));
    }

    // ─── Checker Grain Tests ──────────────────────────────────────────────────

    /// <summary>
    /// CheckInteractionsAsync finds a known interaction after the dataset is loaded.
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_FindsKnownInteraction()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienW = $"IEN-{run}-WARF";
        string ienA = $"IEN-{run}-ASP";

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair(ienW, "WARFARIN SODIUM", ienA, "ASPIRIN",
                InteractionSeverity.Contraindicated,
                "Increased bleeding risk with co-administration",
                effects: "Hemorrhage",
                management: "Avoid combination")
        ]);

        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        DrugInteractionCheckResponse response = await checker.CheckInteractionsAsync(
        [
            Ingredient(ienW, "WARFARIN SODIUM"),
            Ingredient(ienA, "ASPIRIN")
        ]);

        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        List<DrugInteractionResult> results = response.Results;

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Interaction.Severity, Is.EqualTo(InteractionSeverity.Contraindicated));
        Assert.That(results[0].Interaction.Description, Does.Contain("bleeding"));
        Assert.That(results[0].Interaction.ClinicalEffects, Is.EqualTo("Hemorrhage"));
        Assert.That(results[0].Interaction.Management, Is.EqualTo("Avoid combination"));
    }

    /// <summary>
    /// CheckInteractionsAsync with an empty ingredient list returns empty.
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_EmptyListReturnsEmpty()
    {
        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        DrugInteractionCheckResponse response =
            await checker.CheckInteractionsAsync([]);

        // Fewer than two ingredients is legitimately "nothing to check",
        // not a data-availability problem — Ok even before any load.
        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        Assert.That(response.Results, Is.Empty);
    }

    /// <summary>
    /// CheckInteractionsAsync with a single ingredient returns empty (no pair possible).
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_SingleIngredientReturnsEmpty()
    {
        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        DrugInteractionCheckResponse response = await checker.CheckInteractionsAsync(
        [
            Ingredient("IEN-SINGLE-1", "METFORMIN")
        ]);

        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        Assert.That(response.Results, Is.Empty);
    }

    /// <summary>
    /// CheckInteractionsAsync returns empty when no pair has a known interaction.
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_NoResultsForUnrelatedIngredients()
    {
        string run = Guid.NewGuid().ToString("N")[..8];

        // Fail-closed checker requires a loaded dataset; seed one with an
        // unrelated pair so the check runs deterministically regardless of
        // test ordering on the shared cluster.
        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);
        await dataset.LoadInteractionsAsync(
        [
            Pair($"IEN-{run}-UNREL-X", "X", $"IEN-{run}-UNREL-Y", "Y",
                InteractionSeverity.Minor, "Unrelated seed pair")
        ]);

        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        DrugInteractionCheckResponse response = await checker.CheckInteractionsAsync(
        [
            Ingredient($"IEN-{run}-SAFE1", "VITAMIN-C"),
            Ingredient($"IEN-{run}-SAFE2", "VITAMIN-D")
        ]);

        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        Assert.That(response.Results, Is.Empty);
    }

    /// <summary>
    /// Checker finds all pairwise interactions in a three-drug list where
    /// two of three pairs have known interactions.
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_FindsAllPairsInMultiDrugList()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienW = $"IEN-{run}-MDL-W";
        string ienA = $"IEN-{run}-MDL-A";
        string ienI = $"IEN-{run}-MDL-I";

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair(ienW, "WARFARIN", ienA, "ASPIRIN",
                InteractionSeverity.Contraindicated, "Bleeding risk"),
            Pair(ienW, "WARFARIN", ienI, "IBUPROFEN",
                InteractionSeverity.Significant, "Enhanced anticoagulation")
            // ASPIRIN + IBUPROFEN: no interaction registered in this test dataset
        ]);

        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        DrugInteractionCheckResponse response = await checker.CheckInteractionsAsync(
        [
            Ingredient(ienW, "WARFARIN"),
            Ingredient(ienA, "ASPIRIN"),
            Ingredient(ienI, "IBUPROFEN")
        ]);

        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        List<DrugInteractionResult> results = response.Results;

        Assert.That(results, Has.Count.EqualTo(2));

        bool allInvolveWarfarin = results.All(r =>
            r.Drug1.IngredientIen == ienW || r.Drug2.IngredientIen == ienW);
        Assert.That(allInvolveWarfarin, Is.True, "Both interactions should involve WARFARIN");
    }

    /// <summary>
    /// Checker skips self-interaction: same ingredient IEN appearing in two products.
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_SkipsSelfInteraction()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienSelf = $"IEN-SELF-{run}";

        // Seed the dataset so the fail-closed checker runs deterministically
        // regardless of test ordering on the shared cluster.
        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);
        await dataset.LoadInteractionsAsync(
        [
            Pair($"IEN-{run}-SELF-X", "X", $"IEN-{run}-SELF-Y", "Y",
                InteractionSeverity.Minor, "Self-interaction seed pair")
        ]);

        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        DrugInteractionCheckResponse response = await checker.CheckInteractionsAsync(
        [
            Ingredient(ienSelf, "METOPROLOL TARTRATE"),
            Ingredient(ienSelf, "METOPROLOL SUCCINATE")   // same active ingredient, different salt/form
        ]);

        Assert.That(response.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        Assert.That(response.Results, Is.Empty);
    }

    /// <summary>
    /// IsCacheReadyAsync returns true once the dataset grain has been loaded.
    /// </summary>
    [Test]
    public async Task Checker_IsCacheReady_TrueAfterDatasetLoad()
    {
        string run = Guid.NewGuid().ToString("N")[..8];

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair($"IEN-{run}-RDY-X", "X", $"IEN-{run}-RDY-Y", "Y",
                InteractionSeverity.Minor, "Cache readiness test")
        ]);

        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        bool ready = await checker.IsCacheReadyAsync();
        Assert.That(ready, Is.True);
    }

    /// <summary>
    /// Checker finds the same interaction regardless of the order ingredients are passed.
    /// </summary>
    [Test]
    public async Task Checker_CheckInteractions_BothArgumentOrdersMatchSameInteraction()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ienW = $"IEN-{run}-ORD-W";
        string ienA = $"IEN-{run}-ORD-A";

        IDrugInteractionDatasetGrain dataset =
            _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>(DatasetKey);

        await dataset.LoadInteractionsAsync(
        [
            Pair(ienW, "WARFARIN", ienA, "ASPIRIN",
                InteractionSeverity.Contraindicated, "Bleeding")
        ]);

        IDrugInteractionCheckerGrain checker =
            _cluster.GrainFactory.GetGrain<IDrugInteractionCheckerGrain>(CheckerKey);

        // Forward order
        DrugInteractionCheckResponse fwdResponse = await checker.CheckInteractionsAsync(
        [
            Ingredient(ienW, "WARFARIN"),
            Ingredient(ienA, "ASPIRIN")
        ]);

        // Reverse order
        DrugInteractionCheckResponse revResponse = await checker.CheckInteractionsAsync(
        [
            Ingredient(ienA, "ASPIRIN"),
            Ingredient(ienW, "WARFARIN")
        ]);

        Assert.That(fwdResponse.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));
        Assert.That(revResponse.Status, Is.EqualTo(DrugInteractionCheckStatus.Ok));

        List<DrugInteractionResult> fwd = fwdResponse.Results;
        List<DrugInteractionResult> rev = revResponse.Results;

        Assert.That(fwd, Has.Count.EqualTo(1));
        Assert.That(rev, Has.Count.EqualTo(1));
        Assert.That(
            fwd[0].Interaction.Description,
            Is.EqualTo(rev[0].Interaction.Description));
        Assert.That(
            fwd[0].Interaction.Severity,
            Is.EqualTo(rev[0].Interaction.Severity));
    }
}
