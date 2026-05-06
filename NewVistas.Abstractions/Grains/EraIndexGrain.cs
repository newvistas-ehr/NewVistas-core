// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EraIndexGrain : Grain, IEraIndexGrain
{
    private readonly IPersistentState<EraIndexState> _state;

    public EraIndexGrain(
        [PersistentState("eraIndexState", "eraIndexStore")]
        IPersistentState<EraIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EraIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.EraId == entry.EraId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<EraIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}
