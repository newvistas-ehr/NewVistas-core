// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the three High-priority pharmacy gaps via IPatientWorkflowGrain:
/// 1. Prior Auth / Insurance — formulary coverage and PA requirement enforcement
/// 2. Controlled Substance / DEA — DEA check gate on fill workflow
/// 3. Dispense Constraints — MaxDaysSupply/MaxQuantity enforcement on fill workflow
///
/// All tests exercise cross-grain workflow orchestration.
/// </summary>
[TestFixture]
public class PharmacyHighPriorityGapWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> CreateRxWithDur(
        string patientId, string drugName = "TEST DRUG 10MG", string? drugId = null,
        int daysSupply = 30, int quantity = 30, int refills = 5)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, drugName, drugId,
            "10mg", "ORAL", "QD", null, daysSupply, quantity, refills,
            null, null, null, null, null, null);

        // Perform passing DUR
        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.PerformDurAsync(rxId, drugName, drugId, null,
            "10mg", "ORAL", "QD", daysSupply, quantity, null, null,
            false, null, "PHARM-001");

        return rxId;
    }

    // ═══ PRIOR AUTH / INSURANCE ═════════════════════════════════════════════

    [Test]
    public async Task CheckPriorAuthStatus_NoPlan_ReturnsCleared()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string rxId = await CreateRxWithDur(patientId);

        PriorAuthCoverageResult result = await wf.CheckPriorAuthStatusAsync(rxId);

        Assert.That(result.IsCleared, Is.True);
        Assert.That(result.HasActivePlan, Is.False);
    }

    [Test]
    public async Task CheckPriorAuthStatus_WithPlan_DrugNotInFormulary_ReturnsCleared()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up benefit plan
        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-001", "Blue Cross", "BCBS",
            null, null, null, null, 10, 30, 50, 90, 0);

        string rxId = await CreateRxWithDur(patientId, drugId: $"DRUG-{Guid.NewGuid()}");

        PriorAuthCoverageResult result = await wf.CheckPriorAuthStatusAsync(rxId);

        Assert.That(result.IsCleared, Is.True);
        Assert.That(result.HasActivePlan, Is.True);
    }

    [Test]
    public async Task CheckPriorAuthStatus_DrugNotCovered_ReturnsNotCleared()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string drugId = $"DRUG-NC-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up plan + formulary with NOT_COVERED drug
        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-NC", "Aetna", "Aetna",
            null, null, null, null, 10, 30, 50, 90, 0);

        IFormularyIndexGrain formulary = _cluster.GrainFactory
            .GetGrain<IFormularyIndexGrain>("PBM-FORMULARY:PLAN-NC");
        await formulary.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = drugId, DrugName = "BRAND DRUG", Tier = 0,
            CoverageStatus = "NOT_COVERED", RequiresPriorAuth = false
        });

        string rxId = await CreateRxWithDur(patientId, drugId: drugId);

        PriorAuthCoverageResult result = await wf.CheckPriorAuthStatusAsync(rxId);

        Assert.That(result.IsCleared, Is.False);
        Assert.That(result.CoverageStatus, Is.EqualTo("NOT_COVERED"));
        Assert.That(result.Reasons, Has.Some.Contains("NOT COVERED"));
    }

    [Test]
    public async Task CheckPriorAuthStatus_RequiresPA_NoApprovedPA_ReturnsNotCleared()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string drugId = $"DRUG-PA-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up plan + formulary with PA-required drug
        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-PA", "United", "UHC",
            null, null, null, null, 10, 30, 50, 90, 0);

        IFormularyIndexGrain formulary = _cluster.GrainFactory
            .GetGrain<IFormularyIndexGrain>("PBM-FORMULARY:PLAN-PA");
        await formulary.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = drugId, DrugName = "SPECIALTY DRUG", Tier = 3,
            CoverageStatus = "REQUIRES_PA", RequiresPriorAuth = true
        });

        string rxId = await CreateRxWithDur(patientId, drugId: drugId);

        PriorAuthCoverageResult result = await wf.CheckPriorAuthStatusAsync(rxId);

        Assert.That(result.IsCleared, Is.False);
        Assert.That(result.RequiresPriorAuth, Is.True);
        Assert.That(result.HasApprovedPa, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Prior Authorization"));
    }

    [Test]
    public async Task CheckPriorAuthStatus_RequiresPA_WithApprovedPA_ReturnsCleared()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string drugId = $"DRUG-PA-OK-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up plan + formulary
        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-PA2", "Cigna", "Cigna",
            null, null, null, null, 10, 30, 50, 90, 0);

        IFormularyIndexGrain formulary = _cluster.GrainFactory
            .GetGrain<IFormularyIndexGrain>("PBM-FORMULARY:PLAN-PA2");
        await formulary.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = drugId, DrugName = "SPECIALTY DRUG", Tier = 3,
            CoverageStatus = "REQUIRES_PA", RequiresPriorAuth = true
        });

        // Submit and approve a PA
        string paId = $"PA:{Guid.NewGuid()}";
        IPriorAuthorizationGrain paGrain = _cluster.GrainFactory
            .GetGrain<IPriorAuthorizationGrain>(paId);
        await paGrain.SubmitRequestAsync(patientId, drugId, "SPECIALTY DRUG",
            null, null, null, new List<string>(), "Medical necessity");
        await paGrain.ApproveAsync("REVIEWER-001", "Dr. Smith", "Approved", DateTime.UtcNow.AddYears(1));

        // Add to PA index
        IPriorAuthIndexGrain paIndex = _cluster.GrainFactory
            .GetGrain<IPriorAuthIndexGrain>($"PA-INDEX:{patientId}");
        await paIndex.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = paId, DrugId = drugId,
            DrugName = "SPECIALTY DRUG", Status = "APPROVED"
        });

        string rxId = await CreateRxWithDur(patientId, drugId: drugId);

        PriorAuthCoverageResult result = await wf.CheckPriorAuthStatusAsync(rxId);

        Assert.That(result.IsCleared, Is.True);
        Assert.That(result.RequiresPriorAuth, Is.True);
        Assert.That(result.HasApprovedPa, Is.True);
        Assert.That(result.PaId, Is.EqualTo(paId));
    }

    [Test]
    public async Task FillWorkflow_DrugNotCovered_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string drugId = $"DRUG-BLK-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up plan + not-covered formulary entry
        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-BLK", "Humana", "Humana",
            null, null, null, null, 10, 30, 50, 90, 0);

        IFormularyIndexGrain formulary = _cluster.GrainFactory
            .GetGrain<IFormularyIndexGrain>("PBM-FORMULARY:PLAN-BLK");
        await formulary.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = drugId, DrugName = "EXCLUDED DRUG", Tier = 0,
            CoverageStatus = "NOT_COVERED", RequiresPriorAuth = false
        });

        string rxId = await CreateRxWithDur(patientId, drugId: drugId);
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task FillWorkflow_RequiresPA_NoPA_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string drugId = $"DRUG-NOPA-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-NOPA", "Kaiser", "Kaiser",
            null, null, null, null, 10, 30, 50, 90, 0);

        IFormularyIndexGrain formulary = _cluster.GrainFactory
            .GetGrain<IFormularyIndexGrain>("PBM-FORMULARY:PLAN-NOPA");
        await formulary.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = drugId, DrugName = "PA DRUG", Tier = 3,
            CoverageStatus = "REQUIRES_PA", RequiresPriorAuth = true
        });

        string rxId = await CreateRxWithDur(patientId, drugId: drugId);
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    // ═══ DEA WORKFLOW GATE ═════════════════════════════════════════════════

    [Test]
    public async Task FillWorkflow_ControlledSubstance_DeaNotPassed_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "OXYCODONE 5MG", null,
            "5mg", "ORAL", "Q6H", null, 7, 28, 0, null, null, null, null, null, null);
        await rx.SetDeaCheckResultAsync(true, "II", false, "Invalid DEA number");

        await wf.PerformDurAsync(rxId, "OXYCODONE 5MG", null, null,
            "5mg", "ORAL", "Q6H", 7, 28, null, null, true, "II", "PHARM-001");

        await rx.VerifyAsync("RPH-001");

        // PharmacyGrain should throw for DEA check not passed
        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    // ═══ DISPENSE CONSTRAINTS WORKFLOW ═════════════════════════════════════

    [Test]
    public async Task FillWorkflow_DaysSupplyExceedsMax_Throws()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.CreatePrescriptionAsync(patientId, "DRUG 10MG", null,
            "10mg", "ORAL", "QD", null, 90, 90, 5, null, null, null, null, null, null);
        await rx.SetDispenseConstraintsAsync(null, 30, null, false, false, false);

        await wf.PerformDurAsync(rxId, "DRUG 10MG", null, null,
            "10mg", "ORAL", "QD", 90, 90, null, null, false, null, "PHARM-001");

        await rx.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.FillPrescriptionWorkflowAsync(rxId, DateTime.UtcNow));
    }

    [Test]
    public async Task GetRefillEligibility_WithPaCoverageIssue_ReportsIneligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string drugId = $"DRUG-RE-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up plan with PA-required drug
        IPatientBenefitPlanGrain planGrain = _cluster.GrainFactory
            .GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");
        await planGrain.SetPlanAsync("PLAN-RE", "Anthem", "Anthem",
            null, null, null, null, 10, 30, 50, 90, 0);

        IFormularyIndexGrain formulary = _cluster.GrainFactory
            .GetGrain<IFormularyIndexGrain>("PBM-FORMULARY:PLAN-RE");
        await formulary.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = drugId, DrugName = "PA DRUG", Tier = 3,
            CoverageStatus = "REQUIRES_PA", RequiresPriorAuth = true
        });

        string rxId = await CreateRxWithDur(patientId, drugId: drugId);
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rx.VerifyAsync("RPH-001");
        await rx.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-28));

        RefillEligibilityResult result = await wf.GetRefillEligibilityAsync(rxId, DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.PaCoverageCleared, Is.False);
        Assert.That(result.PaCoverageReasons, Has.Some.Contains("Prior Authorization"));
    }
}
