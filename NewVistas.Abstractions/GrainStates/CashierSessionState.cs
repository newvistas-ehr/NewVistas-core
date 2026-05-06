// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Cashier session status ────────────────────────────────────────────────────

/// <summary>Lifecycle status of a cashier's daily session.</summary>
[GenerateSerializer]
public enum CashierSessionStatus
{
    /// <summary>Session is currently open; cashier is actively accepting payments.</summary>
    Open = 0,

    /// <summary>Session has been closed and reconciled by the cashier.</summary>
    Closed = 1,

    /// <summary>Collected funds have been turned in to the fiscal office.</summary>
    TurnedIn = 2,
}

// ─── Cashier session receipt entry ────────────────────────────────────────────

/// <summary>Lightweight record of a single receipt within a cashier session.</summary>
[GenerateSerializer]
public record CashierSessionReceiptEntry
{
    /// <summary>Receipt grain key.</summary>
    [Id(0)] public string ReceiptId { get; init; } = string.Empty;

    /// <summary>Dollar amount of this receipt.</summary>
    [Id(1)] public decimal Amount { get; init; }

    /// <summary>Payment method string (e.g., "Cash", "Check").</summary>
    [Id(2)] public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Date and time this receipt was issued.</summary>
    [Id(3)] public DateTime ReceiptDate { get; init; }
}

// ─── Cashier Session — VistA File #36 AGENT CASHIER ──────────────────────────

/// <summary>
/// A cashier's daily session tracking all receipts issued, running totals by payment
/// method, reconciliation, and turn-in to fiscal (VistA File #36 AGENT CASHIER
/// session record). Managed by RCDPS.m MUMPS routine.
/// </summary>
[GenerateSerializer]
public class CashierSessionState
{
    /// <summary>Unique identifier for this session (grain key, Guid).</summary>
    [Id(0)] public string SessionId { get; set; } = string.Empty;

    /// <summary>Cashier station (window) identifier.</summary>
    [Id(1)] public string StationId { get; set; } = string.Empty;

    /// <summary>Human-readable name of the cashier station (e.g., "Window 1").</summary>
    [Id(2)] public string StationName { get; set; } = string.Empty;

    /// <summary>User ID of the cashier who opened this session.</summary>
    [Id(3)] public string CashierId { get; set; } = string.Empty;

    /// <summary>Display name of the cashier.</summary>
    [Id(4)] public string CashierName { get; set; } = string.Empty;

    /// <summary>Calendar date of this session (date portion only).</summary>
    [Id(5)] public DateTime SessionDate { get; set; }

    /// <summary>Current lifecycle status of this session.</summary>
    [Id(6)] public CashierSessionStatus Status { get; set; } = CashierSessionStatus.Open;

    /// <summary>Cash on hand at session open (petty cash / change fund).</summary>
    [Id(7)] public decimal OpeningBalance { get; set; }

    /// <summary>Running total of cash receipts issued this session.</summary>
    [Id(8)] public decimal TotalCashCollected { get; set; }

    /// <summary>Running total of check receipts issued this session.</summary>
    [Id(9)] public decimal TotalCheckCollected { get; set; }

    /// <summary>Running total of money order receipts issued this session.</summary>
    [Id(10)] public decimal TotalMoneyOrderCollected { get; set; }

    /// <summary>Running total of all other payment method receipts (credit card, wire, etc.).</summary>
    [Id(11)] public decimal TotalOtherCollected { get; set; }

    /// <summary>Sum of all receipts regardless of payment method.</summary>
    [Id(12)] public decimal TotalCollected { get; set; }

    /// <summary>Expected balance at close: OpeningBalance + TotalCollected.</summary>
    [Id(13)] public decimal ExpectedBalance { get; set; }

    /// <summary>Actual cash/instrument count at session close (optional until closed).</summary>
    [Id(14)] public decimal? ActualBalance { get; set; }

    /// <summary>Discrepancy between expected and actual at close (optional until closed).</summary>
    [Id(15)] public decimal? Discrepancy { get; set; }

    /// <summary>Amount turned in to fiscal (optional until turned in).</summary>
    [Id(16)] public decimal? TurnedInAmount { get; set; }

    /// <summary>UTC date and time of the turn-in (optional).</summary>
    [Id(17)] public DateTime? TurnedInDate { get; set; }

    /// <summary>User ID of the fiscal officer who received the turn-in (optional).</summary>
    [Id(18)] public string? TurnedInToUserId { get; set; }

    /// <summary>Turn-in confirmation receipt number (optional).</summary>
    [Id(19)] public string? TurnedInReceiptNumber { get; set; }

    /// <summary>All individual receipts issued within this session.</summary>
    [Id(20)] public List<CashierSessionReceiptEntry> ReceiptEntries { get; set; } = new();

    /// <summary>UTC timestamp when this session was opened.</summary>
    [Id(21)] public DateTime OpenedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when this session was closed (optional).</summary>
    [Id(22)] public DateTime? ClosedDate { get; set; }

    /// <summary>Free-text notes (e.g., reconciliation comments).</summary>
    [Id(23)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(24)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
