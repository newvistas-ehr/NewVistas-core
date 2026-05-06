// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Social Work grain layer — VistA File #707.
/// Tests the assessment and referral grains directly.
/// </summary>
[TestFixture]
public class SocialWorkGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Assessment grain tests ────────────────────────────────────────────────

    [Test]
    public async Task AssessmentGrain_CreateAndRetrieve_PersistsAllFields()
    {
        string id = $"SW-ASSESSMENT:{Guid.NewGuid()}";
        ISocialWorkAssessmentGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkAssessmentGrain>(id);

        await grain.CreateAsync(
            "PATIENT-001",
            SocialWorkAssessmentType.Psychosocial,
            new DateTime(2024, 6, 1, 10, 0, 0),
            "SW-001", "Jane Smith, LCSW",
            SocialWorkRiskLevel.Moderate,
            housingStatus: "HOUSED",
            employmentStatus: "UNEMPLOYED",
            socialSupport: "ADEQUATE",
            financialStressors: "Rent arrears",
            substanceUseHistory: "Alcohol use disorder, in recovery",
            abuseConcernsIdentified: false,
            safetyPlanInPlace: null,
            anticipatedDischargeDate: new DateTime(2024, 6, 5),
            dischargeDisposition: "HOME",
            dischargePlan: "Patient to return home with family support.",
            dischargeBarriers: new List<string> { "Transportation", "Medication cost" },
            recommendations: "Enroll in VA benefits; connect with community support.",
            notes: "Patient is cooperative and engaged.",
            locationId: "LOC-001",
            locationName: "Main Campus");

        SocialWorkAssessmentState state = await grain.GetAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.AssessmentType, Is.EqualTo(SocialWorkAssessmentType.Psychosocial));
        Assert.That(state.SocialWorkerName, Is.EqualTo("Jane Smith, LCSW"));
        Assert.That(state.RiskLevel, Is.EqualTo(SocialWorkRiskLevel.Moderate));
        Assert.That(state.HousingStatus, Is.EqualTo("HOUSED"));
        Assert.That(state.EmploymentStatus, Is.EqualTo("UNEMPLOYED"));
        Assert.That(state.SocialSupport, Is.EqualTo("ADEQUATE"));
        Assert.That(state.DischargeBarriers, Has.Count.EqualTo(2));
        Assert.That(state.DischargeBarriers, Contains.Item("Transportation"));
        Assert.That(state.Status, Is.EqualTo(SocialWorkAssessmentStatus.Draft));
        Assert.That(state.DischargePlan, Does.Contain("family support"));
    }

    [Test]
    public async Task AssessmentGrain_CompleteLifecycle_DraftToComplete()
    {
        string id = $"SW-ASSESSMENT:{Guid.NewGuid()}";
        ISocialWorkAssessmentGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkAssessmentGrain>(id);

        await grain.CreateAsync(
            "PATIENT-002", SocialWorkAssessmentType.DischargeRisk,
            DateTime.UtcNow, "SW-002", "Dr. Brown, MSW",
            SocialWorkRiskLevel.High,
            null, null, null, null, null, null, null, null, null, null, null,
            "Recommend SNF placement", null, null, null);

        SocialWorkAssessmentState draft = await grain.GetAsync();
        Assert.That(draft.Status, Is.EqualTo(SocialWorkAssessmentStatus.Draft));

        await grain.CompleteAsync(DateTime.UtcNow, "SNF referral submitted", "Patient agreed to SNF.");
        SocialWorkAssessmentState completed = await grain.GetAsync();

        Assert.That(completed.Status, Is.EqualTo(SocialWorkAssessmentStatus.Complete));
        Assert.That(completed.CompletedDate, Is.Not.Null);
        Assert.That(completed.Recommendations, Is.EqualTo("SNF referral submitted"));
        Assert.That(completed.Notes, Is.EqualTo("Patient agreed to SNF."));
    }

    [Test]
    public async Task AssessmentGrain_CloseWithReason_SetsStatusClosed()
    {
        string id = $"SW-ASSESSMENT:{Guid.NewGuid()}";
        ISocialWorkAssessmentGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkAssessmentGrain>(id);

        await grain.CreateAsync(
            "PATIENT-003", SocialWorkAssessmentType.HomelessRisk,
            DateTime.UtcNow, null, null, SocialWorkRiskLevel.High,
            "HOMELESS", null, null, null, null, null, null, null, null, null, null,
            null, null, null, null);

        await grain.CloseAsync("Patient deceased");
        SocialWorkAssessmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(SocialWorkAssessmentStatus.Closed));
        Assert.That(state.ClosedReason, Is.EqualTo("Patient deceased"));
    }

    [Test]
    public async Task AssessmentGrain_UpdateRiskLevel_Persists()
    {
        string id = $"SW-ASSESSMENT:{Guid.NewGuid()}";
        ISocialWorkAssessmentGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkAssessmentGrain>(id);

        await grain.CreateAsync(
            "PATIENT-004", SocialWorkAssessmentType.DomesticViolence,
            DateTime.UtcNow, null, null, SocialWorkRiskLevel.Moderate,
            null, null, null, null, null, true, false, null, null, null, null,
            null, null, null, null);

        await grain.UpdateRiskLevelAsync(SocialWorkRiskLevel.Critical);
        SocialWorkAssessmentState state = await grain.GetAsync();

        Assert.That(state.RiskLevel, Is.EqualTo(SocialWorkRiskLevel.Critical));
        Assert.That(state.AbuseConcernsIdentified, Is.True);
    }

    // ── Assessment index grain tests ──────────────────────────────────────────

    [Test]
    public async Task AssessmentIndexGrain_AddAndFilter_ByStatus()
    {
        string indexKey = $"SW-ASSESSMENT-IDX:PATIENT-{Guid.NewGuid()}";
        ISocialWorkAssessmentIndexGrain index =
            _cluster.GrainFactory.GetGrain<ISocialWorkAssessmentIndexGrain>(indexKey);

        string id1 = $"SW-ASSESSMENT:{Guid.NewGuid()}";
        string id2 = $"SW-ASSESSMENT:{Guid.NewGuid()}";

        await index.AddEntryAsync(new SocialWorkAssessmentIndexEntry
        {
            AssessmentId   = id1,
            PatientId      = "P-001",
            AssessmentType = SocialWorkAssessmentType.Psychosocial,
            AssessmentDate = DateTime.UtcNow,
            RiskLevel      = SocialWorkRiskLevel.Low,
            Status         = SocialWorkAssessmentStatus.Draft,
        });

        await index.AddEntryAsync(new SocialWorkAssessmentIndexEntry
        {
            AssessmentId   = id2,
            PatientId      = "P-001",
            AssessmentType = SocialWorkAssessmentType.DischargeRisk,
            AssessmentDate = DateTime.UtcNow,
            RiskLevel      = SocialWorkRiskLevel.High,
            Status         = SocialWorkAssessmentStatus.Complete,
        });

        List<SocialWorkAssessmentIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));

        List<SocialWorkAssessmentIndexEntry> drafts = await index.GetByStatusAsync(SocialWorkAssessmentStatus.Draft);
        Assert.That(drafts, Has.Count.EqualTo(1));
        Assert.That(drafts[0].AssessmentId, Is.EqualTo(id1));

        await index.UpdateEntryStatusAsync(id1, SocialWorkAssessmentStatus.Complete);
        drafts = await index.GetByStatusAsync(SocialWorkAssessmentStatus.Draft);
        Assert.That(drafts, Has.Count.EqualTo(0));
    }

    // ── Referral grain tests ──────────────────────────────────────────────────

    [Test]
    public async Task ReferralGrain_CreateAndRetrieve_PersistsAllFields()
    {
        string id = $"SW-REFERRAL:{Guid.NewGuid()}";
        ISocialWorkReferralGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkReferralGrain>(id);

        await grain.CreateAsync(
            "PATIENT-010",
            new DateTime(2024, 5, 15),
            referralSource: "Primary Care Team",
            referralReason: "Veteran experiencing homelessness, needs emergency housing.",
            serviceType: SocialWorkReferralServiceType.Housing,
            agencyName: "VA Community Resource Center",
            agencyContact: "Tom Lee",
            agencyPhone: "555-0100",
            socialWorkerId: "SW-002",
            socialWorkerName: "Mary Johnson, LCSW",
            followUpDate: new DateTime(2024, 5, 22),
            assessmentId: null,
            locationId: "LOC-001",
            locationName: "Outpatient Clinic",
            comments: "Veteran has 2 children.");

        SocialWorkReferralState state = await grain.GetAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-010"));
        Assert.That(state.ServiceType, Is.EqualTo(SocialWorkReferralServiceType.Housing));
        Assert.That(state.AgencyName, Is.EqualTo("VA Community Resource Center"));
        Assert.That(state.SocialWorkerName, Is.EqualTo("Mary Johnson, LCSW"));
        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.Pending));
        Assert.That(state.FollowUpDate, Is.Not.Null);
        Assert.That(state.ReferralReason, Does.Contain("homelessness"));
    }

    [Test]
    public async Task ReferralGrain_AcceptTransitionsToActive()
    {
        string id = $"SW-REFERRAL:{Guid.NewGuid()}";
        ISocialWorkReferralGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkReferralGrain>(id);

        await grain.CreateAsync(
            "PATIENT-011", DateTime.UtcNow, null, "Transportation needed post-discharge.",
            SocialWorkReferralServiceType.Transportation,
            "VA Volunteer Driver", null, null, null, null, null, null, null, null, null);

        await grain.AcceptAsync(DateTime.UtcNow);
        SocialWorkReferralState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.Active));
        Assert.That(state.AcceptedDate, Is.Not.Null);
    }

    [Test]
    public async Task ReferralGrain_UpdateStatus_ChangesStatusAndNotes()
    {
        string id = $"SW-REFERRAL:{Guid.NewGuid()}";
        ISocialWorkReferralGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkReferralGrain>(id);

        await grain.CreateAsync(
            "PATIENT-012", DateTime.UtcNow, null, null,
            SocialWorkReferralServiceType.FinancialAssistance,
            "VA Financial Services", null, null, null, null, null, null, null, null, null);

        await grain.UpdateStatusAsync(
            SocialWorkReferralStatus.FollowUpNeeded,
            "Application pending — need to re-contact in 2 weeks.",
            followUpDate: DateTime.UtcNow.AddDays(14));

        SocialWorkReferralState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.FollowUpNeeded));
        Assert.That(state.OutcomeNotes, Does.Contain("Application pending"));
        Assert.That(state.FollowUpDate, Is.Not.Null);
    }

    [Test]
    public async Task ReferralGrain_CloseWithOutcomeNotes_SetsStatusClosed()
    {
        string id = $"SW-REFERRAL:{Guid.NewGuid()}";
        ISocialWorkReferralGrain grain = _cluster.GrainFactory.GetGrain<ISocialWorkReferralGrain>(id);

        await grain.CreateAsync(
            "PATIENT-013", DateTime.UtcNow, null, null,
            SocialWorkReferralServiceType.CommunityMentalHealth,
            "Community Counseling Center", null, null, null, null, null, null, null, null, null);

        await grain.CloseAsync("Patient enrolled in outpatient CBT program.");
        SocialWorkReferralState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.Closed));
        Assert.That(state.ClosedDate, Is.Not.Null);
        Assert.That(state.OutcomeNotes, Does.Contain("CBT program"));
    }
}
