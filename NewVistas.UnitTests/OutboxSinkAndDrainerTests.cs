// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainStates;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Serialization;

namespace NewVistas.UnitTests;

/// <summary>
/// Behavioural tests for the SQL outbox sink + drainer, exercised against an
/// in-memory <see cref="IOutboxRepository"/> to avoid requiring SQL Express on
/// the test runner. The SQL implementation is verified at runtime via the
/// schema-application path; these tests cover the orchestration logic that
/// is not SQL-specific.
/// </summary>
[TestFixture]
public class OutboxSinkAndDrainerTests
{
    private static Serializer<EventEnvelope> BuildSerializer()
    {
        // Spin up a minimal Orleans serialization container — enough to round-
        // trip [GenerateSerializer] types without booting a full silo.
        var services = new ServiceCollection();
        services.AddSerializer();
        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<Serializer<EventEnvelope>>();
    }

    private static EventEnvelope NewEnvelope(string patientId, string? eventId = null) =>
        EventEnvelope.Wrap(new ProblemAddedV1
        {
            EventId = eventId ?? $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = "USR-1",
            UserName = "Smith,Jane",
            ProblemId = $"PROB-{Guid.NewGuid()}",
            Snapshot = new ProblemEntry
            {
                ProblemId = "PROB-1",
                Diagnosis = "Hypertension",
                DiagnosisCode = "I10",
                Status = "ACTIVE",
                DateRecorded = DateTime.UtcNow
            }
        }) with
        {
            SourceClusterId = "TEST-CLUSTER",
            EventHash = "deadbeef",
            PreviousEventHash = "0000"
        };

    // ── Sink ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Sink_FreshPublish_InsertsRow()
    {
        var repo = new InMemoryOutboxRepository();
        var sink = new SqlOutboxClinicalEventReplicationSink(
            repo, BuildSerializer(), NullLogger<SqlOutboxClinicalEventReplicationSink>.Instance);

        EventEnvelope env = NewEnvelope("PAT-1");
        await sink.PublishAsync(env, CancellationToken.None);

        Assert.That(repo.Rows, Has.Count.EqualTo(1));
        OutboxRow row = repo.Rows.Values.Single();
        Assert.That(row.EventId, Is.EqualTo(env.EventId));
        Assert.That(row.PatientId, Is.EqualTo("PAT-1"));
        Assert.That(row.SourceClusterId, Is.EqualTo("TEST-CLUSTER"));
        Assert.That(row.EventHash, Is.EqualTo("deadbeef"));
        Assert.That(row.EnvelopeBlob, Is.Not.Empty);
    }

    [Test]
    public async Task Sink_DuplicateEventId_IsNoOp()
    {
        var repo = new InMemoryOutboxRepository();
        var sink = new SqlOutboxClinicalEventReplicationSink(
            repo, BuildSerializer(), NullLogger<SqlOutboxClinicalEventReplicationSink>.Instance);

        EventEnvelope env = NewEnvelope("PAT-1");
        await sink.PublishAsync(env, CancellationToken.None);
        await sink.PublishAsync(env, CancellationToken.None);

        Assert.That(repo.Rows, Has.Count.EqualTo(1));
        Assert.That(repo.InsertAttempts, Is.EqualTo(2));   // both calls reached the repo
        Assert.That(repo.InsertSucceeded, Is.EqualTo(1));  // one was new, one was duplicate
    }

    // ── Drainer ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Drainer_NothingPending_DoesNotCallTransport()
    {
        var repo = new InMemoryOutboxRepository();
        var transport = new SpyTransport();
        OutboxDrainerService drainer = BuildDrainer(repo, transport);

        await drainer.DrainOnceAsync(CancellationToken.None);

        Assert.That(transport.Calls, Is.Empty);
    }

    [Test]
    public async Task Drainer_TransportSucceeds_MarksRowsSent()
    {
        var repo = new InMemoryOutboxRepository();
        Serializer<EventEnvelope> ser = BuildSerializer();
        EventEnvelope env = NewEnvelope("PAT-1");
        await repo.InsertIfNewAsync(ToRow(env, ser), CancellationToken.None);

        var transport = new SpyTransport(returns: TransportResult.Ok());
        OutboxDrainerService drainer = BuildDrainer(repo, transport, serializer: ser);

        await drainer.DrainOnceAsync(CancellationToken.None);

        Assert.That(transport.Calls, Has.Count.EqualTo(1));
        Assert.That(transport.Calls.Single().Single().EventId, Is.EqualTo(env.EventId));
        OutboxRow row = repo.Rows.Values.Single();
        Assert.That(repo.SentEventIds, Has.Member(env.EventId));
        Assert.That(repo.RetryScheduled, Is.Empty);
    }

    [Test]
    public async Task Drainer_TransportFails_SchedulesRetry()
    {
        var repo = new InMemoryOutboxRepository();
        Serializer<EventEnvelope> ser = BuildSerializer();
        EventEnvelope env = NewEnvelope("PAT-1");
        await repo.InsertIfNewAsync(ToRow(env, ser), CancellationToken.None);

        var transport = new SpyTransport(returns: TransportResult.Fail("simulated"));
        OutboxDrainerService drainer = BuildDrainer(repo, transport, serializer: ser);

        await drainer.DrainOnceAsync(CancellationToken.None);

        Assert.That(transport.Calls, Has.Count.EqualTo(1));
        Assert.That(repo.SentEventIds, Is.Empty);
        Assert.That(repo.RetryScheduled, Has.Count.EqualTo(1));
        Assert.That(repo.RetryScheduled.Single().error, Is.EqualTo("simulated"));
    }

