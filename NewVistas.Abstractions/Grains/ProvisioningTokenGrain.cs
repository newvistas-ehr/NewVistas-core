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
/// Default implementation of <see cref="IProvisioningTokenGrain"/>. Persisted
/// to <c>provisioningTokenStore</c>. Grain key is the token string; one
/// activation per token.
/// </summary>
public sealed class ProvisioningTokenGrain : Grain, IProvisioningTokenGrain
{
    private readonly IPersistentState<ProvisioningTokenState> _state;

    public ProvisioningTokenGrain(
        [PersistentState("provisioningToken", "provisioningTokenStore")] IPersistentState<ProvisioningTokenState> state)
    {
        _state = state;
    }

    public async Task IssueAsync(string clusterId, DateTime expiresUtc)
    {
        if (string.IsNullOrWhiteSpace(clusterId))
            throw new ArgumentException("Cluster id is required.", nameof(clusterId));

        if (_state.State.IsIssued)
            throw new InvalidOperationException("Token has already been issued.");

        _state.State.ClusterId = clusterId;
        _state.State.IssuedUtc = DateTime.UtcNow;
        _state.State.ExpiresUtc = expiresUtc;
        _state.State.IsIssued = true;
        await _state.WriteStateAsync();
    }

    public async Task ConsumeAsync(string requestedClusterId, string issuedCertThumbprint)
    {
        if (!_state.State.IsIssued)
            throw new InvalidOperationException("Unknown token.");
        if (_state.State.ConsumedUtc is not null)
            throw new InvalidOperationException("Token has already been consumed.");
        if (DateTime.UtcNow >= _state.State.ExpiresUtc)
            throw new InvalidOperationException("Token has expired.");
        if (!string.Equals(_state.State.ClusterId, requestedClusterId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Token is bound to cluster '{_state.State.ClusterId}'; CSR requested '{requestedClusterId}'.");
        }

        _state.State.ConsumedUtc = DateTime.UtcNow;
        _state.State.ConsumedByThumbprint = issuedCertThumbprint;
        await _state.WriteStateAsync();
    }

    public Task<ProvisioningTokenState> GetStateAsync() => Task.FromResult(_state.State);
}
