// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class TopMatchingGrain : Grain, ITopMatchingGrain
{
    private readonly IPersistentState<TopMatchingState> _state;

    public TopMatchingGrain(
        [PersistentState("topMatchingState", "topMatchingStore")]
        IPersistentState<TopMatchingState> state)
    {
        _state = state;
    }

    public Task<TopMatchingState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> RecordOffsetAsync(
        string treasuryTransactionId, string taxpayerIdNumber, string treasuryPatientName,
        decimal offsetAmount, string offsetSource, DateTime offsetReceivedDate, string? notes)
    {
        string matchId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;
        _state.State.MatchId               = matchId;
        _state.State.TreasuryTransactionId = treasuryTransactionId;
        _state.State.TaxpayerIdNumber      = taxpayerIdNumber;
        _state.State.TreasuryPatientName   = treasuryPatientName;
        _state.State.OffsetAmount          = offsetAmount;
        _state.State.UnmatchedAmount       = offsetAmount;
        _state.State.OffsetSource          = offsetSource;
        _state.State.OffsetReceivedDate    = offsetReceivedDate;
        _state.State.Status                = TopMatchStatus.PendingMatch;
        _state.State.Notes                 = notes;
        _state.State.CreatedDate           = now;
        _state.State.LastModifiedDate      = now;
        await _state.WriteStateAsync();
        return matchId;
    }

    public async Task MatchToAccountAsync(
        string matchedPatientId, string matchedARAccountId, string? matchedTopReferralId,
        decimal appliedAmount, string? processedByUserId, string? processedByUserName)
    {
        _state.State.MatchedPatientId      = matchedPatientId;
        _state.State.MatchedARAccountId    = matchedARAccountId;
        _state.State.MatchedTopReferralId  = matchedTopReferralId;
        _state.State.AppliedAmount         = appliedAmount;
        _state.State.UnmatchedAmount       = _state.State.OffsetAmount - appliedAmount;
        _state.State.MatchProcessedDate    = DateTime.UtcNow;
        _state.State.ProcessedByUserId     = processedByUserId;
        _state.State.ProcessedByUserName   = processedByUserName;
        _state.State.Status = appliedAmount >= _state.State.OffsetAmount
            ? TopMatchStatus.Matched
            : TopMatchStatus.PartialMatch;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkUnmatchedAsync(List<string> reasons, string? processedByUserId)
    {
        _state.State.Status             = TopMatchStatus.Unmatched;
        _state.State.MatchReasons       = reasons;
        _state.State.ProcessedByUserId  = processedByUserId;
        _state.State.MatchProcessedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectAsync(List<string> reasons, string? processedByUserId)
    {
        _state.State.Status             = TopMatchStatus.Rejected;
        _state.State.MatchReasons       = reasons;
        _state.State.ProcessedByUserId  = processedByUserId;
        _state.State.MatchProcessedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
