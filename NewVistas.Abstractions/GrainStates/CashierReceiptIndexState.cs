// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── Cashier Receipt Index Entry ───────────────────────────────────────────────

/// <summary>Lightweight summary of a cashier receipt for per-patient index lookup.</summary>
[GenerateSerializer]
public record CashierReceiptIndexEntry
{
    /// <summary>Unique identifier for the receipt (grain key).</summary>
    [Id(0)] public string ReceiptId { get; init; } = string.Empty;

    /// <summary>Human-readable receipt number.</summary>
    [Id(1)] public string ReceiptNumber { get; init; } = string.Empty;

    /// <summary>Patient who made the payment.</summary>
    [Id(2)] public string PatientId { get; init; } = string.Empty;

    /// <summary>AR account to which the payment was posted.</summary>
    [Id(3)] public string ARAccountId { get; init; } = string.Empty;

    /// <summary>Dollar amount received.</summary>
    [Id(4)] public decimal Amount { get; init; }

    /// <summary>Method of payment (e.g., "Cash", "Check").</summary>
    [Id(5)] public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Lifecycle status of the receipt (e.g., "Issued", "Voided").</summary>
    [Id(6)] public string Status { get; init; } = string.Empty;

    /// <summary>Date and time the payment was received.</summary>
    [Id(7)] public DateTime ReceiptDate { get; init; }
}

// ─── Cashier Receipt Index State ───────────────────────────────────────────────

/// <summary>
/// Per-patient index of all cashier receipts.
/// Grain key: "CASHIER-RECEIPT-IDX:{patientId}".
/// </summary>
[GenerateSerializer]
public class CashierReceiptIndexState
{
    /// <summary>Patient whose receipts are indexed here.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All receipt summaries for this patient.</summary>
    [Id(1)] public List<CashierReceiptIndexEntry> Entries { get; set; } = new();
}
