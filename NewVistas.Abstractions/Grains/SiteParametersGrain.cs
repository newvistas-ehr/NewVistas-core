// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// System-level site parameters grain.
/// Based on VistA PARAMETER file (#8989.5) and PARAMETER DEFINITION (#8989.51).
/// Singleton grain keyed by facility/site ID (e.g., "SITE:DEFAULT").
/// </summary>
public class SiteParametersGrain : Grain, ISiteParametersGrain
{
    private readonly IPersistentState<SiteParametersState> _state;

    public SiteParametersGrain(
        [PersistentState("siteParametersState", "siteParametersStore")]
        IPersistentState<SiteParametersState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SiteId))
        {
            _state.State.SiteId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<SiteParametersState> GetParametersAsync() => Task.FromResult(_state.State);

    public async Task SetVitalsDisplayCountAsync(int count)
    {
        _state.State.VitalsDisplayCount = count;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<int> GetVitalsDisplayCountAsync() => Task.FromResult(_state.State.VitalsDisplayCount);

    public async Task SetOrdersDisplayCountAsync(int count)
    {
        _state.State.OrdersDisplayCount = count;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<int> GetOrdersDisplayCountAsync() => Task.FromResult(_state.State.OrdersDisplayCount);

    public async Task SetNotesDisplayCountAsync(int count)
    {
        _state.State.NotesDisplayCount = count;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<int> GetNotesDisplayCountAsync() => Task.FromResult(_state.State.NotesDisplayCount);

    public async Task SetRecentItemsDisplayCountAsync(int count)
    {
        _state.State.RecentItemsDisplayCount = count;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<int> GetRecentItemsDisplayCountAsync() => Task.FromResult(_state.State.RecentItemsDisplayCount);

    public async Task SetParameterAsync(string parameterName, string value)
    {
        _state.State.Parameters[parameterName] = value;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<string?> GetParameterAsync(string parameterName)
    {
        _state.State.Parameters.TryGetValue(parameterName, out string? value);
        return Task.FromResult(value);
    }

    // ── Site Feature Flags ────────────────────────────────────────────────

    public Task<HashSet<string>> GetFeaturesAsync()
        => Task.FromResult(_state.State.Features);

    public async Task EnableFeatureAsync(string featureName)
    {
        if (_state.State.Features.Add(featureName))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task DisableFeatureAsync(string featureName)
    {
        if (_state.State.Features.Remove(featureName))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsFeatureEnabledAsync(string featureName)
        => Task.FromResult(_state.State.Features.Contains(featureName));
}
