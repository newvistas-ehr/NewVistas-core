// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for consult depth methods through IPatientWorkflowGrain.
/// Covers tracking comments, accept/schedule details, clinical history,
/// follow-up recommendations, and interfacility marking — all via the
/// workflow orchestration layer rather than direct grain calls.
/// </summary>
[TestFixture]
public class ConsultDepthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> RequestConsult(IPatientWorkflowGrain wf)
        => await wf.RequestConsultAsync(
            "Cardiology", "SVC-001", "Primary Care", "SVC-002",
            "ROUTINE", "PROV-001", "Dr. Referring", null, null,
            "Evaluate chest pain", "Atypical chest pain", null, "LOC-001", "Clinic A");

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddConsultTrackingComment_PersistsComment()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.AddConsultTrackingCommentAsync(consultId, "PROV-002", "Dr. Notes", "Patient contacted", "SCHEDULING");

        List<ConsultTrackingComment> comments = await wf.GetConsultTrackingCommentsAsync(consultId);
        Assert.That(comments, Has.Count.EqualTo(1));
        Assert.That(comments[0].CommentText, Is.EqualTo("Patient contacted"));
        Assert.That(comments[0].AuthorId, Is.EqualTo("PROV-002"));
        Assert.That(comments[0].AuthorName, Is.EqualTo("Dr. Notes"));
        Assert.That(comments[0].ActionTaken, Is.EqualTo("SCHEDULING"));
    }

    [Test]
    public async Task AcceptConsultWithDetails_SetsAcceptedFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.AcceptConsultWithDetailsAsync(consultId, "PROV-003", "Dr. Acceptor");

        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.AcceptedById, Is.EqualTo("PROV-003"));
        Assert.That(state.AcceptedByName, Is.EqualTo("Dr. Acceptor"));
    }

    [Test]
    public async Task ScheduleConsultWithDetails_SetsScheduleFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.AcceptConsultWithDetailsAsync(consultId, "PROV-003", "Dr. Acceptor");

        DateTime scheduledDate = DateTime.UtcNow.AddDays(7);
        await wf.ScheduleConsultWithDetailsAsync(consultId, scheduledDate, "CLINIC-001", "Cardiology Clinic");

        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(state.ScheduledDateTime, Is.Not.Null);
        Assert.That(state.ScheduledClinicId, Is.EqualTo("CLINIC-001"));
        Assert.That(state.ScheduledClinicName, Is.EqualTo("Cardiology Clinic"));
    }

    [Test]
    public async Task SetConsultClinicalHistory_PersistsHistory()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.SetConsultClinicalHistoryAsync(consultId, "Patient has 3-year history of intermittent chest pain.");

        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.ClinicalHistory, Is.EqualTo("Patient has 3-year history of intermittent chest pain."));
    }

    [Test]
    public async Task SetConsultFollowUpRecommendation_Persists()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.CompleteConsultAsync(consultId, "Evaluation complete", "PROV-003", "Dr. Acceptor");

        await wf.SetConsultFollowUpRecommendationAsync(consultId, "Follow up in 3 months with repeat stress test.");

        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.FollowUpRecommendation, Is.EqualTo("Follow up in 3 months with repeat stress test."));
    }

    [Test]
    public async Task MarkConsultInterfacility_SetsExternalFacility()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.MarkConsultInterfacilityAsync(consultId, "FAC-EXT-001", "Walter Reed Medical Center");

        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.IsInterfacility, Is.True);
        Assert.That(state.ExternalFacilityId, Is.EqualTo("FAC-EXT-001"));
        Assert.That(state.ExternalFacilityName, Is.EqualTo("Walter Reed Medical Center"));
    }

    [Test]
    public async Task GetConsultTrackingComments_MultipleComments()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);
        string consultId = await RequestConsult(wf);

        await wf.AddConsultTrackingCommentAsync(consultId, "PROV-001", "Dr. First", "Initial review", "REVIEW");
        await wf.AddConsultTrackingCommentAsync(consultId, "PROV-002", "Dr. Second", "Patient called", "SCHEDULING");
        await wf.AddConsultTrackingCommentAsync(consultId, "PROV-003", "Dr. Third", "Appointment confirmed", "CONFIRMATION");

        List<ConsultTrackingComment> comments = await wf.GetConsultTrackingCommentsAsync(consultId);
        Assert.That(comments, Has.Count.EqualTo(3));
    }
}
