// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Immutable;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// In-memory <see cref="IRevocationCache"/> backed by an
/// <see cref="ImmutableHashSet{T}"/> swap on refresh. Reads are lock-free
/// (an atomic field load); writes happen only when the refresh service
/// pulls a new snapshot.
///
/// Source of truth is the singleton <see cref="IRevocationRegistryGrain"/>
/// keyed by <see cref="IRevocationRegistryGrain.GlobalKey"/>.
/// </summary>
public sealed class InMemoryRevocationCache : IRevocationCache
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InMemoryRevocationCache> _logger;

    private ImmutableHashSet<string> _revoked = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.OrdinalIgnoreCase);

    public InMemoryRevocationCache(IGrainFactory grainFactory, ILogger<InMemoryRevocationCache> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public bool IsRevoked(string thumbprint)
    {
        if (string.IsNullOrEmpty(thumbprint)) return false;
        // Normalize like the grain does — uppercase, no whitespace/colons.
        string normalized = thumbprint.Replace(" ", string.Empty).Replace(":", string.Empty).ToUpperInvariant();
        return _revoked.Contains(normalized);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IRevocationRegistryGrain grain = _grainFactory.GetGrain<IRevocationRegistryGrain>(IRevocationRegistryGrain.GlobalKey);
        IReadOnlyList<RevocationRecord> records = await grain.GetAllAsync();

        var snapshot = ImmutableHashSet<string>.Empty
            .WithComparer(StringComparer.OrdinalIgnoreCase)
            .Union(records.Select(r => r.Thumbprint));

        Interlocked.Exchange(ref _revoked, snapshot);
        _logger.LogDebug("Revocation cache refreshed: {Count} thumbprint(s).", snapshot.Count);
    }
}
