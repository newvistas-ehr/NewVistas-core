// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── AR batch payment index entry ─────────────────────────────────────────────

/// <summary>Lightweight summary of an AR batch payment for index lookups.</summary>
[GenerateSerializer]
public record ARBatchPaymentIndexEntry
{
    /// <summary>Unique batch identifier.</summary>
    [Id(0)] public string BatchId { get; init; } = string.Empty;

    /// <summary>Date the batch was opened.</summary>
    [Id(1)] public DateTime BatchDate { get; init; }

    /// <summary>Facility display name (optional).</summary>
    [Id(2)] public string? FacilityName { get; init; }

    /// <summary>Payment method for this batch (e.g., CASH, CHECK, EFT).</summary>
    [Id(3)] public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Total dollar amount of all payments in the batch.</summary>
    [Id(4)] public decimal TotalAmount { get; init; }

    /// <summary>Whether this batch has been posted to individual AR accounts.</summary>
    [Id(5)] public bool IsPosted { get; init; }

    /// <summary>Number of individual payment lines in this batch.</summary>
    [Id(6)] public int PaymentCount { get; init; }
}

// ─── AR batch payment index state — singleton ─────────────────────────────────

/// <summary>
/// Singleton index of all AR batch payment sessions.
/// Keyed as "AR-BATCH-IDX".
/// </summary>
[GenerateSerializer]
public class ARBatchPaymentIndexState
{
    /// <summary>All batch payment summaries.</summary>
    [Id(0)] public List<ARBatchPaymentIndexEntry> Entries { get; set; } = new();
}
