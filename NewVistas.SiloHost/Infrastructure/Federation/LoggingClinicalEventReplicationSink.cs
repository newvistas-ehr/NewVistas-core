// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