    [Test]
    public async Task Drainer_TransportThrows_SchedulesRetry()
    {
        var repo = new InMemoryOutboxRepository();
        Serializer<EventEnvelope> ser = BuildSerializer();
        EventEnvelope env = NewEnvelope("PAT-1");
        await repo.InsertIfNewAsync(ToRow(env, ser), CancellationToken.None);

        var transport = new SpyTransport(throws: new InvalidOperationException("boom"));
        OutboxDrainerService drainer = BuildDrainer(repo, transport, serializer: ser);

        await drainer.DrainOnceAsync(CancellationToken.None);

        Assert.That(repo.SentEventIds, Is.Empty);
        Assert.That(repo.RetryScheduled, Has.Count.EqualTo(1));
        Assert.That(repo.RetryScheduled.Single().error, Does.Contain("boom"));
    }

    [Test]
    public void Drainer_Backoff_IsExponentialAndCapped()
    {
        var options = new OutboxOptions { InitialRetrySeconds = 30, MaxRetrySeconds = 600 };
        OutboxDrainerService drainer = BuildDrainer(
            new InMemoryOutboxRepository(),
            new SpyTransport(),
            options: options);

        Assert.That(drainer.ComputeBackoff(0), Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(drainer.ComputeBackoff(1), Is.EqualTo(TimeSpan.FromSeconds(60)));
        Assert.That(drainer.ComputeBackoff(2), Is.EqualTo(TimeSpan.FromSeconds(120)));
        Assert.That(drainer.ComputeBackoff(3), Is.EqualTo(TimeSpan.FromSeconds(240)));
        Assert.That(drainer.ComputeBackoff(4), Is.EqualTo(TimeSpan.FromSeconds(480)));
        // Cap kicks in.
        Assert.That(drainer.ComputeBackoff(5), Is.EqualTo(TimeSpan.FromSeconds(600)));
        Assert.That(drainer.ComputeBackoff(20), Is.EqualTo(TimeSpan.FromSeconds(600)));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static OutboxDrainerService BuildDrainer(
        IOutboxRepository repo,
        IFederationTransport transport,
        OutboxOptions? options = null,
        Serializer<EventEnvelope>? serializer = null)
        => new(
            repo,
            transport,
            serializer ?? BuildSerializer(),
            Options.Create(options ?? new OutboxOptions()),
            NullLogger<OutboxDrainerService>.Instance);

    private static OutboxRow ToRow(EventEnvelope env, Serializer<EventEnvelope> ser) =>
        new(
            EventId: env.EventId,
            PatientId: env.PatientId,
            Domain: env.Domain,
            EventType: env.EventType,
            OccurredUtc: env.OccurredUtc,
            SourceClusterId: env.SourceClusterId,
            EventHash: env.EventHash,
            PreviousEventHash: env.PreviousEventHash,
            EnvelopeBlob: ser.SerializeToArray(env));

    /// <summary>In-memory <see cref="IOutboxRepository"/> for unit tests.</summary>
    private sealed class InMemoryOutboxRepository : IOutboxRepository
    {
        public ConcurrentDictionary<string, OutboxRow> Rows { get; } = new();
        public ConcurrentBag<string> SentEventIds { get; } = new();
        public ConcurrentBag<(IReadOnlyList<string> ids, string error, TimeSpan retryAfter)> RetryScheduled { get; } = new();
        public int InsertAttempts { get; private set; }
        public int InsertSucceeded { get; private set; }
        private readonly ConcurrentDictionary<string, int> _attempts = new();

        public Task<bool> InsertIfNewAsync(OutboxRow row, CancellationToken cancellationToken)
        {
            InsertAttempts++;
            if (Rows.TryAdd(row.EventId, row))
            {
                InsertSucceeded++;
                _attempts[row.EventId] = 0;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<PendingOutboxEntry>> ReadPendingAsync(int batchSize, CancellationToken cancellationToken)
        {
            IReadOnlyList<PendingOutboxEntry> result = Rows.Values
                .Where(r => !SentEventIds.Contains(r.EventId))
                .Take(batchSize)
                .Select(r => new PendingOutboxEntry(r.EventId, r.EnvelopeBlob, _attempts.GetValueOrDefault(r.EventId, 0)))
                .ToList();
            return Task.FromResult(result);
        }

        public Task MarkSentAsync(IReadOnlyList<string> eventIds, CancellationToken cancellationToken)
        {
            foreach (string id in eventIds) SentEventIds.Add(id);
            return Task.CompletedTask;
        }

        public Task ScheduleRetryAsync(IReadOnlyList<string> eventIds, string error, TimeSpan retryAfter, CancellationToken cancellationToken)
        {
            foreach (string id in eventIds)
            {
                _attempts.AddOrUpdate(id, 1, (_, n) => n + 1);
            }
            RetryScheduled.Add((eventIds, error, retryAfter));
            return Task.CompletedTask;
        }
    }

    /// <summary>Configurable transport that records calls and either returns or throws.</summary>
    private sealed class SpyTransport : IFederationTransport
    {
        public List<IReadOnlyList<EventEnvelope>> Calls { get; } = new();
        private readonly TransportResult _result;
        private readonly Exception? _throws;

        public SpyTransport(TransportResult? returns = null, Exception? throws = null)
        {
            _result = returns ?? TransportResult.Ok();
            _throws = throws;
        }

        public Task<TransportResult> SendAsync(IReadOnlyList<EventEnvelope> batch, CancellationToken cancellationToken)
        {
            Calls.Add(batch);
            if (_throws is not null) throw _throws;
            return Task.FromResult(_result);
        }
    }
}
