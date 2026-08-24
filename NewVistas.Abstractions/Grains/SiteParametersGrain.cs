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
        // A one-way feature that has been disabled stays disabled. Throwing rather than
        // no-opping is deliberate: a silent failure would leave an administrator believing the
        // feature was back on and its statistics trustworthy, which is the precise outcome the
        // latch exists to prevent.
        if (_state.State.PermanentlyDisabledFeatures.Contains(featureName))
        {
            throw new InvalidOperationException(
                $"Feature '{featureName}' was permanently disabled at this site and cannot be re-enabled. " +
                "Its accumulated statistics have gaps for the period it was off, so resuming would " +
                "report rates computed against an under-counted denominator. Re-enabling would require " +
                "discarding and recomputing every derived counter, which is not supported.");
        }

        if (_state.State.Features.Add(featureName))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task DisableFeatureAsync(string featureName)
        => DisableFeatureAsync(featureName, null, null, null);

    public async Task DisableFeatureAsync(
        string featureName, string? byUserId, string? byUserName, string? reason)
    {
        bool removed = _state.State.Features.Remove(featureName);
        bool tombstoned = false;

        // Record the tombstone even when the feature was already absent from Features — a site
        // could disable it twice, or disable something never enabled, and the caller's intent is
        // the same either way: this must never come back.
        if (SiteFeatures.OneWayDisable.Contains(featureName)
            && _state.State.PermanentlyDisabledFeatures.Add(featureName))
        {
            _state.State.PermanentDisableLog.Add(new PermanentFeatureDisable
            {
                FeatureName = featureName,
                DisabledUtc = DateTime.UtcNow,
                DisabledByUserId = byUserId,
                DisabledByUserName = byUserName,
                Reason = reason
            });
            tombstoned = true;
        }

        if (removed || tombstoned)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsFeatureEnabledAsync(string featureName)
        => Task.FromResult(_state.State.Features.Contains(featureName));

    public Task<bool> IsFeaturePermanentlyDisabledAsync(string featureName)
        => Task.FromResult(_state.State.PermanentlyDisabledFeatures.Contains(featureName));

    public Task<List<PermanentFeatureDisable>> GetPermanentDisableLogAsync()
        => Task.FromResult(_state.State.PermanentDisableLog);
}
