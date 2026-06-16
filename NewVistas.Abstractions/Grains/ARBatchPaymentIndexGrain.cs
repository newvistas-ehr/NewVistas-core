// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARBatchPaymentIndexGrain : Grain, IARBatchPaymentIndexGrain
{
    private readonly IPersistentState<ARBatchPaymentIndexState> _state;

    public ARBatchPaymentIndexGrain(
        [PersistentState("arBatchPaymentIndexState", "arBatchPaymentIndexStore")]
        IPersistentState<ARBatchPaymentIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ARBatchPaymentIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.BatchId == entry.BatchId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<ARBatchPaymentIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<ARBatchPaymentIndexEntry>> GetUnpostedAsync()
        => Task.FromResult(_state.State.Entries.Where(e => !e.IsPosted).ToList());
}
