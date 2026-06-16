// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>Singleton nursing unit directory grain — VistA File #210 list.</summary>
public class NursingUnitIndexGrain : Grain, INursingUnitIndexGrain
{
    private readonly IPersistentState<NursingUnitIndexState> _state;

    public NursingUnitIndexGrain(
        [PersistentState("nursingUnitIndexState", "nursingUnitIndexStore")]
        IPersistentState<NursingUnitIndexState> state)
    {
        _state = state;
    }

    public Task<NursingUnitIndexState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task UpsertUnitAsync(NursingUnitEntry entry)
    {
        _state.State.Units.RemoveAll(u => u.UnitId == entry.UnitId);
        _state.State.Units.Add(entry);
        _state.State.Units.Sort(
            (a, b) => string.Compare(a.UnitName, b.UnitName, StringComparison.OrdinalIgnoreCase));
        await _state.WriteStateAsync();
    }

    public async Task RemoveUnitAsync(string unitId)
    {
        _state.State.Units.RemoveAll(u => u.UnitId == unitId);
        await _state.WriteStateAsync();
    }
}
