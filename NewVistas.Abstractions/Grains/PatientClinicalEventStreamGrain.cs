// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient append-only clinical event stream, implemented on Orleans
/// <see cref="JournaledGrain{TGrainState, TEventBase}"/>.
///
/// The framework owns the durable event log via the
/// <c>ClinicalLogConsistency</c> log-consistency provider (LogStorage-based,
/// so the events themselves are persisted — not just a derived state). The
/// projection state (<see cref="PatientStateSnapshot"/>) is rebuilt on
/// activation by replaying every confirmed envelope through
/// <see cref="TransitionState"/>.
///
/// Hash chain (§170.315(d)(2)) is computed at append time and verified by
/// <see cref="VerifyChainAsync"/>.
/// </summary>
[LogConsistencyProvider(ProviderName = "ClinicalLogConsistency")]
[StorageProvider(ProviderName = "patientClinicalStreamStore")]
public class PatientClinicalEventStreamGrain
    : JournaledGrain<PatientStateSnapshot, EventEnvelope>,
      IPatientClinicalEventStreamGrain
{
    private readonly IClinicalEventReplicationSink _replicationSink;
    private readonly IClusterIdentity _clusterIdentity;
    private readonly ILogger<PatientClinicalEventStreamGrain> _logger;

    public PatientClinicalEventStreamGrain(
        IClinicalEventReplicationSink replicationSink,
        IClusterIdentity clusterIdentity,
        ILogger<PatientClinicalEventStreamGrain> logger)
    {
        _replicationSink = replicationSink;
        _clusterIdentity = clusterIdentity;
        _logger = logger;
    }

    public async Task<int> AppendAsync(EventEnvelope envelope)
    {
        // Idempotent: a duplicate EventId from an outbox retry is a no-op.
        if (State.HasEventId(envelope.EventId))
            return Version;

        // Stamp hash-chain fields (overwriting whatever the caller passed in —
        // the chain is owned here, not by the writing grain) and, on a fresh
        // local write, the source cluster id. An envelope that arrives with
        // SourceClusterId already set is a replicated write from another cluster;
        // preserve the upstream value so cross-cluster attribution is honest.
        string previousHash = State.LastEventHash;
        string sourceClusterId = string.IsNullOrEmpty(envelope.SourceClusterId)
            ? _clusterIdentity.LocalClusterId
            : envelope.SourceClusterId;
        EventEnvelope unhashed = envelope with
        {
            PreviousEventHash = previousHash,
            EventHash = string.Empty,
            SourceClusterId = sourceClusterId
        };
        string eventHash = HashChain.Compute(unhashed.Canonicalize(), previousHash);

        EventEnvelope sealed_ = unhashed with { EventHash = eventHash };

        RaiseEvent(sealed_);
        await ConfirmEvents();

        // Post-confirm replication: the event is already legally durable, so a
        // sink failure here must not fail the append. Higher-level retry/drainer
        // infrastructure (future plan) is responsible for at-least-once recovery.
        try
        {
            await _replicationSink.PublishAsync(sealed_, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Replication sink failed for envelope {EventId} (patient {PatientId}); event is durable in the legal log but did not replicate.",
                sealed_.EventId, sealed_.PatientId);
        }

        return Version;
    }

    public async Task<IReadOnlyList<EventEnvelope>> ReadAsync(int fromSequence, int max)
    {
        if (fromSequence < 0) fromSequence = 0;
        if (max <= 0) return Array.Empty<EventEnvelope>();

        int toExclusive = Math.Min(Version, fromSequence + max);
        if (fromSequence >= toExclusive) return Array.Empty<EventEnvelope>();

        IReadOnlyList<EventEnvelope> events =
            await RetrieveConfirmedEvents(fromSequence, toExclusive);
        return events;
    }

    public async Task<IReadOnlyList<EventEnvelope>> ReadByDomainAsync(
        string domain, DateTime fromUtc, DateTime toUtc)
    {
        if (Version == 0) return Array.Empty<EventEnvelope>();

        IReadOnlyList<EventEnvelope> all = await RetrieveConfirmedEvents(0, Version);
        var filtered = new List<EventEnvelope>();
        foreach (EventEnvelope e in all)
        {
            if (!string.Equals(e.Domain, domain, StringComparison.Ordinal)) continue;
            if (e.OccurredUtc < fromUtc || e.OccurredUtc > toUtc) continue;
            filtered.Add(e);
        }
        return filtered;
    }

    public async Task<PatientStateSnapshot> ReplayUntilAsync(
        DateTime asOfUtc, string? domainFilter = null)
    {
        var snapshot = new PatientStateSnapshot();
        if (Version == 0) return snapshot;

        IReadOnlyList<EventEnvelope> all = await RetrieveConfirmedEvents(0, Version);
        foreach (EventEnvelope e in all)
        {
            if (e.OccurredUtc > asOfUtc) continue;
            if (domainFilter is not null &&
                !string.Equals(e.Domain, domainFilter, StringComparison.Ordinal)) continue;
            snapshot.Apply(e);
        }
        return snapshot;
    }

    public async Task<bool> VerifyChainAsync()
    {
        if (Version == 0) return true;

        IReadOnlyList<EventEnvelope> all = await RetrieveConfirmedEvents(0, Version);
        string previousHash = HashChain.GenesisHash;

        foreach (EventEnvelope e in all)
        {
            if (!string.Equals(e.PreviousEventHash, previousHash, StringComparison.Ordinal))
                return false;

            EventEnvelope unhashed = e with { EventHash = string.Empty };
            string recomputed = HashChain.Compute(unhashed.Canonicalize(), previousHash);
            if (!string.Equals(recomputed, e.EventHash, StringComparison.Ordinal))
                return false;

            previousHash = e.EventHash;
        }
        return true;
    }

    public Task<int> GetVersionAsync() => Task.FromResult(Version);
    public Task<string> GetLastEventHashAsync() => Task.FromResult(State.LastEventHash);

    /// <summary>
    /// Apply a confirmed envelope to the projection. Called by the framework
    /// after each <c>RaiseEvent</c> and during activation log replay.
    /// </summary>
    protected override void TransitionState(PatientStateSnapshot state, EventEnvelope @event)
        => state.Apply(@event);
}
