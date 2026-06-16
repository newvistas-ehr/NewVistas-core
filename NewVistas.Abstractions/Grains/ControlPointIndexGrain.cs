// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ControlPointIndexGrain : Grain, IControlPointIndexGrain
{
    private readonly IPersistentState<ControlPointIndexState> _state;

    public ControlPointIndexGrain(
        [PersistentState("controlPointIndexState", "ifcapControlPointIndexStore")]
        IPersistentState<ControlPointIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ControlPointIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.ControlPointId == entry.ControlPointId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ControlPointIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<ControlPointIndexEntry>> GetByFiscalYearAsync(int fiscalYear)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.FiscalYear == fiscalYear)
            .ToList());

    public Task<List<ControlPointIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == ControlPointStatus.Active)
            .ToList());
}
