// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee Invoice Index Entry ───────────────────────────────────────────────────

/// <summary>Lightweight summary of a fee basis invoice for index lookup.</summary>
[GenerateSerializer]
public record FeeInvoiceIndexEntry
{
    /// <summary>Unique identifier for the invoice.</summary>
    [Id(0)] public string InvoiceId { get; init; } = string.Empty;

    /// <summary>Authorization under which services were rendered.</summary>
    [Id(1)] public string AuthorizationId { get; init; } = string.Empty;

    /// <summary>Display name of the vendor who submitted the invoice.</summary>
    [Id(2)] public string VendorName { get; init; } = string.Empty;

    /// <summary>Type of service delivered (e.g., "Outpatient", "Dental").</summary>
    [Id(3)] public string ServiceType { get; init; } = string.Empty;

    /// <summary>Lifecycle status of the invoice (e.g., "Received", "Paid").</summary>
    [Id(4)] public string Status { get; init; } = string.Empty;

    /// <summary>Total dollar amount billed by the vendor.</summary>
    [Id(5)] public decimal BilledAmount { get; init; }

    /// <summary>Amount actually paid (null until payment disbursed).</summary>
    [Id(6)] public decimal? PaidAmount { get; init; }

    /// <summary>Date services were rendered.</summary>
    [Id(7)] public DateTime ServiceDate { get; init; }
}

// ─── Fee Invoice Index State ───────────────────────────────────────────────────

/// <summary>
/// Per-patient index of all fee basis invoices.
/// Grain key: "FEE-INVOICE-IDX:{patientId}".
/// </summary>
[GenerateSerializer]
public class FeeInvoiceIndexState
{
    /// <summary>Patient whose invoices are indexed here.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All invoice summaries for this patient.</summary>
    [Id(1)] public List<FeeInvoiceIndexEntry> Entries { get; set; } = new();
}
