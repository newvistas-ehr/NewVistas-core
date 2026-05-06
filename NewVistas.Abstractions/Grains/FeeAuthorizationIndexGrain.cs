// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeAuthorizationIndexGrain : Grain, IFeeAuthorizationIndexGrain
{
    private readonly IPersistentState<FeeAuthorizationIndexState> _state;

    public FeeAuthorizationIndexGrain(
        [PersistentState("feeAuthorizationIndexState", "feeAuthorizationIndexStore")]
        IPersistentState<FeeAuthorizationIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(FeeAuthorizationIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.AuthorizationId == entry.AuthorizationId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<FeeAuthorizationIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<FeeAuthorizationIndexEntry>> GetActiveAsync()
        => Task.FromResult(
            _state.State.Entries
                  .Where(e => e.Status == "Active" || e.Status == "Pending")
                  .ToList());
}
