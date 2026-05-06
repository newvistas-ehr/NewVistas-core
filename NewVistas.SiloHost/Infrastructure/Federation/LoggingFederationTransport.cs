// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Placeholder federation transport that logs each batch and reports success.
/// Used while real transports (HTTP, Direct Project, file bundle) are still
/// being built — proves the sink → outbox → drainer plumbing end-to-end.
///
/// Not for production. Real transports return <see cref="TransportResult.Fail"/>
/// when the destination is unreachable; this one always returns
/// <see cref="TransportResult.Ok"/> and so will silently drain the outbox even
/// if no real federation peer exists.
/// </summary>
public sealed class LoggingFederationTransport : IFederationTransport
{
    private readonly ILogger<LoggingFederationTransport> _logger;

    public LoggingFederationTransport(ILogger<LoggingFederationTransport> logger)
    {
        _logger = logger;
    }

    public Task<TransportResult> SendAsync(IReadOnlyList<EventEnvelope> batch, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[federation-transport] would ship {Count} envelope(s); first EventId={FirstEventId} sourceCluster={SourceCluster}",
            batch.Count,
            batch.Count > 0 ? batch[0].EventId : "(empty)",
            batch.Count > 0 ? batch[0].SourceClusterId : "(empty)");
        return Task.FromResult(TransportResult.Ok());
    }
}
