// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PurchaseRequestIndexGrain : Grain, IPurchaseRequestIndexGrain
{
    private readonly IPersistentState<PurchaseRequestIndexState> _state;

    public PurchaseRequestIndexGrain(
        [PersistentState("purchaseRequestIndexState", "ifcapPurchaseRequestIndexStore")]
        IPersistentState<PurchaseRequestIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PurchaseRequestIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.RequestId == entry.RequestId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<PurchaseRequestIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PurchaseRequestIndexEntry>> GetPendingAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == PurchaseRequestStatus.Draft
                     || e.Status == PurchaseRequestStatus.Submitted)
            .ToList());
}
