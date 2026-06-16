// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── AR account status ────────────────────────────────────────────────────────

/// <summary>Lifecycle status of an Accounts Receivable account (VistA File #430).</summary>
[GenerateSerializer]
public enum ARAccountStatus
{
    /// <summary>Account is open and has an outstanding balance.</summary>
    Active = 0,

    /// <summary>Balance has been paid in full.</summary>
    Paid = 1,

    /// <summary>Balance has been waived by an authorized user.</summary>
    Waived = 2,

    /// <summary>Balance written off as uncollectable.</summary>
    WrittenOff = 3,

    /// <summary>Referred to a collection agency.</summary>
    InCollection = 4,

    /// <summary>Referred to Treasury Offset Program.</summary>
    TreasuryOffset = 5,

    /// <summary>Patient is on an approved payment plan.</summary>
    OnPaymentPlan = 6,

    /// <summary>Account cancelled — no collection action required.</summary>
    Cancelled = 7,
}

// ─── AR account category ──────────────────────────────────────────────────────

/// <summary>Category of the Accounts Receivable account (derived from IB action type).</summary>
[GenerateSerializer]
public enum ARAccountCategory
{
    /// <summary>Outpatient visit copay charge.</summary>
    CopayOutpatient = 0,

    /// <summary>Inpatient copay or per diem charge.</summary>
    CopayInpatient = 1,

    /// <summary>Pharmacy copay charge.</summary>
    CopayPharmacy = 2,

    /// <summary>Long-term care copay charge.</summary>
    CopayLongTermCare = 3,

    /// <summary>Medicare deductible charge.</summary>
    MedicareDeductible = 4,

    /// <summary>Administrative charge (e.g., hospital admission, CHAMPVA).</summary>
    Administrative = 5,

    /// <summary>Other charge type not otherwise classified.</summary>
    Other = 6,
}

// ─── AR Account — VistA File #430 ACCOUNTS RECEIVABLE ─────────────────────────

/// <summary>
/// An individual Accounts Receivable account representing a single charge
/// against a patient (VistA File #430 ACCOUNTS RECEIVABLE).
/// Managed by RCDP*.m, RCSP*.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class ARAccountState
{
    /// <summary>Unique identifier for this AR account (Guid).</summary>
    [Id(0)] public string ARAccountId { get; set; } = string.Empty;

    /// <summary>Patient identifier (links to PatientGrain and ARDebtorGrain).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Source IB billing action that generated this AR account (optional).</summary>
    [Id(2)] public string? BillingActionId { get; set; }

    /// <summary>Category of the AR charge (Outpatient, Pharmacy, Inpatient, etc.).</summary>
    [Id(3)] public ARAccountCategory ARCategory { get; set; }

    /// <summary>Current lifecycle status of this AR account.</summary>
    [Id(4)] public ARAccountStatus ARStatus { get; set; } = ARAccountStatus.Active;

    /// <summary>Original charge amount when the account was established.</summary>
    [Id(5)] public decimal OriginalAmount { get; set; }

    /// <summary>Outstanding balance (original − paid − waived + interest + penalty + admin).</summary>
    [Id(6)] public decimal CurrentBalance { get; set; }

    /// <summary>Total amount paid against this account.</summary>
    [Id(7)] public decimal AmountPaid { get; set; }

    /// <summary>Total amount waived against this account.</summary>
    [Id(8)] public decimal AmountWaived { get; set; }

    /// <summary>Total interest accrued on this account.</summary>
    [Id(9)] public decimal InterestAccrued { get; set; }

    /// <summary>Total penalty charges accrued on this account.</summary>
    [Id(10)] public decimal PenaltyAccrued { get; set; }

    /// <summary>Total administrative cost charges on this account.</summary>
    [Id(11)] public decimal AdminCostAccrued { get; set; }

    /// <summary>Date the AR account was established.</summary>
    [Id(12)] public DateTime DateEstablished { get; set; }

    /// <summary>Date by which payment is due.</summary>
    [Id(13)] public DateTime? DueDate { get; set; }

    /// <summary>Date of the most recent activity (payment, adjustment, etc.).</summary>
    [Id(14)] public DateTime? LastActivityDate { get; set; }

    /// <summary>Date the most recent statement was generated.</summary>
    [Id(15)] public DateTime? LastStatementDate { get; set; }

    /// <summary>Date the next statement is scheduled.</summary>
    [Id(16)] public DateTime? NextStatementDate { get; set; }

    /// <summary>Whether this account is currently in collection.</summary>
    [Id(17)] public bool IsInCollection { get; set; }

    /// <summary>Date the account was referred to collection.</summary>
    [Id(18)] public DateTime? CollectionDate { get; set; }

    /// <summary>Whether this account is in Treasury Offset Program.</summary>
    [Id(19)] public bool IsTreasuryOffset { get; set; }

    /// <summary>IDs of all ARTransactionGrain records linked to this account.</summary>
    [Id(20)] public List<string> TransactionIds { get; set; } = new();

    /// <summary>Free-text notes about this AR account.</summary>
    [Id(21)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(22)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(23)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
