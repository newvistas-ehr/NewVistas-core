// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// An immutable, versioned snapshot of the interaction dataset held by the
/// silo-local cache. Version corresponds to DrugInteractionDatasetState.Version
/// at the time the snapshot was pulled.
/// </summary>
public sealed record CachedInteractionSnapshot(
    long Version,
    IReadOnlyDictionary<string, DrugInteractionPair> Pairs);

/// <summary>
/// Silo-level singleton cache for drug-drug interaction pairs.
///
/// Populated PULL-THROUGH by IDrugInteractionCheckerGrain activations: on each
/// check the checker validates the cached Version against the DI-DATASET grain
/// and pulls a fresh snapshot only when missing or stale. Because the checker
/// runs on whatever silo received the call, every silo self-populates — unlike
/// the previous push model, where only the silo hosting the dataset grain ever
/// had a populated cache.
///
/// Implements the immutable snapshot swap pattern: readers always see a
/// consistent, fully-populated snapshot without locking.
/// </summary>
public interface IDrugInteractionCacheService
{
    /// <summary>
    /// Atomically installs a new snapshot. Version-monotonic: a snapshot with
    /// a version lower than or equal to the current one is ignored, so
    /// concurrent checker pulls cannot regress the cache.
    /// </summary>
    void Swap(CachedInteractionSnapshot snapshot);

    /// <summary>
    /// Returns the current snapshot, or null if no snapshot has been pulled
    /// on this silo yet. The returned snapshot is immutable.
    /// </summary>
    CachedInteractionSnapshot? GetSnapshot();

    /// <summary>True when a snapshot has been installed on this silo.</summary>
    bool IsPopulated { get; }
}
