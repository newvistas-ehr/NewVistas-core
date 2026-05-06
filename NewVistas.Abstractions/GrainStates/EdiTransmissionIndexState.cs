// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary of a transmission batch for index queries.
/// </summary>
[GenerateSerializer]
public record EdiTransmissionIndexEntry
{
    /// <summary>Transmission grain key string.</summary>
    [Id(0)] public string TransmissionId { get; init; } = string.Empty;

    /// <summary>Human-readable batch number.</summary>
    [Id(1)] public string BatchNumber { get; init; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(2)] public string PayerName { get; init; } = string.Empty;

    /// <summary>Current status string (e.g., "Open", "Sent", "Accepted").</summary>
    [Id(3)] public string Status { get; init; } = string.Empty;

    /// <summary>Total number of claims in the batch.</summary>
    [Id(4)] public int TotalClaims { get; init; }

    /// <summary>Sum of billed amounts across all claims.</summary>
    [Id(5)] public decimal TotalBilledAmount { get; init; }

    /// <summary>Date the batch was transmitted (null if not yet sent).</summary>
    [Id(6)] public DateTime? SentDate { get; init; }
}

/// <summary>
/// Singleton index of all EDI transmission batches.
/// Grain key: "EDI-TX-IDX"
/// </summary>
[GenerateSerializer]
public class EdiTransmissionIndexState
{
    /// <summary>All transmission batch entries.</summary>
    [Id(0)] public List<EdiTransmissionIndexEntry> Entries { get; set; } = new();
}
