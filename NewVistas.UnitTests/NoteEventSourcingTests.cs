// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Notes;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Event-sourcing tests for the NOTES (TIU) domain. TiuDocumentGrain emits
/// causal envelopes for create / sign / cosign into the patient's clinical
/// event stream and replay reproduces the live state — including the cosigner-
/// dependent status branching (UNCOSIGNED vs COMPLETED on first signature).
/// </summary>
[TestFixture]
public class NoteEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ITiuDocumentGrain Note(string docId) =>
        _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(docId);

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private async Task<(string patientId, string docId)> CreateNoteAsync(
        string? cosignerId = null)
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string docId = $"TIU-{Guid.NewGuid()}";
        await Note(docId).CreateDocumentAsync(
            patientId, "PROGRESS NOTE", "TYPE-PN",
            "Patient seen for follow-up. Vitals stable.",
            "Follow-up visit",
            "PROV-1", "Smith,Jane",
            cosignerId, cosignerId is null ? null : "Brown,Bob",
            "LOC-1", "Clinic A",
            null, DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, docId);
    }

    [Test]
    public async Task CreateNote_EmitsNoteCreatedV1()
    {
        var (patientId, docId) = await CreateNoteAsync();

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(NoteCreatedV1)));
        Assert.That(events[0].Domain, Is.EqualTo("NOTES"));

        var payload = events[0].Payload as NoteCreatedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.DocumentId, Is.EqualTo(docId));
        Assert.That(payload.Snapshot.DocumentType, Is.EqualTo("PROGRESS NOTE"));
        Assert.That(payload.Snapshot.Status, Is.EqualTo("UNSIGNED"));
        Assert.That(payload.Snapshot.ReportText, Does.Contain("follow-up"));
    }

    [Test]
    public async Task CreateNote_Idempotent_OnSecondCreate()
    {
        var (patientId, docId) = await CreateNoteAsync();

        await Note(docId).CreateDocumentAsync(
            "OTHER-PAT", "DISCHARGE SUMMARY", null,
            "DIFFERENT TEXT", null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
        TiuDocumentState live = await Note(docId).GetDocumentAsync();
        Assert.That(live.PatientId, Is.EqualTo(patientId));
        Assert.That(live.DocumentType, Is.EqualTo("PROGRESS NOTE"));
    }

    [Test]
    public async Task SignNote_NoCosigner_ResultsInCompleted()
    {
        var (patientId, docId) = await CreateNoteAsync(cosignerId: null);

        DateTime signed = DateTime.UtcNow;
        await Note(docId).SignDocumentAsync(signed);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(NoteSignedV1)));
        var payload = events[1].Payload as NoteSignedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.SignedDateTime, Is.EqualTo(signed));
        Assert.That(payload.ResultingStatus, Is.EqualTo("COMPLETED"));

        TiuDocumentState live = await Note(docId).GetDocumentAsync();
        Assert.That(live.Status, Is.EqualTo("COMPLETED"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task SignNote_WithCosigner_ResultsInUncosigned()
    {
        var (patientId, docId) = await CreateNoteAsync(cosignerId: "PROV-2");

        await Note(docId).SignDocumentAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        var payload = events[1].Payload as NoteSignedV1;
        Assert.That(payload!.ResultingStatus, Is.EqualTo("UNCOSIGNED"));

        TiuDocumentState live = await Note(docId).GetDocumentAsync();
        Assert.That(live.Status, Is.EqualTo("UNCOSIGNED"));
    }

    [Test]
    public async Task SignNote_Idempotent_OnSecondSign()
    {
        var (patientId, docId) = await CreateNoteAsync();

        await Note(docId).SignDocumentAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        await Note(docId).SignDocumentAsync(DateTime.UtcNow.AddMinutes(5));
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task CosignNote_TransitionsToCompleted()
    {
        var (patientId, docId) = await CreateNoteAsync(cosignerId: "PROV-2");

        await Note(docId).SignDocumentAsync(DateTime.UtcNow);
        DateTime cosigned = DateTime.UtcNow;
        await Note(docId).CosignDocumentAsync(cosigned);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[2].EventType, Is.EqualTo(nameof(NoteCosignedV1)));
        var payload = events[2].Payload as NoteCosignedV1;
        Assert.That(payload!.CosignedDateTime, Is.EqualTo(cosigned));

        TiuDocumentState live = await Note(docId).GetDocumentAsync();
        Assert.That(live.Status, Is.EqualTo("COMPLETED"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task ReplayUntilAsync_AfterSignAndCosign_ReproducesLiveState()
    {
        var (patientId, docId) = await CreateNoteAsync(cosignerId: "PROV-2");

        await Note(docId).SignDocumentAsync(DateTime.UtcNow);
        await Note(docId).CosignDocumentAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);

        Assert.That(replayed.Notes, Has.Count.EqualTo(1));
        TiuDocumentState fromChain = replayed.Notes[0];
        Assert.That(fromChain.DocumentId, Is.EqualTo(docId));
        Assert.That(fromChain.DocumentType, Is.EqualTo("PROGRESS NOTE"));
        Assert.That(fromChain.Status, Is.EqualTo("COMPLETED"));
        Assert.That(fromChain.SignedDateTime, Is.Not.Null);
        Assert.That(fromChain.CosignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task ReplayUntilAsync_PointInTime_BeforeSignature_ShowsUnsigned()
    {
        var (patientId, docId) = await CreateNoteAsync(cosignerId: "PROV-2");

        await Task.Delay(150);
        DateTime tBeforeSign = DateTime.UtcNow;
        await Task.Delay(150);

        await Note(docId).SignDocumentAsync(DateTime.UtcNow);
        await Note(docId).CosignDocumentAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        PatientStateSnapshot before =
            await Stream(patientId).ReplayUntilAsync(tBeforeSign);
        Assert.That(before.Notes[0].Status, Is.EqualTo("UNSIGNED"));
        Assert.That(before.Notes[0].SignedDateTime, Is.Null);

        PatientStateSnapshot after =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(after.Notes[0].Status, Is.EqualTo("COMPLETED"));
    }

    private async Task WaitForStreamVersionAsync(
        string patientId, int expected, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        IPatientClinicalEventStreamGrain stream = Stream(patientId);
        while (DateTime.UtcNow < deadline)
        {
            int v = await stream.GetVersionAsync();
            if (v >= expected) return;
            await Task.Delay(50);
        }
        int finalVersion = await stream.GetVersionAsync();
        Assert.Fail(
            $"Stream for {patientId} did not reach version {expected} within {timeoutMs}ms (current={finalVersion}).");
    }
}
