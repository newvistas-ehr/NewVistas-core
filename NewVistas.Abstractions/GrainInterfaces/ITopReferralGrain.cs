// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
