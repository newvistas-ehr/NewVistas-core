// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary of an ERA for index queries.
/// </summary>
[GenerateSerializer]
public record EraIndexEntry
{
    /// <summary>ERA grain key string.</summary>
    [Id(0)] public string EraId { get; init; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(1)] public string PayerName { get; init; } = string.Empty;

    /// <summary>Date the payer issued the payment.</summary>
    [Id(2)] public DateTime PaymentDate { get; init; }

    /// <summary>Total payment amount in this ERA.</summary>
    [Id(3)] public decimal TotalPaymentAmount { get; init; }

    /// <summary>Current status string (e.g., "Received", "Posted", "Error").</summary>
    [Id(4)] public string Status { get; init; } = string.Empty;

    /// <summary>Number of claim payments in this ERA.</summary>
    [Id(5)] public int ClaimCount { get; init; }

    /// <summary>Check or EFT trace number.</summary>
    [Id(6)] public string? CheckNumber { get; init; }
}

/// <summary>
/// Singleton index of all ERAs (Electronic Remittance Advices).
/// Grain key: "ERA-IDX"
/// </summary>
[GenerateSerializer]
public class EraIndexState
{
    /// <summary>All ERA entries.</summary>
    [Id(0)] public List<EraIndexEntry> Entries { get; set; } = new();
}
