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
/// Transfusion Index Grain — grain key: "BB-TX-IDX:{patientId}"
/// </summary>
public class TransfusionIndexGrain : Grain, ITransfusionIndexGrain
{
    private readonly IPersistentState<TransfusionIndexState> _state;

    public TransfusionIndexGrain(
        [PersistentState("bbTransfusionIndexState", "bbTransfusionIndexStore")]
        IPersistentState<TransfusionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(TransfusionIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.TransfusionId == entry.TransfusionId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<TransfusionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}

[GenerateSerializer]
public class TransfusionIndexState
{
    [Id(0)]
    public List<TransfusionIndexEntry> Entries { get; set; } = new();
}
