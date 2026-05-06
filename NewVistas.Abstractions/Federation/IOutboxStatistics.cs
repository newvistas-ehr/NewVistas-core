// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Silo-side service that produces an <see cref="OutboxStats"/> snapshot
/// on demand. The default implementation runs SQL aggregate queries
/// (lives in <c>NewVistas.SiloHost</c> alongside the rest of the SQL
/// outbox plumbing); a no-op fallback is registered for deployments
/// without an outbox so the stats grain can resolve cleanly everywhere.
/// </summary>
public interface IOutboxStatistics
{
    Task<OutboxStats> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Returned by deployments without an outbox configured.</summary>
public sealed class NoOpOutboxStatistics : IOutboxStatistics
{
    public Task<OutboxStats> GetAsync(CancellationToken cancellationToken)
        => Task.FromResult(OutboxStats.NotAvailable);
}
