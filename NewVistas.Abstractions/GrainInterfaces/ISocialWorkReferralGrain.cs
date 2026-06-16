// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Social Work Referral Grain — VistA File #707 referral sub-file.
/// Key: "SW-REFERRAL:{guid}"
///
/// Tracks referrals to community services, VA programs, and other agencies
/// initiated by the social worker on behalf of the patient.
/// </summary>
public interface ISocialWorkReferralGrain : IGrainWithStringKey
{
    Task<GrainStates.SocialWorkReferralState> GetAsync();

    Task CreateAsync(
        string patientId,
        DateTime referralDate,
        string? referralSource,
        string? referralReason,
        GrainStates.SocialWorkReferralServiceType serviceType,
        string? agencyName,
        string? agencyContact,
        string? agencyPhone,
        string? socialWorkerId,
        string? socialWorkerName,
        DateTime? followUpDate,
        string? assessmentId,
        string? locationId,
        string? locationName,
        string? comments);

    Task UpdateStatusAsync(
        GrainStates.SocialWorkReferralStatus status,
        string? outcomeNotes,
        DateTime? followUpDate);

    Task AcceptAsync(DateTime acceptedDate);

    Task CloseAsync(string? outcomeNotes);
}
