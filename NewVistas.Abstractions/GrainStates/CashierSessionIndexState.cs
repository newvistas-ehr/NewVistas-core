// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Cashier Session Index Entry ───────────────────────────────────────────────

/// <summary>Lightweight summary of a cashier session for index lookup.</summary>
[GenerateSerializer]
public record CashierSessionIndexEntry
{
    /// <summary>Unique identifier for the session (grain key).</summary>
    [Id(0)] public string SessionId { get; init; } = string.Empty;

    /// <summary>Cashier station (window) identifier.</summary>
    [Id(1)] public string StationId { get; init; } = string.Empty;

    /// <summary>User ID of the cashier who opened the session.</summary>
    [Id(2)] public string CashierId { get; init; } = string.Empty;

    /// <summary>Display name of the cashier.</summary>
    [Id(3)] public string CashierName { get; init; } = string.Empty;

    /// <summary>Calendar date of this session.</summary>
    [Id(4)] public DateTime SessionDate { get; init; }

    /// <summary>Total amount collected across all receipts in the session.</summary>
    [Id(5)] public decimal TotalCollected { get; init; }

    /// <summary>Lifecycle status (e.g., "Open", "Closed", "TurnedIn").</summary>
    [Id(6)] public string Status { get; init; } = string.Empty;

    /// <summary>UTC date and time of the turn-in (optional).</summary>
    [Id(7)] public DateTime? TurnedInDate { get; init; }
}

// ─── Cashier Session Index State ───────────────────────────────────────────────

/// <summary>
/// Singleton index grain for all cashier sessions across all stations and dates.
/// Grain key: "CASHIER-SESSION-IDX".
/// </summary>
[GenerateSerializer]
public class CashierSessionIndexState
{
    /// <summary>All cashier session summaries.</summary>
    [Id(0)] public List<CashierSessionIndexEntry> Entries { get; set; } = new();
}
