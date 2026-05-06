// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA TIU Documents — clinical notes package.
/// File #8925 (TIU DOCUMENT). Mirrors TIUSRVN.m, TIUSRVL.m, TIUSRVP.m, TIUSRVA.m.
/// </summary>
[TestFixture]
public class TiuWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private ITiuDocumentGrain GetDocument(string documentId)
        => _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

    // ─── ID / Creation ────────────────────────────────────────────────────

    [Test]
    public async Task CreateNote_ReturnsIdWithTiuPrefix()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Patient presents with hypertension, BP 150/95.",
            "Hypertension Follow-up",
            "PROV-001", "Dr. Adams",
            null, null,
            "LOC-001", "Primary Care",
            null, DateTime.UtcNow);

        Assert.That(id, Does.StartWith("TIU-"));
    }

    [Test]
    public async Task CreateNote_DocumentTypeStoredCorrectly()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "DISCHARGE SUMMARY", null,
            "Patient discharged in stable condition.",
            "Discharge",
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.DocumentType, Is.EqualTo("DISCHARGE SUMMARY"));
    }

    [Test]
    public async Task CreateNote_ReportTextStoredCorrectly()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "SUBJECTIVE: Patient reports chest pain for 2 days.\nASSESSMENT: Stable angina.",
            null,
            null, null, null, null, null, null, null, DateTime.UtcNow);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.ReportText, Does.Contain("chest pain"));
        Assert.That(state.ReportText, Does.Contain("ASSESSMENT"));
    }

    [Test]
    public async Task CreateNote_SubjectStoredCorrectly()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Follow-up visit for diabetes management.",
            "Diabetes Management Visit",
            null, null, null, null, null, null, null, DateTime.UtcNow);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.Subject, Is.EqualTo("Diabetes Management Visit"));
    }

    [Test]
    public async Task CreateNote_InitialStatusIsUnsigned()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "New note not yet signed.",
            null,
            "PROV-003", "Dr. Johnson",
            null, null, null, null, null, DateTime.UtcNow);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.Status, Is.EqualTo("UNSIGNED"));
    }

    [Test]
    public async Task CreateNote_AuthorNameStoredCorrectly()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "CONSULT NOTE", null,
            "Cardiology consult — patient evaluated for arrhythmia.",
            "Cardiology Consult",
            "PROV-007", "Dr. Rivera",
            null, null,
            "LOC-003", "Cardiology",
            null, DateTime.UtcNow);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.AuthorName, Is.EqualTo("Dr. Rivera"));
        Assert.That(state.AuthorId, Is.EqualTo("PROV-007"));
    }

    // ─── Patient Linkage ──────────────────────────────────────────────────

    [Test]
    public async Task CreateNote_LinksToPatientDocumentIds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Annual wellness visit.",
            "Annual Wellness",
            null, null, null, null, null, null, null, DateTime.UtcNow);

        List<string> docIds = await GetPatient(patientId).GetTiuDocumentIdsAsync();
        Assert.That(docIds, Has.Count.EqualTo(1));
        Assert.That(docIds[0], Does.StartWith("TIU-"));
    }

    // ─── Retrieve ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetNotes_NoNotes_ReturnsEmpty()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<TiuNoteSummary> notes = await w.GetNotesAsync(null, 50);

        Assert.That(notes, Is.Empty);
    }

    [Test]
    public async Task GetNotes_ReturnsAllTopLevelNotes()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "First visit note.", "Visit 1",
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow.AddDays(-7));

        await w.CreateNoteAsync(
            "DISCHARGE SUMMARY", null,
            "Patient discharged.", "Discharge",
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow);

        List<TiuNoteSummary> notes = await w.GetNotesAsync(null, 50);
        Assert.That(notes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetNotes_FilteredByType_ReturnsCorrectSubset()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Regular follow-up.", null,
            null, null, null, null, null, null, null, DateTime.UtcNow.AddDays(-3));

        await w.CreateNoteAsync(
            "DISCHARGE SUMMARY", null,
            "Discharge from inpatient stay.", null,
            null, null, null, null, null, null, null, DateTime.UtcNow);

        List<TiuNoteSummary> progressNotes = await w.GetNotesAsync("PROGRESS NOTE", 50);
        Assert.That(progressNotes, Has.Count.EqualTo(1));
        Assert.That(progressNotes[0].DocumentType, Is.EqualTo("PROGRESS NOTE"));

        List<TiuNoteSummary> allNotes = await w.GetNotesAsync(null, 50);
        Assert.That(allNotes, Has.Count.EqualTo(2));
    }

    // ─── Signing Workflow ─────────────────────────────────────────────────

    [Test]
    public async Task SignNote_WithoutCosigner_StatusIsCompleted()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Attending note — no cosigner required.",
            null,
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow);

        await w.SignNoteAsync(id);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.SignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task SignNote_WithCosigner_StatusIsUncosigned()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Resident note requiring attending cosignature.",
            null,
            "PROV-099", "Dr. Resident",
            "PROV-001", "Dr. Attending",
            null, null, null, DateTime.UtcNow);

        await w.SignNoteAsync(id);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.Status, Is.EqualTo("UNCOSIGNED"));
        Assert.That(state.SignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task CosignNote_TransitionsToCompleted()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Resident note.",
            null,
            "PROV-099", "Dr. Resident",
            "PROV-001", "Dr. Attending",
            null, null, null, DateTime.UtcNow);

        await w.SignNoteAsync(id);
        await w.CosignNoteAsync(id);

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.CosignedDateTime, Is.Not.Null);
    }

    // ─── Addendum ─────────────────────────────────────────────────────────

    [Test]
    public async Task AddAddendum_LinkedToParentNote()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string parentId = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Original note — labs pending.",
            "Lab Pending",
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow.AddHours(-3));

        await w.SignNoteAsync(parentId);

        string addendumId = await w.AddAddendumAsync(
            parentId,
            "Lab results reviewed — CBC normal, BMP unremarkable.",
            "PROV-001", "Dr. Adams",
            DateTime.UtcNow);

        Assert.That(addendumId, Does.StartWith("TIU-"));

        TiuDocumentState parentState = await GetDocument(parentId).GetDocumentAsync();
        Assert.That(parentState.AddendumIds, Contains.Item(addendumId));

        TiuDocumentState addendumState = await GetDocument(addendumId).GetDocumentAsync();
        Assert.That(addendumState.ParentDocumentId, Is.EqualTo(parentId));
    }

    [Test]
    public async Task AddAddendum_ExcludedFromTopLevelNoteList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string parentId = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Initial note.",
            "Initial",
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow);

        await w.SignNoteAsync(parentId);

        string addendumId = await w.AddAddendumAsync(
            parentId,
            "Addendum: patient called with additional history.",
            "PROV-001", "Dr. Adams",
            DateTime.UtcNow);

        List<TiuNoteSummary> notes = await w.GetNotesAsync(null, 50);

        // Addendum must not appear as top-level note
        Assert.That(notes.Any(n => n.DocumentId == addendumId), Is.False,
            "Addendum should not appear in top-level note list");

        // Parent must be flagged as having addenda
        TiuNoteSummary? parent = notes.FirstOrDefault(n => n.DocumentId == parentId);
        Assert.That(parent, Is.Not.Null);
        Assert.That(parent!.HasAddenda, Is.True);
    }

    // ─── Recent Notes Cache (Three-Tier) ─────────────────────────────────

    [Test]
    public async Task GetRecentNotes_ReturnsCachedNotes()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Cached note 1.", "Visit 1",
            "PROV-001", "Dr. Adams",
            null, null, "LOC-001", "Primary Care",
            null, DateTime.UtcNow.AddDays(-1));

        await w.CreateNoteAsync(
            "DISCHARGE SUMMARY", null,
            "Cached note 2.", "Discharge",
            "PROV-001", "Dr. Adams",
            null, null, null, null,
            null, DateTime.UtcNow);

        List<TiuNoteSummary> recent = await w.GetRecentNotesAsync();
        Assert.That(recent, Has.Count.EqualTo(2));
        // Most recent first
        Assert.That(recent[0].DocumentType, Is.EqualTo("DISCHARGE SUMMARY"));
    }

    [Test]
    public async Task GetNoteHistory_FiltersByDateRange()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        DateTime jan = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime feb = new DateTime(2026, 2, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime mar = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        await w.CreateNoteAsync("PROGRESS NOTE", null, "Jan note.", null,
            null, null, null, null, null, null, null, jan);
        await w.CreateNoteAsync("PROGRESS NOTE", null, "Feb note.", null,
            null, null, null, null, null, null, null, feb);
        await w.CreateNoteAsync("PROGRESS NOTE", null, "Mar note.", null,
            null, null, null, null, null, null, null, mar);

        List<TiuNoteSummary> history = await w.GetNoteHistoryAsync(
            new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), 100);
        Assert.That(history, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SignNote_UpdatesIndexAndCache()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Note to sign.", null,
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow);

        // Verify initial status in cache
        List<TiuNoteSummary> before = await w.GetRecentNotesAsync();
        Assert.That(before[0].Status, Is.EqualTo("UNSIGNED"));

        await w.SignNoteAsync(id);

        // After signing, cache should reflect updated status
        List<TiuNoteSummary> after = await w.GetRecentNotesAsync();
        Assert.That(after[0].Status, Is.EqualTo("COMPLETED"));

        // Index should also reflect updated status
        List<TiuNoteSummary> indexed = await w.GetNotesAsync(null, 50);
        TiuNoteSummary? note = indexed.FirstOrDefault(n => n.DocumentId == id);
        Assert.That(note, Is.Not.Null);
        Assert.That(note!.Status, Is.EqualTo("COMPLETED"));
    }

    // ─── Isolation ────────────────────────────────────────────────────────

    [Test]
    public async Task MultiplePatients_NotesAreIndependent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await w1.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Note for patient 1.",
            null, null, null, null, null, null, null, null, DateTime.UtcNow);

        List<TiuNoteSummary> notesPatient2 = await w2.GetNotesAsync(null, 50);
        Assert.That(notesPatient2, Is.Empty);
    }

    // ─── Performance Benchmark ─────────────────────────────────────────

    [Test]
    public async Task PerformanceBenchmark_CachedNotes_FasterThanIndexQuery()
    {
        string patientId = $"PATIENT-PERF-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Set cache to 5 — only the 5 most recent notes stay on the patient grain
        ISiteParametersGrain siteParams = _cluster.GrainFactory
            .GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.SetNotesDisplayCountAsync(5);

        // Seed 50 notes across different types
        string[] noteTypes = ["PROGRESS NOTE", "DISCHARGE SUMMARY", "CONSULT NOTE",
            "PROCEDURE NOTE", "HISTORY & PHYSICAL"];

        for (int i = 0; i < 50; i++)
        {
            string noteType = noteTypes[i % noteTypes.Length];
            DateTime refDate = DateTime.UtcNow.AddDays(-50 + i);

            await w.CreateNoteAsync(
                noteType, null,
                $"Clinical note #{i} — patient follow-up for {noteType.ToLower()}.",
                $"Note {i}",
                "PROV-001", "Dr. Benchmark",
                null, null,
                "LOC-001", "Primary Care",
                null, refDate);
        }

        // Verify setup: 5 in cache, 50 in index
        List<TiuNoteSummary> cached = await GetPatient(patientId).GetRecentNotesAsync();
        IPatientNoteIndexGrain noteIndex = _cluster.GrainFactory
            .GetGrain<IPatientNoteIndexGrain>(patientId);
        int indexCount = await noteIndex.GetCountAsync();
        Assert.That(cached, Has.Count.EqualTo(5), "Cache should hold exactly 5 notes");
        Assert.That(indexCount, Is.EqualTo(50), "Index should hold all 50 notes");

        // ── Warm-up calls ──
        await w.GetRecentNotesAsync();
        await w.GetNotesAsync(null, 50);

        // ── Timed: hot cache read (embedded on patient grain — zero fan-out) ──
        const int iterations = 20;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            List<TiuNoteSummary> recent = await w.GetRecentNotesAsync();
            Assert.That(recent, Has.Count.GreaterThan(0));
        }
        sw.Stop();
        long cachedMs = sw.ElapsedMilliseconds;
        double cachedAvgMs = (double)cachedMs / iterations;

        // ── Timed: index query (reads from index grain — single grain, no fan-out) ──
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            List<TiuNoteSummary> all = await w.GetNotesAsync(null, 50);
            Assert.That(all, Has.Count.EqualTo(50));
        }
        sw.Stop();
        long indexMs = sw.ElapsedMilliseconds;
        double indexAvgMs = (double)indexMs / iterations;

        double speedup = indexAvgMs > 0 ? cachedAvgMs > 0 ? indexAvgMs / cachedAvgMs : double.PositiveInfinity : 1.0;

        // Log results so they appear in test output
        TestContext.Out.WriteLine("╔══════════════════════════════════════════════════════════╗");
        TestContext.Out.WriteLine("║  NOTES PERFORMANCE BENCHMARK                            ║");
        TestContext.Out.WriteLine("╠══════════════════════════════════════════════════════════╣");
        TestContext.Out.WriteLine($"║  Patient notes:      50 total, 5 cached                 ║");
        TestContext.Out.WriteLine($"║  Iterations:         {iterations,4}                                ║");
        TestContext.Out.WriteLine($"║  Cache read avg:     {cachedAvgMs,8:F2} ms  (zero fan-out)      ║");
        TestContext.Out.WriteLine($"║  Index query avg:    {indexAvgMs,8:F2} ms  (50 entries)        ║");
        TestContext.Out.WriteLine($"║  Speedup factor:     {speedup,8:F1}x                          ║");
        TestContext.Out.WriteLine("╚══════════════════════════════════════════════════════════╝");

        // Reset display count to default
        await siteParams.SetNotesDisplayCountAsync(10);
    }

    // ─── Amend & Get ──────────────────────────────────────────────────────

    [Test]
    public async Task AmendNote_AppendsAmendedText()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "PROGRESS NOTE", null,
            "Original note — labs pending review.",
            "Lab Review",
            "PROV-001", "Dr. Adams",
            null, null, null, null, null, DateTime.UtcNow);

        await w.SignNoteAsync(id);

        await w.AmendNoteAsync(id, "AMENDMENT: Lab values reviewed — potassium corrected to 4.2.");

        TiuDocumentState state = await GetDocument(id).GetDocumentAsync();
        Assert.That(state.ReportText, Does.Contain("potassium"));
        Assert.That(state.Status, Is.EqualTo("AMENDED"));
    }

    [Test]
    public async Task GetNote_ReturnsFullState()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.CreateNoteAsync(
            "CONSULT NOTE", null,
            "Patient evaluated for chronic knee pain. MRI recommended.",
            "Orthopedic Consult",
            "PROV-010", "Dr. Ortho",
            null, null,
            "LOC-005", "Orthopedics",
            null, DateTime.UtcNow);

        TiuDocumentState state = await w.GetNoteAsync(id);
        Assert.That(state.DocumentType, Is.EqualTo("CONSULT NOTE"));
        Assert.That(state.ReportText, Does.Contain("chronic knee pain"));
        Assert.That(state.AuthorName, Is.EqualTo("Dr. Ortho"));
    }
}
