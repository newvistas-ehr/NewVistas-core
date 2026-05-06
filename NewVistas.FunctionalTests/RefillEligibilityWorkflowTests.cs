// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Refill Eligibility Validation via IPatientWorkflowGrain.
/// Tests the cross-grain eligibility check that combines PharmacyGrain rules
/// with DUR and interaction screening gates.
///
/// VistA reference: PSO refill date calculation, 21 CFR 1306.12.
/// </summary>
[TestFixture]
public class RefillEligibilityWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> CreateVerifiedFilledRx(
        string patientId, int refills = 5, int daysSupply = 30, int daysAgoFilled = 15)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "TEST DRUG 10MG", null,
            "10mg", "ORAL", "QD", null, daysSupply, 30, refills, null, null, null, null, null, null);
        await rx.VerifyAsync("RPH-001");
        await rx.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-daysAgoFilled));
        return rxId;
    }

    private async Task PerformPassingDur(IPatientWorkflowGrain wf, string rxId)
    {
        await wf.PerformDurAsync(rxId, "TEST DRUG 10MG", null, null,
            "10mg", "ORAL", "QD", 30, 30, null, null,
            false, null, "PHARM-001",
            ingredientIens: new List<string> { "IEN-TEST" });
    }

    // ─── Workflow Eligibility Check (Cross-Grain Gates) ─────────────────────

    [Test]
    public async Task GetRefillEligibility_WithDurAndEligible_ReturnsFullyEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, daysAgoFilled: 25);

        await PerformPassingDur(wf, rxId);

        RefillEligibilityResult result = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.DurCleared, Is.True);
        Assert.That(result.InteractionCleared, Is.True);
        Assert.That(result.Reasons, Is.Empty);
    }

    [Test]
    public async Task GetRefillEligibility_WithoutDur_ReturnsNotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, daysAgoFilled: 25);

        // No DUR performed
        RefillEligibilityResult result = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.DurCleared, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("DUR"));
    }

    [Test]
    public async Task GetRefillEligibility_WithBlockedInteraction_ReturnsNotEligible()
    {
        string ien1 = $"IEN-RE-{Guid.NewGuid():N}";
        string ien2 = $"IEN-RE-{Guid.NewGuid():N}";
        IDrugInteractionDatasetGrain ds = _cluster.GrainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");
        await ds.LoadInteractionsAsync(new List<DrugInteractionPair>
        {
            new DrugInteractionPair
            {
                IngredientIen1 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien1 : ien2,
                IngredientName1 = "DRUG A",
                IngredientIen2 = string.Compare(ien1, ien2, StringComparison.Ordinal) <= 0 ? ien2 : ien1,
                IngredientName2 = "DRUG B",
                Severity = InteractionSeverity.Significant,
                Description = "Refill eligibility test"
            }
        });

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, daysAgoFilled: 25);

        await PerformPassingDur(wf, rxId);

        // Screen with blocking interaction
        await wf.ScreenPrescriptionForInteractionsAsync(rxId, "TEST DRUG",
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien1, Name = "DRUG A" } },
            new List<DrugIngredient> { new DrugIngredient { IngredientIen = ien2, Name = "DRUG B" } },
            "PHARM-001");

        RefillEligibilityResult result = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.InteractionCleared, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("interaction"));
    }

    [Test]
    public async Task GetRefillEligibility_TooEarly_ReturnsNotEligibleWithDetails()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, daysSupply: 30, daysAgoFilled: 5);

        await PerformPassingDur(wf, rxId);

        RefillEligibilityResult result = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.IsTooEarly, Is.True);
        Assert.That(result.PercentConsumed, Is.LessThan(75));
        Assert.That(result.EarliestRefillDate, Is.Not.Null);
        Assert.That(result.DurCleared, Is.True);
    }

    [Test]
    public async Task GetRefillEligibility_NoRefillsRemaining_ReturnsNotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, refills: 0, daysAgoFilled: 25);

        await PerformPassingDur(wf, rxId);

        RefillEligibilityResult result = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.RefillsRemaining, Is.EqualTo(0));
        Assert.That(result.Reasons, Has.Some.Contains("No refills remaining"));
    }

    // ─── Refill Workflow with Enhanced Guards ───────────────────────────────

    [Test]
    public async Task RefillWorkflow_TooEarly_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, daysSupply: 30, daysAgoFilled: 5);

        await PerformPassingDur(wf, rxId);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task RefillWorkflow_After75PercentConsumed_Succeeds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, daysSupply: 30, daysAgoFilled: 23);

        await PerformPassingDur(wf, rxId);

        Assert.DoesNotThrowAsync(() => wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));

        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        PharmacyState state = await rx.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(4));
    }

    [Test]
    public async Task RefillWorkflow_DeaScheduleII_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "OXYCODONE 5MG", null,
            "5mg", "ORAL", "Q6H", null, 7, 28, 0, null, null, null, null, null, null);
        await rx.SetDeaCheckResultAsync(true, "II", true, null);
        await rx.VerifyAsync("RPH-001");
        await rx.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-10));

        await PerformPassingDur(wf, rxId);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    // ─── Eligibility After Refill ───────────────────────────────────────────

    [Test]
    public async Task GetRefillEligibility_AfterRefill_UpdatesRemainingCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateVerifiedFilledRx(patientId, refills: 3, daysSupply: 30, daysAgoFilled: 25);

        await PerformPassingDur(wf, rxId);

        RefillEligibilityResult before = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);
        Assert.That(before.RefillsRemaining, Is.EqualTo(3));

        await wf.RefillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow);

        RefillEligibilityResult after = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);
        Assert.That(after.RefillsRemaining, Is.EqualTo(2));
        Assert.That(after.RefillsDispensed, Is.EqualTo(1));
    }

    // ─── Patient Isolation ──────────────────────────────────────────────────

    [Test]
    public async Task GetRefillEligibility_DifferentPrescriptions_Independent()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rx1 = await CreateVerifiedFilledRx(patientId, refills: 5, daysAgoFilled: 25);
        string rx2 = await CreateVerifiedFilledRx(patientId, refills: 0, daysAgoFilled: 25);

        await PerformPassingDur(wf, rx1);
        await PerformPassingDur(wf, rx2);

        RefillEligibilityResult r1 = await wf.GetRefillEligibilityAsync(rx1, DateTime.UtcNow);
        RefillEligibilityResult r2 = await wf.GetRefillEligibilityAsync(rx2, DateTime.UtcNow);

        Assert.That(r1.IsEligible, Is.True);
        Assert.That(r2.IsEligible, Is.False);
    }
}
