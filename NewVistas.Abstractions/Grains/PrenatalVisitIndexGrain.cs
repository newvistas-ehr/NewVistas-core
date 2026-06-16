// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PrenatalVisitIndexGrain : Grain, IPrenatalVisitIndexGrain
{
    private readonly IPersistentState<PrenatalVisitIndexState> _state;

    public PrenatalVisitIndexGrain(
        [PersistentState("prenatalVisitIndexState", "prenatalVisitIndexStore")]
        IPersistentState<PrenatalVisitIndexState> state)
    {
        _state = state;
    }

    public Task<List<PrenatalVisitIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddEntryAsync(PrenatalVisitIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<int> GetVisitCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}
