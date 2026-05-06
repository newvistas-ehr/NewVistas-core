// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end functional tests for Drug Interaction Blocking workflow.
/// Tests via IPatientWorkflowGrain — the same path used by controllers and Blazor pages.
///
/// Verifies that IDrugInteractionCheckerGrain is properly connected to the pharmacy
/// fill workflow with blocking and pharmacist override capabilities.
///
/// VistA reference: DRGINT.m integration in PSO fill workflow.
/// </summary>
[TestFixture]
public class InteractionBlockingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IDrugInteractionDatasetGrain Dataset()
        => _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");

    /// <summary>Load a known interaction pair for testing.</summary>
    private async Task SeedInteraction(
        string ien1, string name1, string ien2, string name2,
        InteractionSeverity severity, string description)
    {
        // Load via dataset grain — this populates the silo cache
        await Dataset().LoadInteractionsAsync(new List<DrugInteractionPair>
        {
            new DrugInteractionPair
            {
                IngredientIen1 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien1 : ien2,
                IngredientName1 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? name1 : name2,
                IngredientIen2 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien2 : ien1,
                IngredientName2 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? name2 : name1,
                Severity = severity,
                Description = description,
                ClinicalEffects = $"Clinical effects of {name1}/{name2} interaction",
                Mechanism = "Pharmacokinetic",
                Management = "Monitor closely or avoid combination"
            }
        });
    }

    // ─── Basic Screening — No Interactions ──────────────────────────────────

    [Test]
    public async Task ScreenPrescription_NoInteractions_Cleared()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Screen with ingredients that have no known interactions
        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            "RX-001", "METFORMIN 500MG",
            new List<DrugIngredient>
            {
                new DrugIngredient { IngredientIen = $"SAFE-{Guid.NewGuid()}", Name = "METFORMIN HCL" }
            },
            new List<DrugIngredient>
            {
                new DrugIngredient { IngredientIen = $"SAFE-{Guid.NewGuid()}", Name = "LISINOPRIL" }
            },
            "PHARM-001");

        Assert.That(screeningId, Does.StartWith("IXSCREEN:"));

        InteractionScreeningState state = await wf.GetInteractionScreeningAsync(screeningId);
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.Cleared));
        Assert.That(state.TotalInteractionCount, Is.EqualTo(0));
        Assert.That(state.BlockingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ScreenPrescription_Cleared_AppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.ScreenPrescriptionForInteractionsAsync(
            "RX-IDX-01", "ASPIRIN 81MG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "ASPIRIN" } },
            new List<DrugIngredient>(),
            "PHARM-001");

        List<InteractionScreeningIndexEntry> all = await wf.GetInteractionScreeningsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DrugName, Is.EqualTo("ASPIRIN 81MG"));
        Assert.That(all[0].Status, Is.EqualTo(InteractionScreeningStatus.Cleared));
    }

    // ─── Blocking Interactions (using seeded dataset) ───────────────────────

    [Test]
    public async Task ScreenPrescription_SignificantInteraction_Blocked()
    {
        // Seed a known significant interaction
        string ien1 = $"IEN-SIG-{Guid.NewGuid():N}";
        string ien2 = $"IEN-SIG-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "WARFARIN SODIUM", ien2, "METRONIDAZOLE",
            InteractionSeverity.Significant, "Significantly increases INR risk");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            "RX-BLOCK-01", "WARFARIN 5MG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "WARFARIN SODIUM" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "METRONIDAZOLE" } },
            "PHARM-001");

        InteractionScreeningState state = await wf.GetInteractionScreeningAsync(screeningId);

        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));
        Assert.That(state.TotalInteractionCount, Is.EqualTo(1));
        Assert.That(state.BlockingCount, Is.EqualTo(1));
        Assert.That(state.Findings[0].Severity, Is.EqualTo(InteractionSeverity.Significant));
        Assert.That(state.Findings[0].IsBlocking, Is.True);
        Assert.That(state.Findings[0].Description, Does.Contain("INR"));
    }

    [Test]
    public async Task ScreenPrescription_ContraindicatedInteraction_Blocked()
    {
        string ien1 = $"IEN-CI-{Guid.NewGuid():N}";
        string ien2 = $"IEN-CI-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "LINEZOLID", ien2, "SELEGILINE",
            InteractionSeverity.Contraindicated, "Serotonin syndrome risk — contraindicated");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            "RX-CI-01", "LINEZOLID 600MG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "LINEZOLID" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "SELEGILINE" } },
            "PHARM-001");

        InteractionScreeningState state = await wf.GetInteractionScreeningAsync(screeningId);

        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));
        Assert.That(state.Findings[0].Severity, Is.EqualTo(InteractionSeverity.Contraindicated));
    }

    // ─── Fill Clearance Check ───────────────────────────────────────────────

    [Test]
    public async Task IsPrescriptionClearedForFill_NotScreened_ReturnsTrue()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        bool cleared = await wf.IsPrescriptionClearedForFillAsync($"RX-NEVER-SCREENED-{Guid.NewGuid()}");
        Assert.That(cleared, Is.True);
    }

    [Test]
    public async Task IsPrescriptionClearedForFill_Cleared_ReturnsTrue()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-CLR-{Guid.NewGuid()}";
        await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "SAFE DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "SAFE" } },
            new List<DrugIngredient>(), "PHARM-001");

        bool cleared = await wf.IsPrescriptionClearedForFillAsync(rxId);
        Assert.That(cleared, Is.True);
    }

    [Test]
    public async Task IsPrescriptionClearedForFill_Blocked_ReturnsFalse()
    {
        string ien1 = $"IEN-BLK-{Guid.NewGuid():N}";
        string ien2 = $"IEN-BLK-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "DRUG A", ien2, "DRUG B",
            InteractionSeverity.Significant, "Blocks fill");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-BLK-{Guid.NewGuid()}";
        await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "DRUG A",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG A" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG B" } },
            "PHARM-001");

        bool cleared = await wf.IsPrescriptionClearedForFillAsync(rxId);
        Assert.That(cleared, Is.False);
    }

    // ─── Override Workflow ──────────────────────────────────────────────────

    [Test]
    public async Task OverrideInteraction_UnblocksPrescription()
    {
        string ien1 = $"IEN-OVR-{Guid.NewGuid():N}";
        string ien2 = $"IEN-OVR-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "DRUG C", ien2, "DRUG D",
            InteractionSeverity.Significant, "Override test");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-OVR-{Guid.NewGuid()}";
        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "DRUG C",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG C" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG D" } },
            "PHARM-001");

        Assert.That(await wf.IsPrescriptionClearedForFillAsync(rxId), Is.False);

        await wf.OverrideInteractionBlockAsync(screeningId, 0, "PHARM-SENIOR",
            "Clinical necessity — patient has no alternative. Will monitor closely.");

        Assert.That(await wf.IsPrescriptionClearedForFillAsync(rxId), Is.True);

        InteractionScreeningState state = await wf.GetInteractionScreeningAsync(screeningId);
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.OverriddenByPharmacist));
        Assert.That(state.Findings[0].IsOverridden, Is.True);
        Assert.That(state.Findings[0].OverriddenBy, Is.EqualTo("PHARM-SENIOR"));
    }

    [Test]
    public async Task OverrideInteraction_UpdatesIndex()
    {
        string ien1 = $"IEN-IDX-{Guid.NewGuid():N}";
        string ien2 = $"IEN-IDX-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "DRUG E", ien2, "DRUG F",
            InteractionSeverity.Significant, "Index test");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-IDX-{Guid.NewGuid()}";
        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "DRUG E",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG E" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG F" } },
            "PHARM-001");

        List<InteractionScreeningIndexEntry> blocked = await wf.GetBlockedPrescriptionsAsync();
        Assert.That(blocked, Has.Count.EqualTo(1));

        await wf.OverrideInteractionBlockAsync(screeningId, 0, "PHARM-001", "Justified.");

        blocked = await wf.GetBlockedPrescriptionsAsync();
        Assert.That(blocked, Has.Count.EqualTo(0));
    }

    // ─── Blocked Prescriptions Query ────────────────────────────────────────

    [Test]
    public async Task GetBlockedPrescriptions_OnlyReturnsBlocked()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Cleared screening
        await wf.ScreenPrescriptionForInteractionsAsync(
            $"RX-CLR-{Guid.NewGuid()}", "SAFE DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "SAFE" } },
            new List<DrugIngredient>(), "PHARM-001");

        // Blocked screening
        string ien1 = $"IEN-Q-{Guid.NewGuid():N}";
        string ien2 = $"IEN-Q-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "DRUG G", ien2, "DRUG H",
            InteractionSeverity.Significant, "Query test");

        await wf.ScreenPrescriptionForInteractionsAsync(
            $"RX-BLK-{Guid.NewGuid()}", "DRUG G",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG G" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG H" } },
            "PHARM-001");

        List<InteractionScreeningIndexEntry> blocked = await wf.GetBlockedPrescriptionsAsync();
        Assert.That(blocked, Has.Count.EqualTo(1));
        Assert.That(blocked[0].DrugName, Is.EqualTo("DRUG G"));
    }

    // ─── Prescription Lookup ────────────────────────────────────────────────

    [Test]
    public async Task GetScreeningForPrescription_FindsCorrectScreening()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-LOOKUP-{Guid.NewGuid()}";
        await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "ATORVASTATIN 40MG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "ATORVASTATIN" } },
            new List<DrugIngredient>(), "PHARM-001");

        InteractionScreeningState state = await wf.GetInteractionScreeningForPrescriptionAsync(rxId);
        Assert.That(state.PrescriptionId, Is.EqualTo(rxId));
        Assert.That(state.DrugName, Is.EqualTo("ATORVASTATIN 40MG"));
    }

    // ─── Patient Isolation ──────────────────────────────────────────────────

    [Test]
    public async Task DifferentPatients_HaveIndependentScreenings()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";

        await Workflow(p1).ScreenPrescriptionForInteractionsAsync(
            $"RX-{Guid.NewGuid()}", "DRUG 1",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "D1" } },
            new List<DrugIngredient>(), null);

        await Workflow(p2).ScreenPrescriptionForInteractionsAsync(
            $"RX-{Guid.NewGuid()}", "DRUG 2",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "D2" } },
            new List<DrugIngredient>(), null);

        await Workflow(p2).ScreenPrescriptionForInteractionsAsync(
            $"RX-{Guid.NewGuid()}", "DRUG 3",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "D3" } },
            new List<DrugIngredient>(), null);

        List<InteractionScreeningIndexEntry> p1List = await Workflow(p1).GetInteractionScreeningsAsync();
        List<InteractionScreeningIndexEntry> p2List = await Workflow(p2).GetInteractionScreeningsAsync();

        Assert.That(p1List, Has.Count.EqualTo(1));
        Assert.That(p2List, Has.Count.EqualTo(2));
    }

    // ─── Minor/Moderate Interactions — Not Blocking ─────────────────────────

    [Test]
    public async Task ScreenPrescription_ModerateInteraction_NotBlocking()
    {
        string ien1 = $"IEN-MOD-{Guid.NewGuid():N}";
        string ien2 = $"IEN-MOD-{Guid.NewGuid():N}";
        await SeedInteraction(ien1, "DRUG MOD1", ien2, "DRUG MOD2",
            InteractionSeverity.Moderate, "Moderate interaction — monitoring needed");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-MOD-{Guid.NewGuid()}";
        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(
            rxId, "DRUG MOD1",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG MOD1" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG MOD2" } },
            "PHARM-001");

        InteractionScreeningState state = await wf.GetInteractionScreeningAsync(screeningId);

        // Moderate interaction is found but NOT blocking
        Assert.That(state.TotalInteractionCount, Is.EqualTo(1));
        Assert.That(state.BlockingCount, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.Cleared));
        Assert.That(state.Findings[0].IsBlocking, Is.False);

        bool cleared = await wf.IsPrescriptionClearedForFillAsync(rxId);
        Assert.That(cleared, Is.True);
    }
}
