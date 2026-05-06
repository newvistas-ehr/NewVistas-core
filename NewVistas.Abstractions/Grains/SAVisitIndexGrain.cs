// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class SAVisitIndexGrain : Grain, ISAVisitIndexGrain
{
    private readonly IPersistentState<SAVisitIndexState> _state;

    public SAVisitIndexGrain(
        [PersistentState("saVisitIndexState", "saVisitIndexStore")]
        IPersistentState<SAVisitIndexState> state)
    {
        _state = state;
    }

    public Task<List<SAVisitIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddEntryAsync(SAVisitIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<int> GetVisitCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}
