// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Mpi;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Outbox-backed <see cref="IMpiFederationAnnouncer"/> for federated multi-
/// facility deployments. Wraps each MPI event in an
/// <see cref="EventEnvelope"/> tagged with <see cref="MpiPatientRegisteredV1.MpiDomain"/>
/// and publishes through the existing
/// <see cref="IClinicalEventReplicationSink"/> — same outbox table, same
/// drainer, same transport (HTTP/mTLS or sneakernet bundle) as clinical
/// events.
///
/// <para>
/// On the receiving cluster, <see cref="FederationInboundApplier"/>
/// dispatches on <c>envelope.Domain == "MPI"</c> to <see cref="IMpiInboundHandler"/>
/// instead of the per-patient clinical event stream.
/// </para>
///
/// <para>
/// Stamps <see cref="EventEnvelope.SourceClusterId"/> directly (clinical
/// envelopes get this from <c>PatientClinicalEventStreamGrain.AppendAsync</c>;
/// MPI events bypass that stream, so we set it here from
/// <see cref="IClusterIdentity.LocalClusterId"/>). Hash-chain fields stay
/// empty because MPI events don't participate in a per-patient hash chain
/// — each MPI event is a self-contained announcement, not part of a
/// causal clinical sequence.
/// </para>
/// </summary>
public sealed class OutboxMpiFederationAnnouncer : IMpiFederationAnnouncer
{
    private readonly IClinicalEventReplicationSink _sink;
    private readonly IClusterIdentity _clusterIdentity;
    private readonly ILogger<OutboxMpiFederationAnnouncer> _logger;

    public OutboxMpiFederationAnnouncer(
        IClinicalEventReplicationSink sink,
        IClusterIdentity clusterIdentity,
        ILogger<OutboxMpiFederationAnnouncer> logger)
    {
        _sink = sink;
        _clusterIdentity = clusterIdentity;
        _logger = logger;
    }

    public async Task AnnouncePatientRegisteredAsync(MpiSearchEntry searchEntry, string originatingFacilityId)
    {
        var evt = new MpiPatientRegisteredV1
        {
            EventId = $"MPI-REG-{Guid.NewGuid()}",
            PatientId = searchEntry.Icn,
            OccurredUtc = DateTime.UtcNow,
            PatientName = searchEntry.PatientName,
            Ssn = searchEntry.Ssn,
            DateOfBirth = searchEntry.DateOfBirth,
            Sex = searchEntry.Sex,
            OriginatingFacilityId = originatingFacilityId,
        };
        EventEnvelope envelope = EventEnvelope.Wrap(evt) with
        {
            SourceClusterId = _clusterIdentity.LocalClusterId,
        };

        await _sink.PublishAsync(envelope, CancellationToken.None);
        _logger.LogInformation(
            "MPI federation: announced patient registration for ICN {Icn} from {Facility}.",
            searchEntry.Icn, originatingFacilityId);
    }

    public async Task AnnouncePatientMergedAsync(string sourceIcn, string targetIcn, string originatingFacilityId)
    {
        var evt = new MpiPatientMergedV1
        {
            EventId = $"MPI-MRG-{Guid.NewGuid()}",
            PatientId = sourceIcn,
            OccurredUtc = DateTime.UtcNow,
            SourceIcn = sourceIcn,
            TargetIcn = targetIcn,
            OriginatingFacilityId = originatingFacilityId,
        };
        EventEnvelope envelope = EventEnvelope.Wrap(evt) with
        {
            SourceClusterId = _clusterIdentity.LocalClusterId,
        };

        await _sink.PublishAsync(envelope, CancellationToken.None);
        _logger.LogInformation(
            "MPI federation: announced patient merge {SourceIcn} -> {TargetIcn} from {Facility}.",
            sourceIcn, targetIcn, originatingFacilityId);
    }
}
