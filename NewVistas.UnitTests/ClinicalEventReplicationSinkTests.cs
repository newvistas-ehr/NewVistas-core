// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Behavioural tests for the federation seam: <see cref="IClinicalEventReplicationSink"/>
/// is invoked from <c>PatientClinicalEventStreamGrain.AppendAsync</c> after
/// <c>ConfirmEvents</c> on fresh appends only, and a sink failure does not fail
/// the append.
///
/// Uses its own <see cref="TestCluster"/> (not <see cref="SharedCluster"/>) so a
/// spy sink can be wired in via <see cref="ISiloConfigurator"/>.
/// </summary>
[TestFixture, NonParallelizable]
public class ClinicalEventReplicationSinkTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        SpyReplicationSink.Reset();
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<SpySinkSiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    [SetUp]
    public void ResetSpy() => SpyReplicationSink.Reset();

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private static EventEnvelope NewProblemAddedEnvelope(string patientId)
    {
        var entry = new ProblemEntry
        {
            ProblemId = $"PROB-{Guid.NewGuid()}",
            Diagnosis = "Hypertension",
            DiagnosisCode = "I10",
            Status = "ACTIVE",
            DateRecorded = DateTime.UtcNow
        };
        var problemAdded = new ProblemAddedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = "USR-1",
            UserName = "Smith,Jane",
            ProblemId = entry.ProblemId,
            Snapshot = entry
        };
        return EventEnvelope.Wrap(problemAdded);
    }

    [Test]
    public async Task Append_FreshEvent_InvokesSinkOnce_WithSealedEnvelope()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope envelope = NewProblemAddedEnvelope(patientId);

        int version = await Stream(patientId).AppendAsync(envelope);

        Assert.That(version, Is.EqualTo(1));
        Assert.That(SpyReplicationSink.Captured, Has.Count.EqualTo(1));

        EventEnvelope captured = SpyReplicationSink.Captured.Single();
        Assert.That(captured.EventId, Is.EqualTo(envelope.EventId));
        // Sink receives the SEALED envelope — hash chain populated by the grain.
        Assert.That(captured.PreviousEventHash, Is.EqualTo(HashChain.GenesisHash));
        Assert.That(captured.EventHash, Is.Not.Empty);
        Assert.That(captured.EventHash, Is.Not.EqualTo(envelope.EventHash),
            "Caller-supplied hash should be replaced by the grain's computed one.");
        // Source cluster id is stamped from IClusterIdentity on a fresh write.
        Assert.That(captured.SourceClusterId, Is.EqualTo(TestClusterId));
    }

    [Test]
    public async Task Append_FreshEvent_StampsLocalClusterId_FromIdentity()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope envelope = NewProblemAddedEnvelope(patientId);

        // Caller did not set SourceClusterId — Wrap() leaves it empty.
        Assert.That(envelope.SourceClusterId, Is.Empty);

        await Stream(patientId).AppendAsync(envelope);

        IReadOnlyList<EventEnvelope> persisted = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(persisted, Has.Count.EqualTo(1));
        Assert.That(persisted[0].SourceClusterId, Is.EqualTo(TestClusterId));
    }

    [Test]
    public async Task Append_PreservesCallerProvidedSourceClusterId()
    {
        // Inbound replication-applier path: an envelope arriving from another
        // cluster carries that cluster's id, and the local grain must NOT
        // overwrite it. Without this guarantee, a replicated chain would lose
        // its origin attribution.
        const string upstreamClusterId = "UPSTREAM-CLUSTER";

        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope envelope = NewProblemAddedEnvelope(patientId)
            with { SourceClusterId = upstreamClusterId };

        await Stream(patientId).AppendAsync(envelope);

        IReadOnlyList<EventEnvelope> persisted = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(persisted, Has.Count.EqualTo(1));
        Assert.That(persisted[0].SourceClusterId, Is.EqualTo(upstreamClusterId));
        Assert.That(persisted[0].SourceClusterId, Is.Not.EqualTo(TestClusterId),
            "Replicated envelope must keep its upstream cluster id, not be re-attributed locally.");
    }

    [Test]
    public async Task VerifyChainAsync_AfterAppendingEvents_RemainsValid()
    {
        // Confirm the new canonicalization (with SourceClusterId + 'v1' prefix)
        // produces a consistent chain end-to-end. Belt-and-braces: the existing
        // event-sourcing test suites already cover this implicitly, but we
        // assert it explicitly here so a regression in the canonical form is
        // caught next to the change that introduces it.
        string patientId = $"PAT-{Guid.NewGuid()}";

        for (int i = 0; i < 5; i++)
        {
            await Stream(patientId).AppendAsync(NewProblemAddedEnvelope(patientId));
        }

        bool ok = await Stream(patientId).VerifyChainAsync();
        Assert.That(ok, Is.True);
    }

    [Test]
    public void Canonicalize_DiffersWhenSourceClusterIdDiffers()
    {
        EventEnvelope a = EventEnvelope.Wrap(new ProblemAddedV1
        {
            EventId = "EID-A",
            PatientId = "PAT-1",
            OccurredUtc = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc),
            ProblemId = "PROB-1",
            Snapshot = new ProblemEntry { ProblemId = "PROB-1", Diagnosis = "X" }
        }) with { SourceClusterId = "CLUSTER-A" };

        EventEnvelope b = a with { SourceClusterId = "CLUSTER-B" };

        Assert.That(a.Canonicalize(), Is.Not.EqualTo(b.Canonicalize()),
            "Source cluster identity must be tamper-protected by the chain.");
    }

    [Test]
    public async Task Append_DuplicateEventId_DoesNotInvokeSink()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope envelope = NewProblemAddedEnvelope(patientId);

        await Stream(patientId).AppendAsync(envelope);
        SpyReplicationSink.Reset();

        // Second append with the same EventId is a no-op at the grain level.
        int version = await Stream(patientId).AppendAsync(envelope);

        Assert.That(version, Is.EqualTo(1));
        Assert.That(SpyReplicationSink.Captured, Is.Empty,
            "Duplicate-EventId append must not republish to the sink.");
    }

    [Test]
    public async Task Append_SinkThrows_AppendStillSucceeds_EventIsDurable()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope envelope = NewProblemAddedEnvelope(patientId);

        SpyReplicationSink.ThrowOnNextPublish = true;

        // The sink throws, but AppendAsync must NOT propagate the exception —
        // the event is already durable in the JournaledGrain log.
        int version = await Stream(patientId).AppendAsync(envelope);

        Assert.That(version, Is.EqualTo(1));
        IReadOnlyList<EventEnvelope> persisted = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(persisted, Has.Count.EqualTo(1));
        Assert.That(persisted[0].EventId, Is.EqualTo(envelope.EventId));
    }

    // ── Configuration plumbing ───────────────────────────────────────────────

    private sealed class SpySinkSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");

            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, SpyReplicationSink>();
            siloBuilder.Services.AddSingleton<IClusterIdentity>(new StaticClusterIdentity(TestClusterId, "099"));
        }
    }

    /// <summary>The cluster identity registered into the spy fixture's silo.</summary>
    public const string TestClusterId = "SINK-TEST-CLUSTER";

    /// <summary>
    /// Test sink that records every published envelope and can be told to throw.
    /// Static state keeps the test code and the DI-resolved sink instance in sync
    /// without needing direct ServiceProvider access on the test cluster.
    /// </summary>
    private sealed class SpyReplicationSink : IClinicalEventReplicationSink
    {
        private static readonly ConcurrentBag<EventEnvelope> _captured = new();

        public static IReadOnlyCollection<EventEnvelope> Captured => _captured;

        public static bool ThrowOnNextPublish { get; set; }

        public static void Reset()
        {
            _captured.Clear();
            ThrowOnNextPublish = false;
        }

        public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
        {
            if (ThrowOnNextPublish)
            {
                ThrowOnNextPublish = false;
                throw new InvalidOperationException("simulated sink failure");
            }
            _captured.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
