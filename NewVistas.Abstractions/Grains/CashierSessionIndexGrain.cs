// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CashierSessionIndexGrain : Grain, ICashierSessionIndexGrain
{
    private readonly IPersistentState<CashierSessionIndexState> _state;

    public CashierSessionIndexGrain(
        [PersistentState("cashierSessionIndexState", "cashierSessionIndexStore")]
        IPersistentState<CashierSessionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(CashierSessionIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.SessionId == entry.SessionId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<CashierSessionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<CashierSessionIndexEntry>> GetOpenSessionsAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == "Open").ToList());

    public Task<List<CashierSessionIndexEntry>> GetByDateAsync(DateTime date)
        => Task.FromResult(_state.State.Entries.Where(e => e.SessionDate.Date == date.Date).ToList());
}
