// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeVendorIndexGrain : Grain, IFeeVendorIndexGrain
{
    private readonly IPersistentState<FeeVendorIndexState> _state;

    public FeeVendorIndexGrain(
        [PersistentState("feeVendorIndexState", "feeVendorIndexStore")]
        IPersistentState<FeeVendorIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(FeeVendorIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.VendorId == entry.VendorId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<FeeVendorIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<FeeVendorIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.IsActive).ToList());
}
