// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeBatchPaymentIndexGrain : Grain, IFeeBatchPaymentIndexGrain
{
    private readonly IPersistentState<FeeBatchPaymentIndexState> _state;

    public FeeBatchPaymentIndexGrain(
        [PersistentState("feeBatchPaymentIndexState", "feeBatchPaymentIndexStore")]
        IPersistentState<FeeBatchPaymentIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(FeeBatchPaymentIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.BatchId == entry.BatchId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<FeeBatchPaymentIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<FeeBatchPaymentIndexEntry>> GetUnpostedAsync()
        => Task.FromResult(_state.State.Entries.Where(e => !e.IsPosted).ToList());
}
