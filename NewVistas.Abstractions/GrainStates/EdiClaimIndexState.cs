// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary of an EDI claim for patient-level index queries.
/// </summary>
[GenerateSerializer]
public record EdiClaimIndexEntry
{
    /// <summary>Claim grain key (without prefix).</summary>
    [Id(0)] public string ClaimId { get; init; } = string.Empty;

    /// <summary>Patient associated with this claim.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Name of the insurance payer.</summary>
    [Id(2)] public string PayerName { get; init; } = string.Empty;

    /// <summary>Claim type string (e.g., "Professional837P").</summary>
    [Id(3)] public string ClaimType { get; init; } = string.Empty;

    /// <summary>Current status string (e.g., "Draft", "Paid", "Denied").</summary>
    [Id(4)] public string Status { get; init; } = string.Empty;

    /// <summary>Total dollar amount billed.</summary>
    [Id(5)] public decimal TotalBilledAmount { get; init; }

    /// <summary>Amount paid by payer (null until ERA processed).</summary>
    [Id(6)] public decimal? PaidAmount { get; init; }

    /// <summary>Date of first service on the claim.</summary>
    [Id(7)] public DateTime ServiceFromDate { get; init; }
}

/// <summary>
/// Per-patient index of EDI claims.
/// Grain key: "EDI-CLAIM-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class EdiClaimIndexState
{
    /// <summary>Patient whose claims are indexed here.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All EDI claim entries for this patient.</summary>
    [Id(1)] public List<EdiClaimIndexEntry> Entries { get; set; } = new();
}
