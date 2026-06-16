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
/// Default implementation of <see cref="IProvisioningTokenIndexGrain"/>.
/// Persisted to <c>provisioningTokenIndexStore</c>. Single activation per
/// cluster.
/// </summary>
public sealed class ProvisioningTokenIndexGrain : Grain, IProvisioningTokenIndexGrain
{
    private readonly IPersistentState<ProvisioningTokenIndexState> _state;

    public ProvisioningTokenIndexGrain(
        [PersistentState("provisioningTokenIndex", "provisioningTokenIndexStore")] IPersistentState<ProvisioningTokenIndexState> state)
    {
        _state = state;
    }

    public async Task AddAsync(string token, string clusterId, DateTime issuedUtc, DateTime expiresUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        // Idempotent: re-adding preserves the original entry, matching the
        // per-token grain's IssueAsync semantics.
        if (_state.State.Tokens.Any(t => t.Token == token))
            return;

        _state.State.Tokens.Add(new ProvisioningTokenSummary
        {
            Token = token,
            ClusterId = clusterId ?? string.Empty,
            IssuedUtc = issuedUtc,
            ExpiresUtc = expiresUtc,
        });
        await _state.WriteStateAsync();
    }

    public async Task MarkConsumedAsync(string token, DateTime consumedUtc, string consumedByThumbprint)
    {
        if (string.IsNullOrEmpty(token)) return;

        int idx = _state.State.Tokens.FindIndex(t => t.Token == token);
        if (idx < 0) return;  // not in index; nothing to update

        _state.State.Tokens[idx] = _state.State.Tokens[idx] with
        {
            ConsumedUtc = consumedUtc,
            ConsumedByThumbprint = consumedByThumbprint,
        };
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<ProvisioningTokenSummary>> GetAllAsync()
    {
        IReadOnlyList<ProvisioningTokenSummary> snapshot = _state.State.Tokens
            .OrderByDescending(t => t.IssuedUtc)
            .ToList();
        return Task.FromResult(snapshot);
    }
}
