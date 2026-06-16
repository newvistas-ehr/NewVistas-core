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
/// Crossmatch Index Grain — grain key: "BB-XM-IDX:{patientId}"
/// </summary>
public class CrossmatchIndexGrain : Grain, ICrossmatchIndexGrain
{
    private readonly IPersistentState<CrossmatchIndexState> _state;

    public CrossmatchIndexGrain(
        [PersistentState("bbCrossmatchIndexState", "bbCrossmatchIndexStore")]
        IPersistentState<CrossmatchIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(CrossmatchIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.CrossmatchId == entry.CrossmatchId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<CrossmatchIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}

[GenerateSerializer]
public class CrossmatchIndexState
{
    [Id(0)]
    public List<CrossmatchIndexEntry> Entries { get; set; } = new();
}
