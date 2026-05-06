// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── AR transaction type ───────────────────────────────────────────────────────

/// <summary>Classification of an AR financial transaction (VistA File #433).</summary>
[GenerateSerializer]
public enum ARTransactionType
{
    /// <summary>Original charge posted to the account.</summary>
    Charge = 0,

    /// <summary>Cash, check, or electronic payment received.</summary>
    Payment = 1,

    /// <summary>Balance adjustment (credit or debit, not a payment).</summary>
    Adjustment = 2,

    /// <summary>Partial or full balance waiver by authorized staff.</summary>
    Waiver = 3,

    /// <summary>Balance written off as uncollectable.</summary>
    WriteOff = 4,

    /// <summary>Interest charge accrued on overdue balance.</summary>
    Interest = 5,

    /// <summary>Administrative penalty charge.</summary>
    Penalty = 6,

    /// <summary>Administrative cost charge added to account.</summary>
    AdminCost = 7,

    /// <summary>Refund issued to the patient.</summary>
    Refund = 8,
}

// ─── AR Transaction — VistA File #433 AR TRANSACTION ─────────────────────────

/// <summary>
/// A single financial transaction posted against an AR account (VistA File #433 AR TRANSACTION).
/// Managed by RCDP*.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class ARTransactionState
{
    /// <summary>Unique identifier for this transaction (Guid).</summary>
    [Id(0)] public string TransactionId { get; set; } = string.Empty;

    /// <summary>AR account this transaction was posted against.</summary>
    [Id(1)] public string ARAccountId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Type of financial transaction.</summary>
    [Id(3)] public ARTransactionType TransactionType { get; set; }

    /// <summary>Monetary amount of this transaction (positive = charge/fee; negative = credit).</summary>
    [Id(4)] public decimal Amount { get; set; }

    /// <summary>Date the transaction was posted.</summary>
    [Id(5)] public DateTime TransactionDate { get; set; }

    /// <summary>Receipt number from the cashier system (optional).</summary>
    [Id(6)] public string? ReceiptNumber { get; set; }

    /// <summary>Check number for check payments (optional).</summary>
    [Id(7)] public string? CheckNumber { get; set; }

    /// <summary>Payment method (e.g., CASH, CHECK, CREDIT_CARD, EFT) (optional).</summary>
    [Id(8)] public string? PaymentMethod { get; set; }

    /// <summary>User ID of the staff member who posted this transaction.</summary>
    [Id(9)] public string AppliedByUserId { get; set; } = string.Empty;

    /// <summary>Display name of the staff member who posted this transaction.</summary>
    [Id(10)] public string AppliedByUserName { get; set; } = string.Empty;

    /// <summary>External reference number (e.g., batch ID, voucher number) (optional).</summary>
    [Id(11)] public string? ReferenceNumber { get; set; }

    /// <summary>Free-text notes about this transaction.</summary>
    [Id(12)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this transaction record was created.</summary>
    [Id(13)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
