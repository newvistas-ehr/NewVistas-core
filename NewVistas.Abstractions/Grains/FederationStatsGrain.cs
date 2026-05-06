// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Concurrency;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Stateless thin wrapper around <see cref="IOutboxStatistics"/>. Marked
/// <see cref="StatelessWorkerAttribute"/> so multiple admins viewing the
/// dashboard simultaneously don't queue up against a single activation.
///
/// The actual SQL work happens in the <c>SqlOutboxStatistics</c>
/// implementation (silo-side); deployments without an outbox get
/// <see cref="NoOpOutboxStatistics"/> and return
/// <see cref="OutboxStats.NotAvailable"/>.
/// </summary>
[StatelessWorker]
public sealed class FederationStatsGrain : Grain, IFederationStatsGrain
{
    private readonly IOutboxStatistics _outboxStats;

    public FederationStatsGrain(IOutboxStatistics outboxStats)
    {
        _outboxStats = outboxStats;
    }

    public Task<OutboxStats> GetOutboxStatsAsync()
        => _outboxStats.GetAsync(CancellationToken.None);
}
