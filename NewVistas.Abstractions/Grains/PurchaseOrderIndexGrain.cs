// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PurchaseOrderIndexGrain : Grain, IPurchaseOrderIndexGrain
{
    private readonly IPersistentState<PurchaseOrderIndexState> _state;

    public PurchaseOrderIndexGrain(
        [PersistentState("purchaseOrderIndexState", "ifcapPurchaseOrderIndexStore")]
        IPersistentState<PurchaseOrderIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PurchaseOrderIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.PurchaseOrderId == entry.PurchaseOrderId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<PurchaseOrderIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PurchaseOrderIndexEntry>> GetOpenAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == PurchaseOrderStatus.Open
                     || e.Status == PurchaseOrderStatus.PartiallyReceived)
            .ToList());

    public Task<List<PurchaseOrderIndexEntry>> GetByControlPointAsync(string controlPointId)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.ControlPointId == controlPointId)
            .ToList());
}
