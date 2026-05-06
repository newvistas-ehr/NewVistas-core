// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EdiClaimIndexGrain : Grain, IEdiClaimIndexGrain
{
    private readonly IPersistentState<EdiClaimIndexState> _state;

    public EdiClaimIndexGrain(
        [PersistentState("ediClaimIndexState", "ediClaimIndexStore")]
        IPersistentState<EdiClaimIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EdiClaimIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.ClaimId == entry.ClaimId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<EdiClaimIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}
