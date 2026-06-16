// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all TOP referrals.
/// Grain key: "TOP-REF-IDX"
/// </summary>
public interface ITopReferralIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces the index entry for a referral (keyed by ReferralId).</summary>
    Task AddOrUpdateAsync(TopReferralIndexEntry entry);

    /// <summary>Returns all TOP referral index entries.</summary>
    Task<List<TopReferralIndexEntry>> GetAllAsync();

    /// <summary>Returns referrals with Status == Pending or Certified.</summary>
    Task<List<TopReferralIndexEntry>> GetPendingAsync();

    /// <summary>Returns all referrals linked to a specific AR account.</summary>
    Task<List<TopReferralIndexEntry>> GetByAccountAsync(string arAccountId);
}
