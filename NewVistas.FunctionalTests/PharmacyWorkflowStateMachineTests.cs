// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the Pharmacy Workflow State Machine.
/// Tests cross-grain DUR and interaction screening gates via IPatientWorkflowGrain.
///
/// VistA reference: PSOORED.m enforced DUR → Verify → Fill sequence.
/// </summary>
[TestFixture]
public class PharmacyWorkflowStateMachineTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> CreateAndReturnRxId(string patientId)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "TEST DRUG 10MG", null,
            "10mg", "ORAL", "QD", null, 30, 30, 5, null, null, null, null, null, null);
        return rxId;
    }

    private async Task PerformPassingDur(IPatientWorkflowGrain wf, string rxId)
    {
        await wf.PerformDurAsync(rxId, "TEST DRUG 10MG", null, null,
            "10mg", "ORAL", "QD", 30, 30, null, null,
            false, null, "PHARM-001",
            ingredientIens: new List<string> { "IEN-TEST" });
    }

    // ─── DUR Gate Tests ─────────────────────────────────────────────────────

    [Test]
    public async Task FillWorkflow_WithoutDur_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        // Verify directly on the grain (bypassing workflow to isolate DUR gate test)
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        // Fill via workflow should fail — no DUR performed
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task FillWorkflow_WithFailedDur_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        // Record an allergy that will trigger DUR failure
        await wf.RecordAllergyAsync("TEST DRUG", "DRUG", null, "OBSERVED",
            new List<string> { "RASH" }, "MODERATE", null, null, null);

        // Perform DUR — should fail on allergy check
        await wf.PerformDurAsync(rxId, "TEST DRUG 10MG", null, null,
            "10mg", "ORAL", "QD", 30, 30, null, null,
            false, null, "PHARM-001");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        // Fill via workflow should fail — DUR has failed status
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task FillWorkflow_WithPassedDur_Succeeds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        await PerformPassingDur(wf, rxId);

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));

        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.Not.Null);
    }

    [Test]
    public async Task FillWorkflow_WithOverriddenDur_Succeeds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        // Create allergy-triggered DUR failure
        await wf.RecordAllergyAsync("TEST DRUG", "DRUG", null, "OBSERVED",
            new List<string> { "RASH" }, "MODERATE", null, null, null);

        string durId = await wf.PerformDurAsync(rxId, "TEST DRUG 10MG", null, null,
            "10mg", "ORAL", "QD", 30, 30, null, null,
            false, null, "PHARM-001");

        // Override the allergy check
        await wf.OverrideDurCheckAsync(durId, DurCheckType.DrugAllergyContraindication,
            "PHARM-SENIOR", "Patient tolerates — documented.");

        // Acknowledge the DUR
        await wf.AcknowledgeDurAsync(durId, "PHARM-SENIOR", "Reviewed.");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    // ─── Interaction Gate Tests ──────────────────────────────────────────────

    [Test]
    public async Task FillWorkflow_WithBlockedInteraction_Throws()
    {
        // Seed a significant interaction
        string ien1 = $"IEN-WF-{Guid.NewGuid():N}";
        string ien2 = $"IEN-WF-{Guid.NewGuid():N}";
        IDrugInteractionDatasetGrain ds = _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
        // Merge, not replace: parallel fixtures share this singleton.
        await ds.AddInteractionsAsync(new List<DrugInteractionPair>
        {
            new DrugInteractionPair
            {
                IngredientIen1 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien1 : ien2,
                IngredientName1 = "DRUG A",
                IngredientIen2 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien2 : ien1,
                IngredientName2 = "DRUG B",
                Severity = InteractionSeverity.Significant,
                Description = "Workflow test"
            }
        });

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        await PerformPassingDur(wf, rxId);

        // Screen with blocking interaction
        await wf.ScreenPrescriptionForInteractionsAsync(rxId, "TEST DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG A" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG B" } },
            "PHARM-001");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task FillWorkflow_WithClearedInteraction_Succeeds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        await PerformPassingDur(wf, rxId);

        // Screen with no interactions (safe ingredients)
        await wf.ScreenPrescriptionForInteractionsAsync(rxId, "TEST DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"SAFE-{Guid.NewGuid()}", Name = "SAFE" } },
            new List<DrugIngredient>(), "PHARM-001");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task FillWorkflow_WithOverriddenInteraction_Succeeds()
    {
        string ien1 = $"IEN-OVR-{Guid.NewGuid():N}";
        string ien2 = $"IEN-OVR-{Guid.NewGuid():N}";
        IDrugInteractionDatasetGrain ds = _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
        // Merge, not replace: parallel fixtures share this singleton.
        await ds.AddInteractionsAsync(new List<DrugInteractionPair>
        {
            new DrugInteractionPair
            {
                IngredientIen1 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien1 : ien2,
                IngredientName1 = "DRUG X",
                IngredientIen2 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien2 : ien1,
                IngredientName2 = "DRUG Y",
                Severity = InteractionSeverity.Significant,
                Description = "Override test"
            }
        });

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        await PerformPassingDur(wf, rxId);

        string screeningId = await wf.ScreenPrescriptionForInteractionsAsync(rxId, "TEST DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG X" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG Y" } },
            "PHARM-001");

        // Override the blocking interaction
        await wf.OverrideInteractionBlockAsync(screeningId, 0, "PHARM-SENIOR", "Clinical necessity.");

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    // ─── Verify DUR Gate ────────────────────────────────────────────────────

    [Test]
    public async Task VerifyWorkflow_WithoutDur_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.VerifyPrescriptionWorkflowAsync(rxId, "RPH-001"));
    }

    // ─── Refill Gate Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RefillWorkflow_WithPassedDur_Succeeds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        await PerformPassingDur(wf, rxId);

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");
        await wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow.AddDays(-25));

        Assert.DoesNotThrowAsync(() => wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task RefillWorkflow_WithoutDur_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        // Bypass workflow to get to a fillable state
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");
        await rx.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-25));

        // Refill via workflow should fail — no DUR
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    // ─── Complete VistA PSOORED.m Sequence ──────────────────────────────────

    [Test]
    public async Task FullVistAWorkflow_CreateDurScreenVerifyLabelFillRefill()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        // 1. Perform DUR
        string durId = await wf.PerformDurAsync(rxId, "TEST DRUG 10MG", null, null,
            "10mg", "ORAL", "QD", 30, 30, null, null,
            false, null, "PHARM-001",
            ingredientIens: new List<string> { "IEN-TEST" });
        Assert.That(await wf.IsDurClearedForPrescriptionAsync(rxId), Is.True);

        // 2. Screen for interactions (no interactions — cleared)
        await wf.ScreenPrescriptionForInteractionsAsync(rxId, "TEST DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = $"S-{Guid.NewGuid()}", Name = "TEST" } },
            new List<DrugIngredient>(), "PHARM-001");
        Assert.That(await wf.IsPrescriptionClearedForFillAsync(rxId), Is.True);

        // 3. Verify
        await wf.VerifyPrescriptionWorkflowAsync(rxId, "RPH-001");
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.IsVerified, Is.True);

        // 4. Print label
        await wf.PrintLabelWorkflowAsync(rxId, "RX2026001");
        state = await rx.GetPrescriptionAsync();
        Assert.That(state.IsLabelPrinted, Is.True);

        // 5. Fill (date must allow 75% consumed before refill)
        await wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow.AddDays(-25));
        state = await rx.GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.Not.Null);

        // 6. Refill
        await wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow);
        state = await rx.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(4));
        Assert.That(state.RefillHistory, Has.Count.EqualTo(2));
    }

    // ─── DUR Clearance Check ────────────────────────────────────────────────

    [Test]
    public async Task IsDurClearedForPrescription_ReturnsCorrectValues()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateAndReturnRxId(patientId);

        // Before DUR — not cleared
        Assert.That(await wf.IsDurClearedForPrescriptionAsync(rxId), Is.False);

        // After passing DUR — cleared
        await PerformPassingDur(wf, rxId);
        Assert.That(await wf.IsDurClearedForPrescriptionAsync(rxId), Is.True);
    }
}
