// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class TopReferralGrain : Grain, ITopReferralGrain
{
    private readonly IPersistentState<TopReferralState> _state;

    public TopReferralGrain(
        [PersistentState("topReferralState", "topReferralStore")]
        IPersistentState<TopReferralState> state)
    {
        _state = state;
    }

    public Task<TopReferralState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string arAccountId,
        string patientId,
        string patientName,
        decimal referredAmount,
        decimal originalBalance,
        string referredByUserId,
        string referredByUserName,
        string? notes)
    {
        _state.State.ReferralId          = this.GetPrimaryKeyString();
        _state.State.ARAccountId         = arAccountId;
        _state.State.PatientId           = patientId;
        _state.State.PatientName         = patientName;
        _state.State.ReferredAmount      = referredAmount;
        _state.State.OriginalBalance     = originalBalance;
        _state.State.ReferralDate        = DateTime.UtcNow;
        _state.State.Status              = TopReferralStatus.Pending;
        _state.State.OffsetAmount        = 0m;
        _state.State.ReferredByUserId    = referredByUserId;
        _state.State.ReferredByUserName  = referredByUserName;
        _state.State.Notes               = notes;
        _state.State.CreatedDate         = DateTime.UtcNow;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordOffsetAsync(decimal offsetAmount, DateTime offsetDate)
    {
        _state.State.OffsetAmount       += offsetAmount;
        _state.State.OffsetDate          = offsetDate;
        _state.State.Status              = _state.State.OffsetAmount >= _state.State.ReferredAmount
            ? TopReferralStatus.Offset
            : TopReferralStatus.PartiallyOffset;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task WithdrawAsync(string reason)
    {
        _state.State.Status              = TopReferralStatus.Withdrawn;
        _state.State.WithdrawalReason    = reason;
        _state.State.LastModifiedDate    = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
