// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ClaimStatusInquiryIndexGrain : Grain, IClaimStatusInquiryIndexGrain
{
    private readonly IPersistentState<ClaimStatusInquiryIndexState> _state;

    public ClaimStatusInquiryIndexGrain(
        [PersistentState("claimStatusInquiryIndexState", "claimStatusInquiryIndexStore")]
        IPersistentState<ClaimStatusInquiryIndexState> state)
    {
        _state = state;
    }

    public Task<List<ClaimStatusInquiryIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(ClaimStatusInquiryIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.InquiryId == entry.InquiryId);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ClaimStatusInquiryIndexEntry>> GetByClaimAsync(string claimId)
        => Task.FromResult(_state.State.Entries.Where(e => e.ClaimId == claimId).ToList());
}
