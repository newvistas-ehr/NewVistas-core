// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class TiuDocumentGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task TiuDocumentGrain_CanCreateNote()
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "PROGRESS NOTE", null,
            "Patient presents with chest pain. Assessment: stable angina.",
            "Cardiology Follow-up",
            "PROV-001", "Dr. Smith",
            null, null,
            "LOC-001", "Cardiology Clinic",
            null, DateTime.UtcNow);

        var state = await grain.GetDocumentAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DocumentType, Is.EqualTo("PROGRESS NOTE"));
        Assert.That(state.Subject, Is.EqualTo("Cardiology Follow-up"));
        Assert.That(state.ReportText, Does.Contain("chest pain"));
        Assert.That(state.AuthorName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.Status, Is.EqualTo("UNSIGNED"));
    }

    [Test]
    public async Task TiuDocumentGrain_SignWithoutCosigner_CompletesNote()
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "PROGRESS NOTE", null,
            "Brief note.", "Test",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);

        await grain.SignDocumentAsync(DateTime.UtcNow);
        var state = await grain.GetDocumentAsync();

        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.SignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task TiuDocumentGrain_SignWithCosigner_TransitionsToUncosigned()
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "PROGRESS NOTE", null,
            "Resident note requiring cosign.", "Test",
            "PROV-002", "Dr. Resident",
            "PROV-001", "Dr. Attending",
            null, null, null, DateTime.UtcNow);

        await grain.SignDocumentAsync(DateTime.UtcNow);
        var state = await grain.GetDocumentAsync();
        Assert.That(state.Status, Is.EqualTo("UNCOSIGNED"));

        await grain.CosignDocumentAsync(DateTime.UtcNow);
        state = await grain.GetDocumentAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.CosignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task TiuDocumentGrain_CanAmendNote()
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "PROGRESS NOTE", null,
            "Original text.", "Test",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);
        await grain.SignDocumentAsync(DateTime.UtcNow);

        await grain.AmendDocumentAsync("Original text.\n\nAMENDED: Additional findings noted.");
        var state = await grain.GetDocumentAsync();

        Assert.That(state.Status, Is.EqualTo("AMENDED"));
        Assert.That(state.ReportText, Does.Contain("AMENDED"));
    }

    [Test]
    public async Task TiuDocumentGrain_CanAddAddendum()
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "PROGRESS NOTE", null,
            "Original note.", "Test",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);

        var addendumId = $"TIU-{Guid.NewGuid()}";
        await grain.AddAddendumAsync(addendumId);
        var state = await grain.GetDocumentAsync();

        Assert.That(state.AddendumIds, Has.Count.EqualTo(1));
        Assert.That(state.AddendumIds, Contains.Item(addendumId));
    }

    [Test]
    public async Task TiuDocumentGrain_CanRetractNote()
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "PROGRESS NOTE", null,
            "Note entered in error.", "Test",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);

        await grain.RetractDocumentAsync();
        var state = await grain.GetDocumentAsync();

        Assert.That(state.Status, Is.EqualTo("RETRACTED"));
    }

    [Test]
    public async Task WorkflowGrain_CanCreateAndListNotes()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var docId1 = await workflow.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "First encounter note.\nVitals stable.",
            "Initial Visit",
            "PROV-001", "Dr. Smith",
            null, null,
            "LOC-001", "Primary Care",
            null, DateTime.UtcNow.AddHours(-2));

        var docId2 = await workflow.CreateNoteAsync(
            "DISCHARGE SUMMARY", null,
            "Patient discharged in good condition.",
            "Discharge",
            "PROV-001", "Dr. Smith",
            null, null,
            "LOC-002", "Med-Surg Ward",
            null, DateTime.UtcNow);

        var allNotes = await workflow.GetNotesAsync(null, 50);
        Assert.That(allNotes, Has.Count.EqualTo(2));
        Assert.That(allNotes[0].ReferenceDate, Is.GreaterThan(allNotes[1].ReferenceDate));

        var progressNotes = await workflow.GetNotesAsync("PROGRESS NOTE", 50);
        Assert.That(progressNotes, Has.Count.EqualTo(1));
        Assert.That(progressNotes[0].DocumentType, Is.EqualTo("PROGRESS NOTE"));
    }

    [Test]
    public async Task WorkflowGrain_CanSignAndRetrieveNote()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var docId = await workflow.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Patient doing well.", "Follow-up",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);

        await workflow.SignNoteAsync(docId);
        var note = await workflow.GetNoteAsync(docId);

        Assert.That(note.Status, Is.EqualTo("COMPLETED"));
        Assert.That(note.SignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task WorkflowGrain_CanAddAddendumViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var docId = await workflow.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Original note.", "Visit",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);
        await workflow.SignNoteAsync(docId);

        var addendumId = await workflow.AddAddendumAsync(
            docId, "Lab results reviewed — all normal.",
            "PROV-001", "Dr. Smith", DateTime.UtcNow);

        var parent = await workflow.GetNoteAsync(docId);
        Assert.That(parent.AddendumIds, Contains.Item(addendumId));

        var notes = await workflow.GetNotesAsync(null, 50);
        Assert.That(notes.Any(n => n.DocumentId == addendumId), Is.False,
            "Addenda should not appear in top-level note list");
        Assert.That(notes.Any(n => n.DocumentId == docId && n.HasAddenda), Is.True);
    }

    [Test]
    public async Task WorkflowGrain_CoverSheet_IncludesRecentNotes()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("John Doe", "M", new DateTime(1960, 5, 15), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Cover sheet test note.", "Test",
            "PROV-001", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);

        var coverSheet = await workflow.GetCoverSheetAsync();

        Assert.That(coverSheet.RecentNotes, Has.Count.EqualTo(1));
        Assert.That(coverSheet.RecentNotes[0].DocumentType, Is.EqualTo("PROGRESS NOTE"));
    }
}
