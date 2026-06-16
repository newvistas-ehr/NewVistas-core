// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Facility-wide Accounts Receivable configuration (VistA File #342 AR SITE PARAMETER).
/// Singleton grain — one instance per facility.
/// </summary>
[GenerateSerializer]
public class ARSiteParametersState
{
    /// <summary>Site identifier (typically station number).</summary>
    [Id(0)] public string SiteId { get; set; } = string.Empty;

    /// <summary>Facility name for AR correspondence.</summary>
    [Id(1)] public string SiteName { get; set; } = string.Empty;

    /// <summary>AR facility number used in FMS and Treasury reporting.</summary>
    [Id(2)] public string ARFacilityNumber { get; set; } = string.Empty;

    /// <summary>Annual interest rate applied to delinquent accounts (percent, e.g., 0.0625 = 6.25%).</summary>
    [Id(3)] public decimal InterestRate { get; set; }

    /// <summary>Administrative cost charged for collection actions (dollars).</summary>
    [Id(4)] public decimal AdminCost { get; set; }

    /// <summary>Penalty rate applied to delinquent accounts (percent).</summary>
    [Id(5)] public decimal PenaltyRate { get; set; }

    /// <summary>Minimum acceptable payment amount for installment plans (dollars).</summary>
    [Id(6)] public decimal MinimumPaymentAmount { get; set; }

    /// <summary>Maximum number of months allowed for payment plan installments.</summary>
    [Id(7)] public int MaxPaymentPlanMonths { get; set; }

    /// <summary>Whether interest is automatically accrued on delinquent accounts.</summary>
    [Id(8)] public bool IsAutoInterestEnabled { get; set; }

    /// <summary>Whether penalty charges are automatically applied.</summary>
    [Id(9)] public bool IsPenaltyEnabled { get; set; }

    /// <summary>Number of days between AR statements (e.g., 30 = monthly).</summary>
    [Id(10)] public int StatementFrequencyDays { get; set; } = 30;

    /// <summary>Minimum outstanding balance before referring to collection (dollars).</summary>
    [Id(11)] public decimal CollectionThreshold { get; set; }

    /// <summary>Whether FMS (Financial Management System) integration is active.</summary>
    [Id(12)] public bool IsFmsEnabled { get; set; }

    /// <summary>Whether Treasury Offset Program is active for delinquent debts.</summary>
    [Id(13)] public bool IsTreasuryOffsetEnabled { get; set; }

    /// <summary>UTC timestamp of the most recent parameter update.</summary>
    [Id(14)] public DateTime? LastUpdatedDate { get; set; }

    /// <summary>User ID who last updated the parameters.</summary>
    [Id(15)] public string? LastUpdatedByUserId { get; set; }
}
