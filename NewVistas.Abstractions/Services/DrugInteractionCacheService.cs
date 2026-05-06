// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Silo-level singleton implementation of IDrugInteractionCacheService.
///
/// Uses a <c>volatile</c> reference to implement a lock-free immutable swap:
/// writers replace the entire dictionary reference in one atomic store,
/// readers always observe either the old or new complete dictionary, never
/// a partially-updated state.
///
/// Registered as a singleton in the Orleans silo DI container so that both
/// IDrugInteractionDatasetGrain and IDrugInteractionCheckerGrain share the
/// same instance within a silo.
/// </summary>
public sealed class DrugInteractionCacheService : IDrugInteractionCacheService
{
    // volatile: ensures that any thread that reads _snapshot sees the most
    // recently written reference, without a memory barrier on every read.
    private volatile IReadOnlyDictionary<string, DrugInteractionPair> _snapshot =
        new Dictionary<string, DrugInteractionPair>();

    /// <inheritdoc/>
    public void Swap(IReadOnlyDictionary<string, DrugInteractionPair> snapshot)
    {
        _snapshot = snapshot;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, DrugInteractionPair> GetSnapshot() => _snapshot;

    /// <inheritdoc/>
    public bool IsPopulated => _snapshot.Count > 0;
}
