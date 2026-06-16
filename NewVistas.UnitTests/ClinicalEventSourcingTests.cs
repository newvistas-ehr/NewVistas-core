// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.UnitTests;

/// <summary>
/// End-to-end tests for the clinical event-sourcing pipeline: in-grain outbox →
/// per-patient JournaledGrain stream → hash chain → forensic replay.
///
/// Covers the foundation slice (HashChain, EventEnvelope, IClinicalEvent) plus
/// the first event-sourced domain (PROBLEMS via PatientGrain).
/// </summary>
[TestFixture]
public class ClinicalEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private IPatientGrain Patient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private static ProblemEntry NewProblem(string code = "I10", string status = "ACTIVE") => new()
    {
        ProblemId = $"PROB-{Guid.NewGuid()}",
        Diagnosis = "Hypertension",
        DiagnosisCode = code,
        Status = status,
        DateRecorded = DateTime.UtcNow
    };

    private static ProblemAddedV1 NewProblemAddedEvent(string patientId, ProblemEntry entry) => new()
    {
        EventId = $"CEV-{Guid.NewGuid()}",
        PatientId = patientId,
        OccurredUtc = DateTime.UtcNow,
        UserId = "USR-1",
        UserName = "Smith,Jane",
        ProblemId = entry.ProblemId,
        Snapshot = entry
    };

    // ── HashChain ─────────────────────────────────────────────────────────

    [Test]
    public void HashChain_Compute_IsDeterministic()
    {
        string canonical = "EID-1|PAT-1|PROBLEMS|ProblemAddedV1";
        string hash1 = HashChain.Compute(canonical, HashChain.GenesisHash);
        string hash2 = HashChain.Compute(canonical, HashChain.GenesisHash);
        Assert.That(hash1, Is.EqualTo(hash2));
        Assert.That(hash1, Is.Not.Empty);
    }

    [Test]
    public void HashChain_Compute_DiffersForDifferentPreviousHash()
    {
        string canonical = "EID-1|PAT-1|PROBLEMS|ProblemAddedV1";
        string hash1 = HashChain.Compute(canonical, HashChain.GenesisHash);
        string hash2 = HashChain.Compute(canonical, "OTHER-HASH");
        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public void HashChain_GenesisHash_MatchesAuditChainGenesis()
    {
        // The clinical chain and audit chain must share genesis so a brand-new
        // patient's first event in either chain anchors to the same root.
        Assert.That(HashChain.GenesisHash, Is.EqualTo(IAuditEventGrain.GenesisHash));
    }

    // ── JournaledGrain append + idempotency + Version ─────────────────────

    [Test]
    public async Task Append_AssignsHashChainAndIncrementsVersion()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientClinicalEventStreamGrain stream = Stream(patientId);

        var evt = NewProblemAddedEvent(patientId, NewProblem());
        int versionAfter = await stream.AppendAsync(EventEnvelope.Wrap(evt));

        Assert.That(versionAfter, Is.EqualTo(1));
        Assert.That(await stream.GetVersionAsync(), Is.EqualTo(1));

        IReadOnlyList<EventEnvelope> events = await stream.ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].PreviousEventHash, Is.EqualTo(HashChain.GenesisHash));
        Assert.That(events[0].EventHash, Is.Not.Empty);
        Assert.That(await stream.GetLastEventHashAsync(), Is.EqualTo(events[0].EventHash));
    }

    [Test]
    public async Task Append_Idempotent_OnDuplicateEventId()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientClinicalEventStreamGrain stream = Stream(patientId);

        var evt = NewProblemAddedEvent(patientId, NewProblem());
        EventEnvelope env = EventEnvelope.Wrap(evt);

        int v1 = await stream.AppendAsync(env);
        int v2 = await stream.AppendAsync(env);   // duplicate EventId
        int v3 = await stream.AppendAsync(env);   // and again

        Assert.That(v1, Is.EqualTo(1));
        Assert.That(v2, Is.EqualTo(1));
        Assert.That(v3, Is.EqualTo(1));
        Assert.That(await stream.GetVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Append_MultipleEvents_ChainsHashesAndVersionMonotonic()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientClinicalEventStreamGrain stream = Stream(patientId);

        for (int i = 0; i < 5; i++)
        {
            var evt = NewProblemAddedEvent(patientId, NewProblem(code: $"E11.{i}"));
            int v = await stream.AppendAsync(EventEnvelope.Wrap(evt));
            Assert.That(v, Is.EqualTo(i + 1));
        }

        IReadOnlyList<EventEnvelope> events = await stream.ReadAsync(0, 100);
        Assert.That(events, Has.Count.EqualTo(5));

        string previous = HashChain.GenesisHash;
        foreach (EventEnvelope e in events)
        {
            Assert.That(e.PreviousEventHash, Is.EqualTo(previous));
            previous = e.EventHash;
        }
    }

    // ── VerifyChainAsync ──────────────────────────────────────────────────

    [Test]
    public async Task VerifyChainAsync_ReturnsTrue_ForCleanChain()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientClinicalEventStreamGrain stream = Stream(patientId);

        for (int i = 0; i < 3; i++)
            await stream.AppendAsync(
                EventEnvelope.Wrap(NewProblemAddedEvent(patientId, NewProblem())));

        Assert.That(await stream.VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task VerifyChainAsync_ReturnsTrue_ForEmptyChain()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    // ── ReplayUntilAsync ──────────────────────────────────────────────────

    [Test]
    public async Task ReplayUntilAsync_AfterAddAndInactivate_ReflectsBoth()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientClinicalEventStreamGrain stream = Stream(patientId);

        ProblemEntry entry = NewProblem();
        var added = new ProblemAddedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow.AddMinutes(-10),
            ProblemId = entry.ProblemId,
            Snapshot = entry
        };
        await stream.AppendAsync(EventEnvelope.Wrap(added));

        var inactivated = new ProblemInactivatedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow.AddMinutes(-2),
            ProblemId = entry.ProblemId,
            DateResolved = DateTime.UtcNow.AddMinutes(-2)
        };
        await stream.AppendAsync(EventEnvelope.Wrap(inactivated));

        PatientStateSnapshot now = await stream.ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(now.Problems, Has.Count.EqualTo(1));
        Assert.That(now.Problems[0].Status, Is.EqualTo("INACTIVE"));
    }

    [Test]
    public async Task ReplayUntilAsync_PointInTime_ExcludesLaterEvents()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientClinicalEventStreamGrain stream = Stream(patientId);

        DateTime tEarly = DateTime.UtcNow.AddMinutes(-10);
        DateTime tLate = DateTime.UtcNow.AddMinutes(-1);

        ProblemEntry entry = NewProblem();
        await stream.AppendAsync(EventEnvelope.Wrap(new ProblemAddedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = tEarly,
            ProblemId = entry.ProblemId,
            Snapshot = entry
        }));
        await stream.AppendAsync(EventEnvelope.Wrap(new ProblemInactivatedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = tLate,
            ProblemId = entry.ProblemId,
            DateResolved = tLate
        }));

        // Snapshot taken between the two events — should show ACTIVE.
        PatientStateSnapshot midway =
            await stream.ReplayUntilAsync(tEarly.AddMinutes(1));
        Assert.That(midway.Problems, Has.Count.EqualTo(1));
        Assert.That(midway.Problems[0].Status, Is.EqualTo("ACTIVE"));

        // Snapshot taken after the inactivation — should show INACTIVE.
        PatientStateSnapshot after =
            await stream.ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(after.Problems[0].Status, Is.EqualTo("INACTIVE"));
    }

    // ── PatientGrain emits events ────────────────────────────────────────

    [Test]
    public async Task PatientGrain_AddProblemAsync_EmitsProblemAddedV1ToStream()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        ProblemEntry entry = NewProblem();
        await patient.AddProblemAsync(entry);

        // Drain runs as fire-and-forget after WriteStateAsync; allow it to settle.
        await WaitForStreamVersionAsync(patientId, expected: 1);

        IPatientClinicalEventStreamGrain stream = Stream(patientId);
        IReadOnlyList<EventEnvelope> events = await stream.ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(ProblemAddedV1)));
        Assert.That(events[0].PatientId, Is.EqualTo(patientId));
        Assert.That(events[0].Domain, Is.EqualTo("PROBLEMS"));

        var payload = events[0].Payload as ProblemAddedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ProblemId, Is.EqualTo(entry.ProblemId));
        Assert.That(payload.Snapshot.Diagnosis, Is.EqualTo(entry.Diagnosis));
    }

    [Test]
    public async Task PatientGrain_InactivateProblemAsync_EmitsProblemInactivatedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        ProblemEntry entry = NewProblem();
        await patient.AddProblemAsync(entry);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        DateTime resolved = DateTime.UtcNow;
        await patient.InactivateProblemAsync(entry.ProblemId, resolved);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IPatientClinicalEventStreamGrain stream = Stream(patientId);
        IReadOnlyList<EventEnvelope> events = await stream.ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[1].EventType, Is.EqualTo(nameof(ProblemInactivatedV1)));

        var inactivated = events[1].Payload as ProblemInactivatedV1;
        Assert.That(inactivated, Is.Not.Null);
        Assert.That(inactivated!.ProblemId, Is.EqualTo(entry.ProblemId));

        // Live state on the patient grain reflects the inactivation immediately.
        ProblemEntry? live = await patient.GetProblemAsync(entry.ProblemId);
        Assert.That(live, Is.Not.Null);
        Assert.That(live!.Status, Is.EqualTo("INACTIVE"));

        // Hash chain is intact.
        Assert.That(await stream.VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task PatientGrain_AddProblem_DuplicateProblemId_NoSecondEvent()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientGrain patient = Patient(patientId);

        ProblemEntry entry = NewProblem();
        await patient.AddProblemAsync(entry);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        // Same problem id — should be ignored, no new event.
        await patient.AddProblemAsync(entry);
        await Task.Delay(200);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Wait for the patient's clinical event stream to reach the expected
    /// version. Required because <c>DrainOutboxAsync</c> is fire-and-forget
    /// from the writing grain — the stream append finishes shortly after the
    /// command returns, not synchronously with it.
    /// </summary>
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
