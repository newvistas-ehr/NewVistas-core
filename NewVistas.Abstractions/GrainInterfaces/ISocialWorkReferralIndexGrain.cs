// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of Social Work Referrals.
/// Key: "SW-REFERRAL-IDX:{patientId}"
/// </summary>
public interface ISocialWorkReferralIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetAllAsync();

    Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetByServiceTypeAsync(
        GrainStates.SocialWorkReferralServiceType serviceType);

    Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetByStatusAsync(
        GrainStates.SocialWorkReferralStatus status);

    Task AddEntryAsync(GrainStates.SocialWorkReferralIndexEntry entry);

    Task UpdateEntryStatusAsync(
        string referralId,
        GrainStates.SocialWorkReferralStatus status,
        DateTime? followUpDate = null);
}
