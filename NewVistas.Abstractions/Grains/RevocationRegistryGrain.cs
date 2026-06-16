// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Default implementation of <see cref="IRevocationRegistryGrain"/>. Persisted
/// to <c>revocationRegistryStore</c>. Single activation per cluster (single
/// grain key).
/// </summary>
public sealed class RevocationRegistryGrain : Grain, IRevocationRegistryGrain
{
    private readonly IPersistentState<RevocationRegistryState> _state;

    public RevocationRegistryGrain(
        [PersistentState("revocationRegistry", "revocationRegistryStore")] IPersistentState<RevocationRegistryState> state)
    {
        _state = state;
    }

    public async Task RevokeAsync(string thumbprint, string clusterId, string reason, string revokedByUserId)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
            throw new ArgumentException("Thumbprint is required.", nameof(thumbprint));

        string normalized = NormalizeThumbprint(thumbprint);

        // Idempotent: preserve the original record on re-revoke so forensic
        // timestamps don't drift.
        if (_state.State.Records.Any(r => r.Thumbprint == normalized))
            return;

        _state.State.Records.Add(new RevocationRecord
        {
            Thumbprint = normalized,
            ClusterId = clusterId ?? string.Empty,
            Reason = reason ?? string.Empty,
            RevokedUtc = DateTime.UtcNow,
            RevokedByUserId = revokedByUserId ?? string.Empty,
        });
        await _state.WriteStateAsync();
    }

    public Task<bool> IsRevokedAsync(string thumbprint)
    {
        if (string.IsNullOrEmpty(thumbprint)) return Task.FromResult(false);
        string normalized = NormalizeThumbprint(thumbprint);
        return Task.FromResult(_state.State.Records.Any(r => r.Thumbprint == normalized));
    }

    public Task<IReadOnlyList<RevocationRecord>> GetAllAsync()
    {
        IReadOnlyList<RevocationRecord> snapshot = _state.State.Records
            .OrderBy(r => r.RevokedUtc)
            .ToList();
        return Task.FromResult(snapshot);
    }

    /// <summary>
    /// X509Certificate2.Thumbprint returns uppercase hex with no separators;
    /// we normalize to that format so operator-supplied input matches what
    /// the auth handler computes.
    /// </summary>
    private static string NormalizeThumbprint(string input) =>
        input.Replace(" ", string.Empty).Replace(":", string.Empty).ToUpperInvariant();
}
