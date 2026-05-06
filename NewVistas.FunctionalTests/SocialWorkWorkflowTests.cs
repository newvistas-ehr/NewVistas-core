// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Social Work — VistA File #707.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class SocialWorkWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Assessment workflows ──────────────────────────────────────────────────

    [Test]
    public async Task CreateAssessment_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string assessmentId = await wf.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.Psychosocial,
            new DateTime(2024, 6, 1),
            "SW-001", "Jane Smith, LCSW",
            SocialWorkRiskLevel.Low,
            housingStatus: "HOUSED",
            employmentStatus: "RETIRED",
            socialSupport: "STRONG",
            financialStressors: null,
            substanceUseHistory: null,
            abuseConcernsIdentified: false,
            safetyPlanInPlace: null,
            anticipatedDischargeDate: null,
            dischargeDisposition: null,
            dischargePlan: null,
            dischargeBarriers: null,
            recommendations: "No immediate needs identified.",
            notes: "Veteran appears stable.",
            locationId: null,
            locationName: null);

        Assert.That(assessmentId, Does.StartWith("SW-ASSESSMENT:"));

        List<SocialWorkAssessmentIndexEntry> all = await wf.GetSocialWorkAssessmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].AssessmentId, Is.EqualTo(assessmentId));
        Assert.That(all[0].AssessmentType, Is.EqualTo(SocialWorkAssessmentType.Psychosocial));
        Assert.That(all[0].RiskLevel, Is.EqualTo(SocialWorkRiskLevel.Low));
        Assert.That(all[0].Status, Is.EqualTo(SocialWorkAssessmentStatus.Draft));
    }

    [Test]
    public async Task GetAssessment_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string assessmentId = await wf.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.DischargeRisk,
            DateTime.UtcNow,
            "SW-002", "Tom Brown, MSW",
            SocialWorkRiskLevel.High,
            null, null, null, null, null, null, null,
            new DateTime(2024, 6, 5), "NURSING FACILITY",
            "SNF placement coordinated.",
            new List<string> { "Insurance authorization" },
            "Contact SNF admissions.",
            "Family is supportive.",
            "LOC-002", "Inpatient Unit");

        SocialWorkAssessmentState state = await wf.GetSocialWorkAssessmentAsync(assessmentId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.AssessmentType, Is.EqualTo(SocialWorkAssessmentType.DischargeRisk));
        Assert.That(state.RiskLevel, Is.EqualTo(SocialWorkRiskLevel.High));
        Assert.That(state.DischargeDisposition, Is.EqualTo("NURSING FACILITY"));
        Assert.That(state.DischargeBarriers, Contains.Item("Insurance authorization"));
        Assert.That(state.SocialWorkerName, Is.EqualTo("Tom Brown, MSW"));
    }

    [Test]
    public async Task CompleteAssessment_UpdatesStatusInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string assessmentId = await wf.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.HomelessRisk, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.High,
            "HOMELESS", null, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        await wf.CompleteSocialWorkAssessmentAsync(
            assessmentId, DateTime.UtcNow,
            "Emergency housing referral submitted.", "Patient transported to shelter.");

        SocialWorkAssessmentState state = await wf.GetSocialWorkAssessmentAsync(assessmentId);
        Assert.That(state.Status, Is.EqualTo(SocialWorkAssessmentStatus.Complete));
        Assert.That(state.CompletedDate, Is.Not.Null);

        List<SocialWorkAssessmentIndexEntry> index = await wf.GetSocialWorkAssessmentsAsync();
        Assert.That(index[0].Status, Is.EqualTo(SocialWorkAssessmentStatus.Complete));
    }

    [Test]
    public async Task CloseAssessment_SetsStatusClosed()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string assessmentId = await wf.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.Bereavement, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.Moderate,
            null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        await wf.CloseSocialWorkAssessmentAsync(assessmentId, "Patient transferred to another facility");

        SocialWorkAssessmentState state = await wf.GetSocialWorkAssessmentAsync(assessmentId);
        Assert.That(state.Status, Is.EqualTo(SocialWorkAssessmentStatus.Closed));
        Assert.That(state.ClosedReason, Does.Contain("transferred"));
    }

    [Test]
    public async Task GetAssessmentsByType_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.SubstanceUse, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.Moderate,
            null, null, null, null, "EtOH use x 10 years", null, null,
            null, null, null, null, null, null, null, null);

        await wf.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.Psychosocial, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.Low,
            "HOUSED", null, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        List<SocialWorkAssessmentIndexEntry> suOnly =
            await wf.GetSocialWorkAssessmentsByTypeAsync(SocialWorkAssessmentType.SubstanceUse);
        Assert.That(suOnly, Has.Count.EqualTo(1));
        Assert.That(suOnly[0].AssessmentType, Is.EqualTo(SocialWorkAssessmentType.SubstanceUse));
    }

    // ── Referral workflows ────────────────────────────────────────────────────

    [Test]
    public async Task CreateReferral_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string referralId = await wf.CreateSocialWorkReferralAsync(
            referralDate: DateTime.UtcNow,
            referralSource: "Primary Care",
            referralReason: "Food insecurity — qualifies for SNAP",
            serviceType: SocialWorkReferralServiceType.Food,
            agencyName: "VA Food Pantry",
            agencyContact: "Sue Chen",
            agencyPhone: "555-0200",
            socialWorkerId: "SW-003",
            socialWorkerName: "Alice Park, LCSW",
            followUpDate: DateTime.UtcNow.AddDays(7),
            assessmentId: null,
            locationId: null,
            locationName: null,
            comments: null);

        Assert.That(referralId, Does.StartWith("SW-REFERRAL:"));

        List<SocialWorkReferralIndexEntry> all = await wf.GetSocialWorkReferralsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ReferralId, Is.EqualTo(referralId));
        Assert.That(all[0].ServiceType, Is.EqualTo(SocialWorkReferralServiceType.Food));
        Assert.That(all[0].Status, Is.EqualTo(SocialWorkReferralStatus.Pending));
        Assert.That(all[0].SocialWorkerName, Is.EqualTo("Alice Park, LCSW"));
    }

    [Test]
    public async Task GetReferral_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string referralId = await wf.CreateSocialWorkReferralAsync(
            DateTime.UtcNow, "ER team",
            "Veteran homeless after discharge", SocialWorkReferralServiceType.Housing,
            "Veteran Housing Hub", "Mike Davis", "555-0300",
            "SW-004", "Bob White, MSW",
            null, null, null, null, "2 children in tow");

        SocialWorkReferralState state = await wf.GetSocialWorkReferralAsync(referralId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ServiceType, Is.EqualTo(SocialWorkReferralServiceType.Housing));
        Assert.That(state.AgencyName, Is.EqualTo("Veteran Housing Hub"));
        Assert.That(state.ReferralSource, Is.EqualTo("ER team"));
        Assert.That(state.Comments, Does.Contain("children"));
    }

    [Test]
    public async Task UpdateReferralStatus_TransitionsCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string referralId = await wf.CreateSocialWorkReferralAsync(
            DateTime.UtcNow, null, null,
            SocialWorkReferralServiceType.Transportation,
            "VA Shuttle", null, null, null, null, null, null, null, null, null);

        // Pending → Active
        await wf.UpdateSocialWorkReferralStatusAsync(
            referralId, SocialWorkReferralStatus.Active, null, null);

        SocialWorkReferralState state = await wf.GetSocialWorkReferralAsync(referralId);
        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.Active));

        // Active → FollowUpNeeded
        await wf.UpdateSocialWorkReferralStatusAsync(
            referralId, SocialWorkReferralStatus.FollowUpNeeded,
            "Shuttle schedule unavailable; re-check Friday.", DateTime.UtcNow.AddDays(5));

        state = await wf.GetSocialWorkReferralAsync(referralId);
        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.FollowUpNeeded));
        Assert.That(state.OutcomeNotes, Does.Contain("Shuttle schedule"));
    }

    [Test]
    public async Task CloseReferral_SetsStatusClosed()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string referralId = await wf.CreateSocialWorkReferralAsync(
            DateTime.UtcNow, null, "Vocational rehab assessment needed",
            SocialWorkReferralServiceType.VocationalRehabilitation,
            "VA Voc Rehab", null, null, null, null, null, null, null, null, null);

        await wf.CloseSocialWorkReferralAsync(referralId, "Veteran enrolled in VR&E program.");

        SocialWorkReferralState state = await wf.GetSocialWorkReferralAsync(referralId);
        Assert.That(state.Status, Is.EqualTo(SocialWorkReferralStatus.Closed));
        Assert.That(state.OutcomeNotes, Does.Contain("VR&E program"));

        // Index should reflect closed status
        List<SocialWorkReferralIndexEntry> index = await wf.GetSocialWorkReferralsAsync();
        Assert.That(index[0].Status, Is.EqualTo(SocialWorkReferralStatus.Closed));
    }

    [Test]
    public async Task GetReferralsByStatus_FiltersPendingVsClosed()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string r1 = await wf.CreateSocialWorkReferralAsync(
            DateTime.UtcNow, null, null,
            SocialWorkReferralServiceType.LegalServices,
            "VA Legal Aid", null, null, null, null, null, null, null, null, null);

        string r2 = await wf.CreateSocialWorkReferralAsync(
            DateTime.UtcNow, null, null,
            SocialWorkReferralServiceType.HomeHealth,
            "Community Home Health", null, null, null, null, null, null, null, null, null);

        await wf.CloseSocialWorkReferralAsync(r1, "Resolved");

        List<SocialWorkReferralIndexEntry> pending =
            await wf.GetSocialWorkReferralsByStatusAsync(SocialWorkReferralStatus.Pending);
        List<SocialWorkReferralIndexEntry> closed =
            await wf.GetSocialWorkReferralsByStatusAsync(SocialWorkReferralStatus.Closed);

        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].ReferralId, Is.EqualTo(r2));
        Assert.That(closed, Has.Count.EqualTo(1));
        Assert.That(closed[0].ReferralId, Is.EqualTo(r1));
    }

    [Test]
    public async Task MultipleAssessmentsAndReferrals_IndependentPatients()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.Psychosocial, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.Low,
            null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        await wf2.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.CaregiverStress, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.Moderate,
            null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        await wf2.CreateSocialWorkAssessmentAsync(
            SocialWorkAssessmentType.SubstanceUse, DateTime.UtcNow,
            null, null, SocialWorkRiskLevel.High,
            null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        List<SocialWorkAssessmentIndexEntry> p1Assessments = await wf1.GetSocialWorkAssessmentsAsync();
        List<SocialWorkAssessmentIndexEntry> p2Assessments = await wf2.GetSocialWorkAssessmentsAsync();

        Assert.That(p1Assessments, Has.Count.EqualTo(1));
        Assert.That(p2Assessments, Has.Count.EqualTo(2));
    }
}
