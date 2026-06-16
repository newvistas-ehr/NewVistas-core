// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Services;

/// <summary>
/// Silo-level singleton implementation of IDrugInteractionCacheService.
///
/// Uses a <c>volatile</c> reference for lock-free reads; Swap takes a short
/// lock only to enforce version monotonicity (two checker activations may
/// race to install snapshots — the newer version must win regardless of
/// arrival order).
///
/// Registered as a singleton in the Orleans silo DI container so all
/// IDrugInteractionCheckerGrain activations on a silo share one instance.
/// </summary>
public sealed class DrugInteractionCacheService : IDrugInteractionCacheService
{
    private readonly object _swapLock = new();

    // volatile: ensures that any thread that reads _snapshot sees the most
    // recently written reference, without a memory barrier on every read.
    private volatile CachedInteractionSnapshot? _snapshot;

    /// <inheritdoc/>
    public void Swap(CachedInteractionSnapshot snapshot)
    {
        lock (_swapLock)
        {
            if (_snapshot is null || snapshot.Version > _snapshot.Version)
                _snapshot = snapshot;
        }
    }

    /// <inheritdoc/>
    public CachedInteractionSnapshot? GetSnapshot() => _snapshot;

    /// <inheritdoc/>
    public bool IsPopulated => _snapshot is not null;
}
