// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the lifecycle of a single Treasury Offset Program referral.
/// Grain key: "TOP-REF:{guid}"
/// </summary>
public interface ITopReferralGrain : IGrainWithStringKey
{
    /// <summary>Returns the current TOP referral state.</summary>
    Task<TopReferralState> GetAsync();

    /// <summary>
    /// Creates the TOP referral record. Sets Status = Pending.
    /// </summary>
    Task CreateAsync(
        string arAccountId,
        string patientId,
        string patientName,
        decimal referredAmount,
        decimal originalBalance,
        string referredByUserId,
        string referredByUserName,
        string? notes);

    /// <summary>
    /// Records an incoming Treasury offset payment.
    /// Adds <paramref name="offsetAmount"/> to OffsetAmount.
    /// Sets Status = Offset if cumulative offset &gt;= ReferredAmount, else PartiallyOffset.
    /// </summary>
    Task RecordOffsetAsync(decimal offsetAmount, DateTime offsetDate);

    /// <summary>
    /// Withdraws the TOP referral before an offset occurs.
    /// Sets Status = Withdrawn, records the withdrawal reason.
    /// </summary>
    Task WithdrawAsync(string reason);
}
