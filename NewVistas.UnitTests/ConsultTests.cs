// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class ConsultGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Grain-level tests ───────────────────────────────────────────────

    [Test]
    public async Task ConsultGrain_CanRequestConsult()
    {
        var consultId = $"CONSULT-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

        await grain.RequestConsultAsync(
            "PATIENT-001", "Cardiology", "SVC-CARDIO",
            "Primary Care", "SVC-PC", "ROUTINE",
            "PROV-001", "Dr. Smith",
            "PROV-002", "Dr. Jones",
            "Chest pain on exertion, r/o CAD",
            "Chest pain, unspecified",
            null, "LOC-001", "Primary Care Clinic");

        var state = await grain.GetConsultAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.ToService, Is.EqualTo("Cardiology"));
        Assert.That(state.FromService, Is.EqualTo("Primary Care"));
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(state.RequestingProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.AttentionProviderName, Is.EqualTo("Dr. Jones"));
        Assert.That(state.ReasonForRequest, Does.Contain("Chest pain"));
    }

    [Test]
    public async Task ConsultGrain_FullLifecycle_PendingToComplete()
    {
        var consultId = $"CONSULT-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

        await grain.RequestConsultAsync(
            "PATIENT-001", "Orthopedics", null,
            "ER", null, "URGENT",
            "PROV-001", "Dr. Smith",
            null, null,
            "Acute knee injury", "Knee pain",
            null, null, null);

        // Accept
        await grain.AcceptAsync();
        var state = await grain.GetConsultAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));

        // Schedule
        await grain.ScheduleAsync();
        state = await grain.GetConsultAsync();
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));

        // Complete
        await grain.CompleteAsync(DateTime.UtcNow, "TIU-RESULT-001");
        state = await grain.GetConsultAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.CompletedDateTime, Is.Not.Null);
        Assert.That(state.ResultDocumentId, Is.EqualTo("TIU-RESULT-001"));
    }

    [Test]
    public async Task ConsultGrain_CanCancel()
    {
        var consultId = $"CONSULT-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

        await grain.RequestConsultAsync(
            "PATIENT-001", "Dermatology", null,
            null, null, "ROUTINE",
            null, null, null, null,
            "Skin rash", null, null, null, null);

        await grain.CancelAsync("Patient declined");
        var state = await grain.GetConsultAsync();

        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Is.EqualTo("Patient declined"));
    }

    [Test]
    public async Task ConsultGrain_CanDiscontinue()
    {
        var consultId = $"CONSULT-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

        await grain.RequestConsultAsync(
            "PATIENT-001", "Neurology", null,
            null, null, "ROUTINE",
            null, null, null, null,
            null, null, null, null, null);

        await grain.AcceptAsync();
        await grain.DiscontinueAsync("Service no longer needed");
        var state = await grain.GetConsultAsync();

        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.Comments, Is.EqualTo("Service no longer needed"));
    }

    // ─── Workflow-level tests ────────────────────────────────────────────

    [Test]
    public async Task WorkflowGrain_CanRequestAndListConsults()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var id1 = await workflow.RequestConsultAsync(
            "Cardiology", null, "Primary Care", null, "ROUTINE",
            "PROV-001", "Dr. Smith", null, null,
            "Evaluate murmur", "Heart murmur",
            null, null, null);

        var id2 = await workflow.RequestConsultAsync(
            "Pulmonology", null, "Primary Care", null, "URGENT",
            "PROV-001", "Dr. Smith", null, null,
            "Persistent cough x 3 months", "Chronic cough",
            null, null, null);

        var allConsults = await workflow.GetConsultsAsync(null, 50);
        Assert.That(allConsults, Has.Count.EqualTo(2));

        // Filter by status
        var pending = await workflow.GetConsultsAsync("PENDING", 50);
        Assert.That(pending, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task WorkflowGrain_CanAcceptAndScheduleConsult()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var consultId = await workflow.RequestConsultAsync(
            "Gastroenterology", null, null, null, "ROUTINE",
            null, null, null, null,
            "Evaluate GERD symptoms", null,
            null, null, null);

        await workflow.AcceptConsultAsync(consultId);
        var state = await workflow.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));

        await workflow.ScheduleConsultAsync(consultId);
        state = await workflow.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
    }

    [Test]
    public async Task WorkflowGrain_CompleteConsult_CreatesResultNote()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var consultId = await workflow.RequestConsultAsync(
            "Cardiology", null, "Primary Care", null, "ROUTINE",
            "PROV-001", "Dr. Smith", "PROV-002", "Dr. Jones",
            "Evaluate chest pain", "Chest pain",
            null, null, null);

        await workflow.AcceptConsultAsync(consultId);

        // Complete with result note
        await workflow.CompleteConsultAsync(consultId,
            "Cardiac evaluation complete. EKG normal. No evidence of CAD.\nRecommend: continue current medications.",
            "PROV-002", "Dr. Jones");

        var state = await workflow.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.ResultDocumentId, Is.Not.Null.And.StartsWith("TIU-"));

        // Verify the TIU note was created
        var note = await workflow.GetNoteAsync(state.ResultDocumentId!);
        Assert.That(note.DocumentType, Is.EqualTo("CONSULT NOTE"));
        Assert.That(note.ReportText, Does.Contain("Cardiac evaluation"));
        Assert.That(note.AuthorName, Is.EqualTo("Dr. Jones"));

        // Verify the note appears in the patient's note list
        var notes = await workflow.GetNotesAsync("CONSULT NOTE", 50);
        Assert.That(notes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task WorkflowGrain_CompleteConsult_WithoutNote()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var consultId = await workflow.RequestConsultAsync(
            "Lab", null, null, null, "ROUTINE",
            null, null, null, null, null, null, null, null, null);

        await workflow.AcceptConsultAsync(consultId);
        await workflow.CompleteConsultAsync(consultId, null, null, null);

        var state = await workflow.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.ResultDocumentId, Is.Null);
    }

    [Test]
    public async Task WorkflowGrain_CanCancelConsultViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var consultId = await workflow.RequestConsultAsync(
            "Dermatology", null, null, null, "ROUTINE",
            null, null, null, null, "Rash evaluation", null, null, null, null);

        await workflow.CancelConsultAsync(consultId, "Patient refused");

        var state = await workflow.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));

        // Cancelled consults still appear in listings
        var all = await workflow.GetConsultsAsync(null, 50);
        Assert.That(all, Has.Count.EqualTo(1));

        // But not when filtering for active
        var active = await workflow.GetConsultsAsync("ACTIVE", 50);
        Assert.That(active, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task WorkflowGrain_CoverSheet_IncludesActiveConsults()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Jane Doe", "F", new DateTime(1955, 3, 10), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.RequestConsultAsync(
            "Endocrinology", null, "Primary Care", null, "ROUTINE",
            "PROV-001", "Dr. Smith", null, null,
            "Evaluate thyroid nodule", "Thyroid nodule",
            null, null, null);

        var coverSheet = await workflow.GetCoverSheetAsync();

        Assert.That(coverSheet.ActiveConsults, Has.Count.EqualTo(1));
        Assert.That(coverSheet.ActiveConsults[0].ToService, Is.EqualTo("Endocrinology"));
        Assert.That(coverSheet.ActiveConsults[0].Status, Is.EqualTo("PENDING"));
    }

    [Test]
    public async Task WorkflowGrain_FilterConsultsByStatus()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var id1 = await workflow.RequestConsultAsync(
            "Cardiology", null, null, null, "ROUTINE",
            null, null, null, null, null, null, null, null, null);
        var id2 = await workflow.RequestConsultAsync(
            "Neurology", null, null, null, "URGENT",
            null, null, null, null, null, null, null, null, null);

        await workflow.AcceptConsultAsync(id1);

        var pending = await workflow.GetConsultsAsync("PENDING", 50);
        var active = await workflow.GetConsultsAsync("ACTIVE", 50);

        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].ToService, Is.EqualTo("Neurology"));
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ToService, Is.EqualTo("Cardiology"));
    }
}
