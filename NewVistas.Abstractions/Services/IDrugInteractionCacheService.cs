// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Silo-level singleton cache for drug-drug interaction pairs.
///
/// Implements the immutable snapshot swap pattern: the entire interaction
/// dictionary is replaced atomically via <see cref="Swap"/>, so readers
/// always see a consistent, fully-populated snapshot without any locking.
/// Reads are lock-free because <c>volatile</c> guarantees visibility.
///
/// Lifecycle:
///   1. IDrugInteractionDatasetGrain calls Swap on activation and after each load.
///   2. IDrugInteractionCheckerGrain calls GetSnapshot on every check request.
///   3. If the silo restarts before the dataset grain activates, the cache
///      is empty and IsCacheReady returns false — callers should handle this.
/// </summary>
public interface IDrugInteractionCacheService
{
    /// <summary>
    /// Atomically replaces the current snapshot with a new interaction dictionary.
    /// Thread-safe; called by IDrugInteractionDatasetGrain only.
    /// </summary>
    void Swap(IReadOnlyDictionary<string, DrugInteractionPair> snapshot);

    /// <summary>
    /// Returns the current snapshot. The reference is immutable from the
    /// caller's perspective — the dictionary will not change after this call
    /// returns. Safe to read from multiple threads without locking.
    /// </summary>
    IReadOnlyDictionary<string, DrugInteractionPair> GetSnapshot();

    /// <summary>
    /// True when the cache contains at least one interaction pair.
    /// </summary>
    bool IsPopulated { get; }
}
