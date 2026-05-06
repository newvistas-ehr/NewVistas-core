// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Automatic Eligibility Determination — DGENELA.m.
/// </summary>
[TestFixture]
public class AutoEligibilityDeterminationWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IAutoEligibilityDeterminationGrain NewDetermination()
        => _cluster.GrainFactory.GetGrain<IAutoEligibilityDeterminationGrain>($"ELIG-DET:{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── SC 50%+ Exempt ──────────────────────────────────────────────────────

    [Test]
    public async Task SC50Plus_DeterminesExempt()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-001", "Verified", "1", null, false, false, null,
            null, null, null, true, 70, false, false, false, false, "USR-1", "Test");

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Exempt));
        Assert.That(result.CopayRequired, Is.False);
        Assert.That(result.CopayExemptionReason, Is.EqualTo("SC_50_PLUS"));
        Assert.That(result.AssignedPriorityGroup, Is.EqualTo("1"));
    }

    // ─── Former POW Exempt ───────────────────────────────────────────────────

    [Test]
    public async Task FormerPOW_DeterminesExempt()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-002", "Verified", "3", null, false, false, null,
            null, null, null, false, 10, false, false, true, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Exempt));
        Assert.That(result.CopayExemptionReason, Is.EqualTo("FORMER_POW"));
    }

    // ─── Purple Heart Exempt ─────────────────────────────────────────────────

    [Test]
    public async Task PurpleHeart_DeterminesExempt()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-003", "Verified", "3", null, false, false, null,
            null, null, null, false, 0, false, false, false, true, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Exempt));
        Assert.That(result.CopayExemptionReason, Is.EqualTo("PURPLE_HEART"));
    }

    // ─── Catastrophically Disabled ───────────────────────────────────────────

    [Test]
    public async Task CatastrophicallyDisabled_DeterminesExempt()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-004", "Verified", "4", null, false, false, null,
            null, null, null, false, 30, false, true, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Exempt));
        Assert.That(result.CopayExemptionReason, Is.EqualTo("CATASTROPHICALLY_DISABLED"));
    }

    // ─── VA Pension Exempt ───────────────────────────────────────────────────

    [Test]
    public async Task VaPension_DeterminesExempt()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-005", "Verified", "5", null, false, false, null,
            null, null, null, false, 20, true, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Exempt));
        Assert.That(result.CopayExemptionReason, Is.EqualTo("VA_PENSION"));
    }

    // ─── Means Test Based ────────────────────────────────────────────────────

    [Test]
    public async Task MeansTestExempt_DeterminesExempt()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-006", "Verified", "7", null, true, true, "MT-001",
            25000m, 35000m, "EXEMPT", false, 10, false, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Exempt));
        Assert.That(result.CopayExemptionReason, Is.EqualTo("MEANS_TEST_EXEMPT"));
    }

    [Test]
    public async Task MeansTestCopayRequired_BelowGMT_PriorityGroup7()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-007", "Verified", "8", null, true, true, "MT-002",
            30000m, 35000m, "COPAY_REQUIRED", false, 10, false, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.CopayRequired));
        Assert.That(result.CopayRequired, Is.True);
        Assert.That(result.AssignedPriorityGroup, Is.EqualTo("7"));
    }

    [Test]
    public async Task MeansTestCopayRequired_AboveGMT_PriorityGroup8()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-008", "Verified", "8", null, true, true, "MT-003",
            45000m, 35000m, "COPAY_REQUIRED", false, 10, false, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.CopayRequired));
        Assert.That(result.AssignedPriorityGroup, Is.EqualTo("8"));
    }

    // ─── Pending / Not Eligible ──────────────────────────────────────────────

    [Test]
    public async Task MeansTestNotCompleted_Pending()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-009", "Verified", null, null, true, false, null,
            null, null, null, false, 0, false, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.Pending));
    }

    [Test]
    public async Task RejectedEnrollment_NotEligible()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-010", "Rejected", null, null, false, false, null,
            null, null, null, false, 0, false, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.NotEligible));
    }

    [Test]
    public async Task ClosedEnrollment_NotEligible()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-011", "Closed", null, null, false, false, null,
            null, null, null, false, 0, false, false, false, false, null, null);

        Assert.That(result.Result, Is.EqualTo(EligibilityDeterminationResult.NotEligible));
    }

    // ─── Factors Tracking ────────────────────────────────────────────────────

    [Test]
    public async Task Determination_RecordsDeterminationFactors()
    {
        IAutoEligibilityDeterminationGrain det = NewDetermination();
        AutoEligibilityDeterminationState result = await det.DetermineAsync(
            "PAT-AE-012", "Verified", "1", null, false, false, null,
            null, null, null, true, 80, false, false, false, false, null, null);

        Assert.That(result.DeterminationFactors, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(result.DeterminationFactors[0], Does.Contain("Service-connected"));
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_RunAutoEligibility_ReturnsResult()
    {
        string patientId = $"PAT-AE-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        AutoEligibilityDeterminationState result = await wf.RunAutoEligibilityDeterminationAsync("USR-WF", "WF");

        Assert.That(result.PatientId, Is.EqualTo(patientId));
        Assert.That(result.DeterminationFactors, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Workflow_GetAutoEligibility_ReturnsCached()
    {
        string patientId = $"PAT-AE-WF2-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.RunAutoEligibilityDeterminationAsync(null, null);
        AutoEligibilityDeterminationState cached = await wf.GetAutoEligibilityDeterminationAsync();

        Assert.That(cached.PatientId, Is.EqualTo(patientId));
        Assert.That(cached.DeterminationDate.Date, Is.EqualTo(DateTime.UtcNow.Date));
    }
}
