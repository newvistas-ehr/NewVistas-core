// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lifecycle status of a Treasury Offset Program referral.
/// </summary>
[GenerateSerializer]
public enum TopReferralStatus
{
    /// <summary>Referral submitted to TOP; awaiting certification.</summary>
    Pending = 0,
    /// <summary>Debt certified with Treasury; offset imminent.</summary>
    Certified = 1,
    /// <summary>Full offset received; account resolved.</summary>
    Offset = 2,
    /// <summary>Partial offset received; remaining debt still owed.</summary>
    PartiallyOffset = 3,
    /// <summary>Referral withdrawn before offset occurred.</summary>
    Withdrawn = 4,
    /// <summary>Referral closed after full resolution.</summary>
    Resolved = 5,
}

/// <summary>
/// Tracks a single Treasury Offset Program referral for a delinquent AR account
/// (PRCA TOP referral record — VistA AR module).
/// Grain key: "TOP-REF:{guid}"
/// </summary>
[GenerateSerializer]
public class TopReferralState
{
    /// <summary>Unique referral ID (Guid string). (.01)</summary>
    [Id(0)] public string ReferralId { get; set; } = string.Empty;

    /// <summary>AR account key (without "AR-ACCOUNT:" prefix). (.02)</summary>
    [Id(1)] public string ARAccountId { get; set; } = string.Empty;

    /// <summary>Patient ID. (.03)</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (denormalized for index display). (.04)</summary>
    [Id(3)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Amount referred to Treasury for offset. (.05)</summary>
    [Id(4)] public decimal ReferredAmount { get; set; }

    /// <summary>AR account balance at time of referral. (.06)</summary>
    [Id(5)] public decimal OriginalBalance { get; set; }

    /// <summary>Date the referral was submitted. (.07)</summary>
    [Id(6)] public DateTime ReferralDate { get; set; }

    /// <summary>Current referral lifecycle status. (.08)</summary>
    [Id(7)] public TopReferralStatus Status { get; set; } = TopReferralStatus.Pending;

    /// <summary>Cumulative amount offset by Treasury so far. (.09)</summary>
    [Id(8)] public decimal OffsetAmount { get; set; }

    /// <summary>Date of the most recent offset received. (.10)</summary>
    [Id(9)] public DateTime? OffsetDate { get; set; }

    /// <summary>User ID who submitted the referral. (.11)</summary>
    [Id(10)] public string ReferredByUserId { get; set; } = string.Empty;

    /// <summary>User name who submitted the referral. (.12)</summary>
    [Id(11)] public string ReferredByUserName { get; set; } = string.Empty;

    /// <summary>Reason provided when referral was withdrawn. (.13)</summary>
    [Id(12)] public string? WithdrawalReason { get; set; }

    /// <summary>Free-text notes. (.14)</summary>
    [Id(13)] public string? Notes { get; set; }

    /// <summary>Record creation timestamp.</summary>
    [Id(14)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modification timestamp.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight summary entry stored in the TOP referral singleton index grain.
/// </summary>
[GenerateSerializer]
public record TopReferralIndexEntry(
    [property: Id(0)] string ReferralId,
    [property: Id(1)] string ARAccountId,
    [property: Id(2)] string PatientId,
    [property: Id(3)] string PatientName,
    [property: Id(4)] decimal ReferredAmount,
    [property: Id(5)] TopReferralStatus Status,
    [property: Id(6)] DateTime ReferralDate,
    [property: Id(7)] decimal OffsetAmount);

/// <summary>
/// Singleton index state for the TOP referral index grain.
/// </summary>
[GenerateSerializer]
public class TopReferralIndexState
{
    [Id(0)] public List<TopReferralIndexEntry> Entries { get; set; } = new();
}
