// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EdiClaimIndexGrain : Grain, IEdiClaimIndexGrain
{
    private readonly IPersistentState<EdiClaimIndexState> _state;

    public EdiClaimIndexGrain(
        [PersistentState("ediClaimIndexState", "ediClaimIndexStore")]
        IPersistentState<EdiClaimIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EdiClaimIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.ClaimId == entry.ClaimId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<EdiClaimIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);
}
