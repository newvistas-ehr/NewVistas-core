// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IBillingActionIndexGrain : Grain, IIBillingActionIndexGrain
{
    private readonly IPersistentState<IBillingActionIndexState> _state;

    public IBillingActionIndexGrain(
        [PersistentState("ibBillingActionIndexState", "ibBillingActionIndexStore")]
        IPersistentState<IBillingActionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(IBillingActionIndexEntry entry)
    {
        List<IBillingActionIndexEntry> entries = _state.State.Entries;
        int existingIdx = entries.FindIndex(e => e.BillingActionId == entry.BillingActionId);
        if (existingIdx >= 0)
            entries[existingIdx] = entry;
        else
            entries.Insert(0, entry);

        await _state.WriteStateAsync();
    }

    public Task<List<IBillingActionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<IBillingActionIndexEntry>> GetByStatusAsync(IBillingActionStatus status)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == status)
            .ToList());

    public Task<List<IBillingActionIndexEntry>> GetByDateRangeAsync(DateTime from, DateTime to)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.ServiceDate >= from && e.ServiceDate <= to)
            .OrderByDescending(e => e.ServiceDate)
            .ToList());
}
