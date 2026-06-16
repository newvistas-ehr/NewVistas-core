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
/// DSS Unit Index Grain — application-wide searchable index of DSS unit definitions.
/// Singleton key: "EC-DSS-IDX".
/// Persists to "dssUnitIndexStore".
/// </summary>
public class DssUnitIndexGrain : Grain, IDssUnitIndexGrain
{
    private readonly IPersistentState<DssUnitIndexState> _state;

    public DssUnitIndexGrain(
        [PersistentState("dssUnitIndexState", "dssUnitIndexStore")]
        IPersistentState<DssUnitIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(DssUnitIndexEntry entry)
    {
        int idx = _state.State.Units.FindIndex(u => u.DssUnitId == entry.DssUnitId);
        if (idx >= 0)
            _state.State.Units[idx] = entry;
        else
            _state.State.Units.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<DssUnitIndexEntry>> SearchAsync(string? searchText, bool activeOnly, int maxResults)
    {
        IEnumerable<DssUnitIndexEntry> query = _state.State.Units;

        if (activeOnly)
            query = query.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string lower = searchText.ToLowerInvariant();
            query = query.Where(u =>
                u.UnitName.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                u.UnitCode.Contains(lower, StringComparison.OrdinalIgnoreCase));
        }

        List<DssUnitIndexEntry> result = query
            .OrderBy(u => u.UnitName)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<List<DssUnitIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Units.OrderBy(u => u.UnitName).ToList());
}
