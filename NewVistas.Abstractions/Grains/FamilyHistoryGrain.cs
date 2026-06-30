// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class FamilyHistoryGrain : Grain, IFamilyHistoryGrain
{
    private readonly IPersistentState<FamilyHistoryState> _state;

    public FamilyHistoryGrain(
        [PersistentState("familyHistoryState", "familyHistoryStore")] IPersistentState<FamilyHistoryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> AddMemberAsync(FamilyMemberHistoryEntry member)
    {
        if (string.IsNullOrEmpty(member.MemberId))
            member.MemberId = Guid.NewGuid().ToString();
        _state.State.Members.Add(member);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return member.MemberId;
    }

    public async Task AddConditionAsync(string memberId, FamilyConditionEntry condition)
    {
        FamilyMemberHistoryEntry? member = _state.State.Members.FirstOrDefault(m => m.MemberId == memberId);
        if (member is null) return;
        member.Conditions.Add(condition);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveMemberAsync(string memberId)
    {
        int removed = _state.State.Members.RemoveAll(m => m.MemberId == memberId);
        if (removed == 0) return;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<FamilyHistoryState> GetAsync() => Task.FromResult(_state.State);
}
