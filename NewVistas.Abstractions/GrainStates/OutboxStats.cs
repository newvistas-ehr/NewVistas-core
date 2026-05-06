// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Aggregate statistics for the federation outbox table. Computed on
/// demand by <see cref="GrainInterfaces.IFederationStatsGrain"/>; surfaced
/// by the security dashboard.
///
/// All counts are point-in-time snapshots; nothing here is persisted.
/// </summary>
[GenerateSerializer]
public sealed record OutboxStats(
    [property: Id(0)] int Pending,
    [property: Id(1)] int Sent,
    [property: Id(2)] DateTime? OldestPendingUtc,
    [property: Id(3)] int MaxAttemptsOnPending,
    [property: Id(4)] DateTime? LastSentUtc,
    [property: Id(5)] bool Available)
{
    /// <summary>Returned by deployments that don't run a federation outbox.</summary>
    public static OutboxStats NotAvailable { get; } = new(0, 0, null, 0, null, false);
}
