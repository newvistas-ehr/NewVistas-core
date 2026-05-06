// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CashierReceiptIndexGrain : Grain, ICashierReceiptIndexGrain
{
    private readonly IPersistentState<CashierReceiptIndexState> _state;

    public CashierReceiptIndexGrain(
        [PersistentState("cashierReceiptIndexState", "cashierReceiptIndexStore")]
        IPersistentState<CashierReceiptIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(CashierReceiptIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.ReceiptId == entry.ReceiptId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<CashierReceiptIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}
