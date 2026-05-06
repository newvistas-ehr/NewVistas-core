// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Cashier payment method ────────────────────────────────────────────────────

/// <summary>Method of payment tendered at the cashier window.</summary>
[GenerateSerializer]
public enum CashierPaymentMethod
{
    /// <summary>US currency (bills and coins).</summary>
    Cash = 0,

    /// <summary>Personal or certified check.</summary>
    Check = 1,

    /// <summary>Postal or commercial money order.</summary>
    MoneyOrder = 2,

    /// <summary>Credit or debit card.</summary>
    CreditCard = 3,

    /// <summary>Electronic wire transfer.</summary>
    WireTransfer = 4,
}

// ─── Cashier receipt status ────────────────────────────────────────────────────

/// <summary>Lifecycle status of a cashier receipt.</summary>
[GenerateSerializer]
public enum CashierReceiptStatus
{
    /// <summary>Receipt issued and active; payment posted to AR.</summary>
    Issued = 0,

    /// <summary>Receipt voided; original payment may require a manual AR adjustment.</summary>
    Voided = 1,

    /// <summary>Receipt included in a completed cashier turn-in to fiscal.</summary>
    TurnedIn = 2,
}

// ─── Cashier Receipt — VistA File #36 AGENT CASHIER ──────────────────────────

/// <summary>
/// An individual receipt issued by a VA cashier when accepting a patient payment
/// at the cashier window (VistA File #36 AGENT CASHIER receipt subfile).
/// Managed by RCDPE.m, RCDPR.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class CashierReceiptState
{
    /// <summary>Unique identifier for this receipt (grain key, Guid).</summary>
    [Id(0)] public string ReceiptId { get; set; } = string.Empty;

    /// <summary>Human-readable receipt number assigned at the window (e.g., "R-20240201-001").</summary>
    [Id(1)] public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>Patient who made the payment.</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient display name at the time of payment.</summary>
    [Id(3)] public string PatientName { get; set; } = string.Empty;

    /// <summary>AR account against which this payment was posted.</summary>
    [Id(4)] public string ARAccountId { get; set; } = string.Empty;

    /// <summary>Dollar amount received.</summary>
    [Id(5)] public decimal Amount { get; set; }

    /// <summary>Method of payment tendered.</summary>
    [Id(6)] public CashierPaymentMethod PaymentMethod { get; set; }

    /// <summary>Current lifecycle status of this receipt.</summary>
    [Id(7)] public CashierReceiptStatus Status { get; set; } = CashierReceiptStatus.Issued;

    /// <summary>User ID of the cashier who accepted the payment.</summary>
    [Id(8)] public string CashierId { get; set; } = string.Empty;

    /// <summary>Display name of the cashier who accepted the payment.</summary>
    [Id(9)] public string CashierName { get; set; } = string.Empty;

    /// <summary>Session (daily cashier shift) in which this receipt was issued.</summary>
    [Id(10)] public string SessionId { get; set; } = string.Empty;

    /// <summary>Date and time the payment was received.</summary>
    [Id(11)] public DateTime ReceiptDate { get; set; }

    /// <summary>Check or money order number (optional; populated when PaymentMethod is Check or MoneyOrder).</summary>
    [Id(12)] public string? CheckNumber { get; set; }

    /// <summary>User ID of the staff member who voided this receipt (optional).</summary>
    [Id(13)] public string? VoidedByUserId { get; set; }

    /// <summary>UTC date and time the receipt was voided (optional).</summary>
    [Id(14)] public DateTime? VoidedDate { get; set; }

    /// <summary>Reason this receipt was voided (optional).</summary>
    [Id(15)] public string? VoidReason { get; set; }

    /// <summary>Free-text notes about this receipt.</summary>
    [Id(16)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(17)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(18)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
