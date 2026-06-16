// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Replication sink that simply logs each envelope at Information level.
/// Wired into <see cref="Profiles.LocalhostDevProfile"/> so developers can see
/// what would be replicated before any real transport exists.
///
/// Not for production — sustained event volume would flood logs.
/// </summary>
public sealed class LoggingClinicalEventReplicationSink : IClinicalEventReplicationSink
{
    private readonly ILogger<LoggingClinicalEventReplicationSink> _logger;

    public LoggingClinicalEventReplicationSink(ILogger<LoggingClinicalEventReplicationSink> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[replication-sink] would publish EventId={EventId} Patient={PatientId} Domain={Domain} Type={EventType} OccurredUtc={OccurredUtc:O}",
            envelope.EventId,
            envelope.PatientId,
            envelope.Domain,
            envelope.EventType,
            envelope.OccurredUtc);
        return Task.CompletedTask;
    }
}
