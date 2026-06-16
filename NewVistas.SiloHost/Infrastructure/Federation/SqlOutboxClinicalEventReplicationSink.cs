// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using Orleans.Serialization;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// <see cref="IClinicalEventReplicationSink"/> that persists each envelope to
/// the per-cluster <c>FederationOutbox</c> SQL table. The
/// <see cref="OutboxDrainerService"/> picks rows up from there.
///
/// Idempotent on <see cref="EventEnvelope.EventId"/> via the repository's
/// <c>InsertIfNewAsync</c> — a duplicate publish (e.g. from a crash retry) is a
/// no-op.
/// </summary>
public sealed class SqlOutboxClinicalEventReplicationSink : IClinicalEventReplicationSink
{
    private readonly IOutboxRepository _repository;
    private readonly Serializer<EventEnvelope> _serializer;
    private readonly ILogger<SqlOutboxClinicalEventReplicationSink> _logger;

    public SqlOutboxClinicalEventReplicationSink(
        IOutboxRepository repository,
        Serializer<EventEnvelope> serializer,
        ILogger<SqlOutboxClinicalEventReplicationSink> logger)
    {
        _repository = repository;
        _serializer = serializer;
        _logger = logger;
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        // Orleans-serialized binary blob — preserves polymorphic Payload faithfully.
        // The transport (next plan) is responsible for any wire-format translation
        // when shipping to non-Orleans receivers.
        byte[] blob = _serializer.SerializeToArray(envelope);

        var row = new OutboxRow(
            EventId: envelope.EventId,
            PatientId: envelope.PatientId,
            Domain: envelope.Domain,
            EventType: envelope.EventType,
            OccurredUtc: envelope.OccurredUtc,
            SourceClusterId: envelope.SourceClusterId,
            EventHash: envelope.EventHash,
            PreviousEventHash: envelope.PreviousEventHash,
            EnvelopeBlob: blob);

        bool inserted = await _repository.InsertIfNewAsync(row, cancellationToken);
        if (!inserted)
        {
            _logger.LogDebug(
                "Outbox already contains EventId {EventId} — duplicate publish ignored.",
                envelope.EventId);
        }
    }
}
