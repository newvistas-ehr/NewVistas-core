// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the consult lifecycle (File #123, GMRCACTM.m) driven entirely
/// through <see cref="IPatientWorkflowGrain"/> — the orchestration layer the UI and API
/// actually call. The existing <c>ConsultsWorkflowTests</c> exercise the consult grain
/// directly; these tests pin the workflow-level contract instead:
///
/// <list type="bullet">
/// <item>status transitions are visible through every read path after each step
/// (GetConsultAsync, GetConsultsAsync with and without a status filter, the cover
/// sheet's ActiveConsults section, and GetConsultHistoryAsync paging);</item>
/// <item>completing a consult with result text creates and links a CONSULT NOTE TIU
/// document that lands in the note index and the recent-notes hot cache;</item>
/// <item>the terminal-state guards hold: re-complete is a no-op, and backward
/// transitions out of a terminal status (cancel/discontinue/accept on a COMPLETE,
/// CANCELLED, or DISCONTINUED consult) throw without mutating state.</item>
/// </list>
/// </summary>
[TestFixture]
public class ConsultLifecycleWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private static Task<string> RequestStandardAsync(IPatientWorkflowGrain wf,
        string toService = "CARDIOLOGY", string urgency = "ROUTINE")
        => wf.RequestConsultAsync(
            toService, "SVC-CARD",
            "PRIMARY CARE", "SVC-PC",
            urgency,
            "PROV-001", "Dr. Referring",
            "PROV-002", "Dr. Consultant",
            "Evaluate chest pain", "Atypical chest pain",
            null, "LOC-001", "Medicine Clinic");

    private static ConsultSummary? Find(List<ConsultSummary> summaries, string consultId)
        => summaries.SingleOrDefault(s => s.ConsultId == consultId);

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Test]
    public async Task ConsultLifecycle_HappyPath_EachTransitionVisibleInEveryRead()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act — request
        string consultId = await RequestStandardAsync(wf);

        // Assert — PENDING everywhere
        ConsultState afterRequest = await wf.GetConsultAsync(consultId);
        Assert.That(afterRequest.Status, Is.EqualTo("PENDING"));
        Assert.That(afterRequest.PatientId, Is.EqualTo(patientId));
        Assert.That(afterRequest.ToService, Is.EqualTo("CARDIOLOGY"));
        Assert.That(afterRequest.RequestDateTime, Is.Not.EqualTo(default(DateTime)));

        ConsultSummary? pendingSummary = Find(await wf.GetConsultsAsync("PENDING", 10), consultId);
        Assert.That(pendingSummary, Is.Not.Null, "a fresh request must show up in a PENDING-filtered list");
        Assert.That(pendingSummary!.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(pendingSummary.HasResultDocument, Is.False);

        // Act — accept
        await wf.AcceptConsultAsync(consultId);

        // Assert — ACTIVE; no longer PENDING
        Assert.That((await wf.GetConsultAsync(consultId)).Status, Is.EqualTo("ACTIVE"));
        Assert.That(Find(await wf.GetConsultsAsync("ACTIVE", 10), consultId), Is.Not.Null);
        Assert.That(Find(await wf.GetConsultsAsync("PENDING", 10), consultId), Is.Null,
            "an accepted consult must leave the PENDING view");

        // Act — schedule
        await wf.ScheduleConsultAsync(consultId);

        // Assert — SCHEDULED
        Assert.That((await wf.GetConsultAsync(consultId)).Status, Is.EqualTo("SCHEDULED"));
        Assert.That(Find(await wf.GetConsultsAsync("SCHEDULED", 10), consultId), Is.Not.Null);
        Assert.That(Find(await wf.GetConsultsAsync("ACTIVE", 10), consultId), Is.Null);

        // Act — complete with a result note
        await wf.CompleteConsultAsync(consultId,
            "Echocardiogram reviewed. EF 55%, no wall-motion abnormality. Chest pain non-cardiac.",
            "PROV-002", "Dr. Consultant");

        // Assert — COMPLETE, dated, linked to a result document
        ConsultState final = await wf.GetConsultAsync(consultId);
        Assert.That(final.Status, Is.EqualTo("COMPLETE"));
        Assert.That(final.CompletedDateTime, Is.Not.Null);
        Assert.That(final.ResultDocumentId, Is.Not.Null.And.Not.Empty);

        ConsultSummary? completeSummary = Find(await wf.GetConsultsAsync("COMPLETE", 10), consultId);
        Assert.That(completeSummary, Is.Not.Null);
        Assert.That(completeSummary!.HasResultDocument, Is.True);
        Assert.That(Find(await wf.GetConsultsAsync("SCHEDULED", 10), consultId), Is.Null);
    }

    // ─── Completion and the result note ──────────────────────────────────────

    [Test]
    public async Task CompleteWithResultText_CreatesLinkedConsultNote_InIndexAndHotCache()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);

        // Act
        await wf.CompleteConsultAsync(consultId,
            "Consult findings: mild aortic sclerosis, no stenosis.",
            "PROV-002", "Dr. Consultant");

        // Assert — the linked TIU document is a real, readable CONSULT NOTE
        ConsultState state = await wf.GetConsultAsync(consultId);
        string resultDocumentId = state.ResultDocumentId!;
        TiuDocumentState note = await wf.GetNoteAsync(resultDocumentId);
        Assert.That(note.DocumentType, Is.EqualTo("CONSULT NOTE"));
        Assert.That(note.ReportText, Does.Contain("aortic sclerosis"));
        Assert.That(note.PatientId, Is.EqualTo(patientId));
        Assert.That(note.AuthorName, Is.EqualTo("Dr. Consultant"));

        // The note must land in the per-patient note index...
        List<TiuNoteSummary> indexed = await wf.GetNotesAsync("CONSULT NOTE", 10);
        Assert.That(indexed.Select(n => n.DocumentId), Contains.Item(resultDocumentId));

        // ...and in the recent-notes hot cache that feeds the cover sheet.
        List<TiuNoteSummary> recent = await wf.GetRecentNotesAsync();
        Assert.That(recent.Select(n => n.DocumentId), Contains.Item(resultDocumentId));
    }

    [Test]
    public async Task CompleteWithoutResultText_LeavesNoResultDocument()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);

        // Act
        await wf.CompleteConsultAsync(consultId, null, "PROV-002", "Dr. Consultant");

        // Assert
        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.ResultDocumentId, Is.Null);
        ConsultSummary? summary = Find(await wf.GetConsultsAsync("COMPLETE", 10), consultId);
        Assert.That(summary!.HasResultDocument, Is.False);
        Assert.That(await wf.GetNotesAsync("CONSULT NOTE", 10), Is.Empty,
            "no result text means no result note");
    }

    [Test]
    public async Task CompleteTwice_SecondCallIsANoOp_OriginalOutcomePreserved()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);
        await wf.CompleteConsultAsync(consultId, "First and final result.", "PROV-002", "Dr. Consultant");
        ConsultState first = await wf.GetConsultAsync(consultId);

        // Act — a repeat completion (no result text, so no side-band note is created)
        await wf.CompleteConsultAsync(consultId, null, "PROV-009", "Dr. SecondOpinion");

        // Assert — the grain's already-complete guard holds: nothing about the
        // first completion is overwritten.
        ConsultState second = await wf.GetConsultAsync(consultId);
        Assert.That(second.Status, Is.EqualTo("COMPLETE"));
        Assert.That(second.CompletedDateTime, Is.EqualTo(first.CompletedDateTime));
        Assert.That(second.ResultDocumentId, Is.EqualTo(first.ResultDocumentId));
    }

    // ─── Cancel / discontinue ────────────────────────────────────────────────

    [Test]
    public async Task Cancel_WithComments_RecordsReasonAndTerminalStatus()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);

        // Act
        await wf.CancelConsultAsync(consultId, "Patient declined the consult");

        // Assert
        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Is.EqualTo("Patient declined the consult"));
        Assert.That(Find(await wf.GetConsultsAsync("CANCELLED", 10), consultId), Is.Not.Null);
        Assert.That(Find(await wf.GetConsultsAsync("PENDING", 10), consultId), Is.Null);
    }

    [Test]
    public async Task Cancel_WithoutComments_StillCancels_CommentsStayNull()
    {
        // The API differentiates: comments are optional, and an omitted reason must
        // not block the cancellation or leave a phantom comment behind.
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);

        await wf.CancelConsultAsync(consultId, null);

        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Is.Null);
    }

    [Test]
    public async Task Discontinue_AfterAccept_RecordsReasonAndTerminalStatus()
    {
        // Arrange — discontinue is the verb for a consult already in flight
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);

        // Act
        await wf.DiscontinueConsultAsync(consultId, "No longer clinically indicated");

        // Assert
        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.Comments, Is.EqualTo("No longer clinically indicated"));
        Assert.That(Find(await wf.GetConsultsAsync("DISCONTINUED", 10), consultId), Is.Not.Null);
    }

    // ─── Out-of-order transitions ────────────────────────────────────────────

    [Test]
    public async Task AcceptTwice_RemainsActive_NoDuplicateInListing()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);

        // Act — the accepting service double-clicks
        await wf.AcceptConsultAsync(consultId);
        await wf.AcceptConsultAsync(consultId);

        // Assert — idempotent outcome: still ACTIVE, listed exactly once
        Assert.That((await wf.GetConsultAsync(consultId)).Status, Is.EqualTo("ACTIVE"));
        List<ConsultSummary> active = await wf.GetConsultsAsync("ACTIVE", 10);
        Assert.That(active.Count(s => s.ConsultId == consultId), Is.EqualTo(1));
    }

    [Test]
    public async Task ScheduleBeforeAccept_MovesDirectlyToScheduled()
    {
        // The grain is deliberately permissive about FORWARD movement: a clerk may
        // book the appointment before the service formally acknowledges the request
        // (PENDING → SCHEDULED without an ACTIVE stop). Pin that permissiveness.
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);

        await wf.ScheduleConsultAsync(consultId);

        Assert.That((await wf.GetConsultAsync(consultId)).Status, Is.EqualTo("SCHEDULED"));
        Assert.That(Find(await wf.GetConsultsAsync("PENDING", 10), consultId), Is.Null);
    }

    // ─── Terminal-state guards (backward transitions must throw) ─────────────

    [Test]
    public async Task Cancel_OnCompleteConsult_Throws_TerminalStateUnchanged()
    {
        // Arrange — drive the consult to COMPLETE
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);
        await wf.CompleteConsultAsync(consultId, "Final result.", "PROV-002", "Dr. Consultant");
        ConsultState before = await wf.GetConsultAsync(consultId);

        // Act / Assert — cancelling a completed consult is rejected outright
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.CancelConsultAsync(consultId, "Too late to cancel"));
        Assert.That(ex!.Message, Does.Contain("COMPLETE"));

        // The terminal state survives untouched — no comment, no status flip.
        ConsultState after = await wf.GetConsultAsync(consultId);
        Assert.That(after.Status, Is.EqualTo("COMPLETE"));
        Assert.That(after.CompletedDateTime, Is.EqualTo(before.CompletedDateTime));
        Assert.That(after.ResultDocumentId, Is.EqualTo(before.ResultDocumentId));
        Assert.That(after.Comments, Is.EqualTo(before.Comments));
        Assert.That(Find(await wf.GetConsultsAsync("CANCELLED", 10), consultId), Is.Null);
    }

    [Test]
    public async Task Discontinue_OnCancelledConsult_Throws_OriginalCommentsIntact()
    {
        // Arrange — cancel with a reason worth preserving
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.CancelConsultAsync(consultId, "Patient declined the consult");

        // Act / Assert
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.DiscontinueConsultAsync(consultId, "Attempting to discontinue instead"));
        Assert.That(ex!.Message, Does.Contain("CANCELLED"));

        // The cancellation — status AND comments — must be exactly as recorded.
        ConsultState state = await wf.GetConsultAsync(consultId);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Is.EqualTo("Patient declined the consult"));
        Assert.That(Find(await wf.GetConsultsAsync("DISCONTINUED", 10), consultId), Is.Null);
    }

    [Test]
    public async Task Discontinue_OnCompleteConsult_Throws()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);
        await wf.CompleteConsultAsync(consultId, null, "PROV-002", "Dr. Consultant");

        // Act / Assert
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.DiscontinueConsultAsync(consultId, "No longer indicated"));
        Assert.That(ex!.Message, Does.Contain("COMPLETE"));
        Assert.That((await wf.GetConsultAsync(consultId)).Status, Is.EqualTo("COMPLETE"));
    }

    [Test]
    public async Task Accept_OnCompleteConsult_Throws_DoesNotReactivate()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);
        await wf.CompleteConsultAsync(consultId, "Result filed.", "PROV-002", "Dr. Consultant");

        // Act / Assert — a completed consult can never come back to life
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => wf.AcceptConsultAsync(consultId));
        Assert.That(ex!.Message, Does.Contain("COMPLETE"));

        Assert.That((await wf.GetConsultAsync(consultId)).Status, Is.EqualTo("COMPLETE"));
        Assert.That(Find(await wf.GetConsultsAsync("ACTIVE", 10), consultId), Is.Null);
    }

    [Test]
    public async Task RepeatCompleteWithResultText_FilesNoOrphanNote()
    {
        // Arrange — complete once, with a linked result note
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);
        await wf.AcceptConsultAsync(consultId);
        await wf.CompleteConsultAsync(consultId, "First and final result.", "PROV-002", "Dr. Consultant");
        ConsultState first = await wf.GetConsultAsync(consultId);
        List<TiuNoteSummary> notesBefore = await wf.GetNotesAsync("CONSULT NOTE", 50);
        Assert.That(notesBefore, Has.Count.EqualTo(1));

        // Act — a repeat completion that carries result text
        await wf.CompleteConsultAsync(consultId, "Second-opinion text that must not be filed.",
            "PROV-009", "Dr. SecondOpinion");

        // Assert — no orphaned CONSULT NOTE: the note count must not grow, and the
        // consult still points at the original result document.
        List<TiuNoteSummary> notesAfter = await wf.GetNotesAsync("CONSULT NOTE", 50);
        Assert.That(notesAfter, Has.Count.EqualTo(notesBefore.Count),
            "a repeat completion must not file a new result note");
        Assert.That((await wf.GetRecentNotesAsync()).Count(n => n.DocumentType == "CONSULT NOTE"),
            Is.EqualTo(1), "the hot cache must not pick up an orphan note either");

        ConsultState second = await wf.GetConsultAsync(consultId);
        Assert.That(second.Status, Is.EqualTo("COMPLETE"));
        Assert.That(second.CompletedDateTime, Is.EqualTo(first.CompletedDateTime));
        Assert.That(second.ResultDocumentId, Is.EqualTo(first.ResultDocumentId));
    }

    // ─── Cover sheet / recent activity coherence ─────────────────────────────

    [Test]
    public async Task CoverSheet_ShowsConsultAfterRequest_AndTracksStatusWithoutDuplicates()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);

        // Assert — freshly requested consult is on the cover sheet
        CoverSheetState afterRequest = await wf.GetCoverSheetAsync();
        List<ConsultSummary> sheetConsults = afterRequest.ActiveConsults
            .Where(c => c.ConsultId == consultId).ToList();
        Assert.That(sheetConsults, Has.Count.EqualTo(1));
        Assert.That(sheetConsults[0].Status, Is.EqualTo("PENDING"));
        Assert.That(sheetConsults[0].ToService, Is.EqualTo("CARDIOLOGY"));

        // Act — advance the consult and re-read the cover sheet at each step
        await wf.AcceptConsultAsync(consultId);
        CoverSheetState afterAccept = await wf.GetCoverSheetAsync();
        Assert.That(Find(afterAccept.ActiveConsults, consultId)!.Status, Is.EqualTo("ACTIVE"),
            "cover sheet must show the live status, never a stale copy");

        await wf.CompleteConsultAsync(consultId, "Result filed.", "PROV-002", "Dr. Consultant");
        CoverSheetState afterComplete = await wf.GetCoverSheetAsync();
        ConsultSummary? completed = Find(afterComplete.ActiveConsults, consultId);
        Assert.That(completed!.Status, Is.EqualTo("COMPLETE"));
        Assert.That(completed.HasResultDocument, Is.True);
        Assert.That(afterComplete.ActiveConsults.Count(c => c.ConsultId == consultId), Is.EqualTo(1),
            "status changes must update the one entry, not append another");

        // The consult's result note reaches the cover sheet's RecentNotes section too.
        string resultDocumentId = (await wf.GetConsultAsync(consultId)).ResultDocumentId!;
        Assert.That(afterComplete.RecentNotes.Select(n => n.DocumentId), Contains.Item(resultDocumentId));
    }

    // ─── Listing, filtering, paging ──────────────────────────────────────────

    [Test]
    public async Task GetConsults_StatusFilterIsCaseInsensitive()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string consultId = await RequestStandardAsync(wf);

        // Act / Assert
        Assert.That(Find(await wf.GetConsultsAsync("pending", 10), consultId), Is.Not.Null);
        Assert.That(Find(await wf.GetConsultsAsync("Pending", 10), consultId), Is.Not.Null);
    }

    [Test]
    public async Task GetConsults_MaxResultsCapsTheResponse()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        await RequestStandardAsync(wf, toService: "CARDIOLOGY");
        await RequestStandardAsync(wf, toService: "NEUROLOGY");
        await RequestStandardAsync(wf, toService: "NEPHROLOGY");

        // Act
        List<ConsultSummary> capped = await wf.GetConsultsAsync(null, 2);
        List<ConsultSummary> all = await wf.GetConsultsAsync(null, 10);

        // Assert
        Assert.That(capped, Has.Count.EqualTo(2));
        Assert.That(all, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetConsultHistory_PagesAreDisjointAndCoverEverything()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        var created = new List<string>
        {
            await RequestStandardAsync(wf, toService: "CARDIOLOGY"),
            await RequestStandardAsync(wf, toService: "NEUROLOGY"),
            await RequestStandardAsync(wf, toService: "NEPHROLOGY"),
        };

        // Act
        List<ConsultSummary> page1 = await wf.GetConsultHistoryAsync(0, 2);
        List<ConsultSummary> page2 = await wf.GetConsultHistoryAsync(2, 2);

        // Assert — 2 + 1, no overlap, and together the pages are exactly the
        // consults created (newest first: the most recent request leads page 1).
        Assert.That(page1, Has.Count.EqualTo(2));
        Assert.That(page2, Has.Count.EqualTo(1));
        List<string> paged = page1.Concat(page2).Select(s => s.ConsultId).ToList();
        Assert.That(paged, Is.Unique);
        Assert.That(paged, Is.EquivalentTo(created));
        Assert.That(page1[0].ConsultId, Is.EqualTo(created[2]));
    }

    [Test]
    public async Task Consults_AreIsolatedPerPatient()
    {
        // Arrange — two patients, one consult each
        IPatientWorkflowGrain wfA = Workflow($"PAT-{Guid.NewGuid()}");
        IPatientWorkflowGrain wfB = Workflow($"PAT-{Guid.NewGuid()}");
        string consultA = await RequestStandardAsync(wfA);
        string consultB = await RequestStandardAsync(wfB, toService: "NEUROLOGY");

        // Act
        List<ConsultSummary> listA = await wfA.GetConsultsAsync(null, 10);
        List<ConsultSummary> listB = await wfB.GetConsultsAsync(null, 10);

        // Assert
        Assert.That(listA.Select(s => s.ConsultId), Is.EquivalentTo(new[] { consultA }));
        Assert.That(listB.Select(s => s.ConsultId), Is.EquivalentTo(new[] { consultB }));
    }
}
