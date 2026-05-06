// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Insurance Eligibility Verification — EDI 270/271.
/// Covers inquiry creation, submission, response recording, index management,
/// and payer configuration.
/// MUMPS: IBCNEDE*.m, IBCNEUT.m, IBCNEDE1.m
/// </summary>
[TestFixture]
public class EligibilityVerificationWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IEligibilityInquiryGrain NewInquiry()
        => _cluster.GrainFactory.GetGrain<IEligibilityInquiryGrain>($"ELIG-270:{Guid.NewGuid()}");

    private IEligibilityVerificationIndexGrain GetIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IEligibilityVerificationIndexGrain>($"ELIG-IDX:{patientId}");

    private IPayerConfigGrain GetPayerConfig(string payerId)
        => _cluster.GrainFactory.GetGrain<IPayerConfigGrain>($"PAYER-CFG:{payerId}");

    private IPayerConfigIndexGrain GetPayerIndex()
        => _cluster.GrainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Inquiry Creation ────────────────────────────────────────────────────

    [Test]
    public async Task CreateInquiry_SetsDraftStatus()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-001", null, null,
            "PAYER-MEDICARE", "Medicare Part A/B",
            "1EG4-TE5-MK72", "PATIENT,TEST", "SELF",
            new DateTime(1950, 1, 15),
            new List<string> { "30" }, DateTime.UtcNow,
            "USR-001", "Test User", null);

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EligibilityInquiryStatus.Draft));
    }

    [Test]
    public async Task CreateInquiry_StoresPatientAndPayer()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-002", "PLAN-001", "POLICY-001",
            "PAYER-BCBS", "Blue Cross Blue Shield",
            "XYZ123", "SMITH,JOHN", "SELF",
            null, new List<string> { "30", "1" }, DateTime.UtcNow,
            "USR-002", "Dr. Smith", "Routine verification");

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-ELIG-002"));
        Assert.That(state.PayerId, Is.EqualTo("PAYER-BCBS"));
        Assert.That(state.SubscriberId, Is.EqualTo("XYZ123"));
        Assert.That(state.InsurancePlanId, Is.EqualTo("PLAN-001"));
    }

    // ─── Inquiry Submission ──────────────────────────────────────────────────

    [Test]
    public async Task SubmitInquiry_SetsSubmittedStatus()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-003", null, null,
            "PAYER-AETNA", "Aetna",
            "A999", null, "SELF",
            null, new List<string> { "30" }, DateTime.UtcNow,
            "USR-001", "Test User", null);

        await inquiry.SubmitAsync("TN-20260328-TEST001");

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EligibilityInquiryStatus.Submitted));
        Assert.That(state.TraceNumber, Is.EqualTo("TN-20260328-TEST001"));
        Assert.That(state.SubmittedDate, Is.Not.Null);
    }

    // ─── Eligible Response (271) ─────────────────────────────────────────────

    [Test]
    public async Task RecordEligibleResponse_SetsEligibleStatus()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-004", null, null,
            "PAYER-MEDICARE", "Medicare Part A/B",
            "MBI-001", null, "SELF",
            null, new List<string> { "30" }, DateTime.UtcNow,
            "USR-001", "Test User", null);
        await inquiry.SubmitAsync("TN-001");

        await inquiry.RecordEligibleResponseAsync(
            CoverageLevel.ActiveCoverage,
            "Medicare Standard", DateTime.UtcNow.AddYears(-1), null,
            "GRP-MED-001",
            new List<EligibilityBenefitDetail>
            {
                new() { BenefitType = "DEDUCTIBLE", Amount = 226m, TimePeriod = "CALENDAR_YEAR" }
            },
            "Member is eligible.");

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EligibilityInquiryStatus.Eligible));
        Assert.That(state.CoverageLevel, Is.EqualTo(CoverageLevel.ActiveCoverage));
        Assert.That(state.ResponsePlanName, Is.EqualTo("Medicare Standard"));
        Assert.That(state.BenefitDetails, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RecordEligibleResponse_StoresBenefitDetails()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-005", null, null,
            "PAYER-BCBS", "BCBS",
            "BCBS-001", null, "SELF",
            null, new List<string> { "30" }, DateTime.UtcNow,
            "USR-001", "Test User", null);
        await inquiry.SubmitAsync("TN-002");

        List<EligibilityBenefitDetail> benefits = new()
        {
            new() { BenefitType = "DEDUCTIBLE", Amount = 500m, TimePeriod = "CALENDAR_YEAR", NetworkIndicator = "IN" },
            new() { BenefitType = "COPAY", Amount = 30m, TimePeriod = "VISIT", ServiceTypeCode = "1" },
            new() { BenefitType = "COINSURANCE", Percentage = 0.20m, TimePeriod = "CALENDAR_YEAR" },
        };

        await inquiry.RecordEligibleResponseAsync(
            CoverageLevel.ActiveCoverage, "BCBS PPO", null, null, "GRP-BCBS", benefits, null);

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.BenefitDetails, Has.Count.EqualTo(3));
        Assert.That(state.BenefitDetails[0].BenefitType, Is.EqualTo("DEDUCTIBLE"));
        Assert.That(state.BenefitDetails[0].Amount, Is.EqualTo(500m));
    }

    // ─── Ineligible Response ─────────────────────────────────────────────────

    [Test]
    public async Task RecordIneligibleResponse_SetsIneligibleStatus()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-006", null, null,
            "PAYER-CIGNA", "Cigna",
            "CIG-001", null, "SELF",
            null, new List<string> { "30" }, DateTime.UtcNow,
            "USR-001", "Test User", null);
        await inquiry.SubmitAsync("TN-003");

        await inquiry.RecordIneligibleResponseAsync(
            CoverageLevel.Terminated,
            new List<string> { "72", "73" },
            "Coverage terminated effective 2025-12-31.");

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EligibilityInquiryStatus.Ineligible));
        Assert.That(state.CoverageLevel, Is.EqualTo(CoverageLevel.Terminated));
        Assert.That(state.RejectReasonCodes, Has.Count.EqualTo(2));
    }

    // ─── Error Response ──────────────────────────────────────────────────────

    [Test]
    public async Task RecordErrorResponse_SetsErrorStatus()
    {
        IEligibilityInquiryGrain inquiry = NewInquiry();
        await inquiry.CreateAsync(
            "PAT-ELIG-007", null, null,
            "PAYER-UNITED", "UnitedHealthcare",
            "UHC-001", null, "SELF",
            null, new List<string> { "30" }, DateTime.UtcNow,
            "USR-001", "Test User", null);
        await inquiry.SubmitAsync("TN-004");

        await inquiry.RecordErrorResponseAsync(
            new List<string> { "42", "79" },
            "Unable to process request — subscriber not found.");

        EligibilityInquiryState state = await inquiry.GetAsync();
        Assert.That(state.Status, Is.EqualTo(EligibilityInquiryStatus.Error));
        Assert.That(state.PayerMessage, Does.Contain("subscriber not found"));
    }

    // ─── Verification Index ──────────────────────────────────────────────────

    [Test]
    public async Task VerificationIndex_AddOrUpdate_AppearsInGetAll()
    {
        string patientId = $"PAT-ELIG-IDX-{Guid.NewGuid()}";
        IEligibilityVerificationIndexGrain index = GetIndex(patientId);

        string inquiryId = Guid.NewGuid().ToString();
        await index.AddOrUpdateAsync(new EligibilityVerificationIndexEntry
        {
            InquiryId = inquiryId,
            PayerName = "Medicare Part A/B",
            SubscriberId = "MBI-TEST",
            ServiceDate = DateTime.UtcNow,
            Status = EligibilityInquiryStatus.Eligible,
            CoverageLevel = CoverageLevel.ActiveCoverage,
            SubmittedDate = DateTime.UtcNow,
            ResponseDate = DateTime.UtcNow,
        });

        List<EligibilityVerificationIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Any(e => e.InquiryId == inquiryId), Is.True);
    }

    [Test]
    public async Task VerificationIndex_GetEligible_FiltersCorrectly()
    {
        string patientId = $"PAT-ELIG-FILT-{Guid.NewGuid()}";
        IEligibilityVerificationIndexGrain index = GetIndex(patientId);

        await index.AddOrUpdateAsync(new EligibilityVerificationIndexEntry
        {
            InquiryId = "INQ-E1", PayerName = "Medicare", SubscriberId = "M1",
            ServiceDate = DateTime.UtcNow, Status = EligibilityInquiryStatus.Eligible,
            CoverageLevel = CoverageLevel.ActiveCoverage,
        });
        await index.AddOrUpdateAsync(new EligibilityVerificationIndexEntry
        {
            InquiryId = "INQ-I1", PayerName = "Cigna", SubscriberId = "C1",
            ServiceDate = DateTime.UtcNow, Status = EligibilityInquiryStatus.Ineligible,
            CoverageLevel = CoverageLevel.Terminated,
        });

        List<EligibilityVerificationIndexEntry> eligible = await index.GetEligibleAsync();
        Assert.That(eligible, Has.Count.EqualTo(1));
        Assert.That(eligible[0].PayerName, Is.EqualTo("Medicare"));
    }

    [Test]
    public async Task VerificationIndex_GetLatestForPayer_ReturnsCorrectEntry()
    {
        string patientId = $"PAT-ELIG-LAT-{Guid.NewGuid()}";
        IEligibilityVerificationIndexGrain index = GetIndex(patientId);

        await index.AddOrUpdateAsync(new EligibilityVerificationIndexEntry
        {
            InquiryId = "INQ-MED-OLD", PayerName = "Medicare Part A/B", SubscriberId = "M1",
            ServiceDate = DateTime.UtcNow.AddDays(-30), Status = EligibilityInquiryStatus.Eligible,
            CoverageLevel = CoverageLevel.ActiveCoverage,
        });
        await index.AddOrUpdateAsync(new EligibilityVerificationIndexEntry
        {
            InquiryId = "INQ-MED-NEW", PayerName = "Medicare Part A/B", SubscriberId = "M1",
            ServiceDate = DateTime.UtcNow, Status = EligibilityInquiryStatus.Eligible,
            CoverageLevel = CoverageLevel.ActiveCoverage,
        });

        EligibilityVerificationIndexEntry? latest = await index.GetLatestForPayerAsync("Medicare Part A/B");
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.InquiryId, Is.EqualTo("INQ-MED-NEW"));
    }

    // ─── Payer Configuration ─────────────────────────────────────────────────

    [Test]
    public async Task PayerConfig_Create_StoresConfiguration()
    {
        string payerId = $"PAYER-TEST-{Guid.NewGuid()}";
        IPayerConfigGrain config = GetPayerConfig(payerId);

        await config.CreateAsync(
            "Test Insurance Co.", true,
            "ISA-RECV-001", "ZZ",
            "https://eligibility.test.com/270", "1-800-555-1234",
            new List<string> { "30", "1", "47" }, "Test payer");

        PayerConfigState state = await config.GetAsync();
        Assert.That(state.PayerName, Is.EqualTo("Test Insurance Co."));
        Assert.That(state.SupportsRealTimeEligibility, Is.True);
        Assert.That(state.IsActive, Is.True);
    }

    [Test]
    public async Task PayerConfig_Deactivate_SetsInactive()
    {
        string payerId = $"PAYER-DEACT-{Guid.NewGuid()}";
        IPayerConfigGrain config = GetPayerConfig(payerId);

        await config.CreateAsync("Deactivation Test", true, null, null, null, null, null, null);
        await config.DeactivateAsync();

        PayerConfigState state = await config.GetAsync();
        Assert.That(state.IsActive, Is.False);
    }

    [Test]
    public async Task PayerConfigIndex_SelfSeeds_DemoPayers()
    {
        IPayerConfigIndexGrain index = GetPayerIndex();
        List<PayerConfigIndexEntry> payers = await index.GetAllAsync();

        Assert.That(payers.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(payers.Any(p => p.PayerId == "PAYER-MEDICARE"), Is.True);
        Assert.That(payers.Any(p => p.PayerId == "PAYER-BCBS"), Is.True);
    }

    [Test]
    public async Task PayerConfigIndex_Search_FiltersByName()
    {
        IPayerConfigIndexGrain index = GetPayerIndex();

        List<PayerConfigIndexEntry> results = await index.SearchAsync("Medicare");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.All(p => p.PayerName.Contains("Medicare", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_SubmitEligibilityInquiry_ReturnsInquiryId()
    {
        string patientId = $"PAT-ELIG-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string inquiryId = await wf.SubmitEligibilityInquiryAsync(
            null, null, "PAYER-MEDICARE", "Medicare Part A/B",
            "MBI-WF-001", "WORKFLOW,TEST", "SELF", null,
            new List<string> { "30" }, DateTime.UtcNow,
            "USR-WF", "Workflow User", "Workflow test");

        Assert.That(inquiryId, Is.Not.Null.And.Not.Empty);

        EligibilityInquiryState state = await wf.GetEligibilityInquiryAsync(inquiryId);
        Assert.That(state.Status, Is.EqualTo(EligibilityInquiryStatus.Eligible));
        Assert.That(state.CoverageLevel, Is.EqualTo(CoverageLevel.ActiveCoverage));
    }

    [Test]
    public async Task Workflow_SubmitInquiry_UpdatesPatientIndex()
    {
        string patientId = $"PAT-ELIG-IDX-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.SubmitEligibilityInquiryAsync(
            null, null, "PAYER-BCBS", "Blue Cross Blue Shield",
            "BCBS-WF-001", null, "SELF", null,
            new List<string> { "30" }, DateTime.UtcNow,
            "USR-WF", "Workflow User", null);

        List<EligibilityVerificationIndexEntry> history = await wf.GetEligibilityVerificationHistoryAsync();
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].PayerName, Is.EqualTo("Blue Cross Blue Shield"));
        Assert.That(history[0].Status, Is.EqualTo(EligibilityInquiryStatus.Eligible));
    }
}
