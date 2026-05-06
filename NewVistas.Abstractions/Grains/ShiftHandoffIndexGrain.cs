// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ShiftHandoffIndexGrain : Grain, IShiftHandoffIndexGrain
{
    private readonly IPersistentState<ShiftHandoffIndexState> _state;

    public ShiftHandoffIndexGrain(
        [PersistentState("shiftHandoffIndexState", "shiftHandoffIndexStore")]
        IPersistentState<ShiftHandoffIndexState> state)
    { _state = state; }

    public Task<List<ShiftHandoffIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(ShiftHandoffIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.HandoffId == entry.HandoffId);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<ShiftHandoffIndexEntry?> GetLatestAsync()
        => Task.FromResult(_state.State.Entries.FirstOrDefault());
}
