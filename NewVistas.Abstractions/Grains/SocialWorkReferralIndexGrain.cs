// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient Social Work Referral index grain.
/// Key: "SW-REFERRAL-IDX:{patientId}"
/// </summary>
public class SocialWorkReferralIndexGrain : Grain, ISocialWorkReferralIndexGrain
{
    private readonly IPersistentState<SocialWorkReferralIndexState> _state;

    public SocialWorkReferralIndexGrain(
        [PersistentState("socialWorkReferralIndexState", "socialWorkReferralIndexStore")]
        IPersistentState<SocialWorkReferralIndexState> state)
    {
        _state = state;
    }

    public Task<List<SocialWorkReferralIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<SocialWorkReferralIndexEntry>> GetByServiceTypeAsync(SocialWorkReferralServiceType serviceType)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.ServiceType == serviceType)
            .ToList());

    public Task<List<SocialWorkReferralIndexEntry>> GetByStatusAsync(SocialWorkReferralStatus status)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == status)
            .ToList());

    public async Task AddEntryAsync(SocialWorkReferralIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(
        string referralId,
        SocialWorkReferralStatus status,
        DateTime? followUpDate = null)
    {
        SocialWorkReferralIndexEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.ReferralId == referralId);
        if (entry != null)
        {
            entry.Status = status;
            if (followUpDate.HasValue)
                entry.FollowUpDate = followUpDate;
            await _state.WriteStateAsync();
        }
    }
}
