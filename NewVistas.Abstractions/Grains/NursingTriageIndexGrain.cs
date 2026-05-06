// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class NursingTriageIndexGrain : Grain, INursingTriageIndexGrain
{
    private readonly IPersistentState<NursingTriageIndexState> _state;

    public NursingTriageIndexGrain(
        [PersistentState("nursingTriageIndexState", "nursingTriageIndexStore")]
        IPersistentState<NursingTriageIndexState> state)
    {
        _state = state;
    }

    public Task<List<NursingTriageIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(NursingTriageIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.TriageId == entry.TriageId);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }
}
