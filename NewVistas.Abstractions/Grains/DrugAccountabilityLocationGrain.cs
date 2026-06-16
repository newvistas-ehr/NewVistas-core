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
/// Per-location drug inventory index — lightweight balance projections for every
/// drug stocked at this location. Grain key: "DA-LOC:{locationId}"
/// </summary>
public class DrugAccountabilityLocationGrain : Grain, IDrugAccountabilityLocationGrain
{
    private readonly IPersistentState<DrugAccountabilityLocationState> _state;

    public DrugAccountabilityLocationGrain(
        [PersistentState("daLocationState", "daLocationStore")]
        IPersistentState<DrugAccountabilityLocationState> state)
    {
        _state = state;
    }

    public Task<DrugAccountabilityLocationState> GetLocationAsync() =>
        Task.FromResult(_state.State);

    public async Task InitialiseLocationAsync(string locationId, string locationName, string locationType)
    {
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.LocationType = locationType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddOrUpdateDrugAsync(DrugBalanceSummary summary)
    {
        int idx = _state.State.DrugBalances.FindIndex(d => d.DrugId == summary.DrugId);
        if (idx >= 0)
            _state.State.DrugBalances[idx] = summary;
        else
            _state.State.DrugBalances.Add(summary);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<DrugBalanceSummary>> GetAllDrugsAsync() =>
        Task.FromResult(_state.State.DrugBalances
            .OrderBy(d => d.DrugName)
            .ToList());

    public Task<List<DrugBalanceSummary>> GetLowStockDrugsAsync() =>
        Task.FromResult(_state.State.DrugBalances
            .Where(d => d.ReorderPoint.HasValue && d.CurrentBalance <= d.ReorderPoint.Value)
            .OrderBy(d => d.DrugName)
            .ToList());

    public Task<List<DrugBalanceSummary>> GetControlledSubstancesAsync() =>
        Task.FromResult(_state.State.DrugBalances
            .Where(d => d.IsControlled)
            .OrderBy(d => d.DrugName)
            .ToList());

    public async Task ClearAsync()
    {
        _state.State.DrugBalances.Clear();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
