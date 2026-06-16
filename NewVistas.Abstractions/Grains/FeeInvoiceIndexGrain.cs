// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeInvoiceIndexGrain : Grain, IFeeInvoiceIndexGrain
{
    private readonly IPersistentState<FeeInvoiceIndexState> _state;

    public FeeInvoiceIndexGrain(
        [PersistentState("feeInvoiceIndexState", "feeInvoiceIndexStore")]
        IPersistentState<FeeInvoiceIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(FeeInvoiceIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.InvoiceId == entry.InvoiceId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<FeeInvoiceIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<FeeInvoiceIndexEntry>> GetByStatusAsync(string status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());
}
