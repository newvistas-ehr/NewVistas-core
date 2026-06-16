// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── AR batch payment entry ────────────────────────────────────────────────────

/// <summary>A single payment line within an AR batch payment session.</summary>
[GenerateSerializer]
public record ARBatchPaymentEntry
{
    /// <summary>AR account this payment is applied to.</summary>
    [Id(0)] public string ARAccountId { get; init; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Patient display name for batch review.</summary>
    [Id(2)] public string PatientName { get; init; } = string.Empty;

    /// <summary>Payment amount for this line.</summary>
    [Id(3)] public decimal Amount { get; init; }

    /// <summary>Receipt number from the cashier system (optional).</summary>
    [Id(4)] public string? ReceiptNumber { get; init; }
}

// ─── AR Batch Payment — VistA File #344 AR BATCH PAYMENT ─────────────────────

/// <summary>
/// A batch payment session grouping multiple AR account payments posted together
/// (VistA File #344 AR BATCH PAYMENT).
/// Managed by RCDP*.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class ARBatchPaymentState
{
    /// <summary>Unique identifier for this batch (Guid).</summary>
    [Id(0)] public string BatchId { get; set; } = string.Empty;

    /// <summary>Facility identifier where this batch was processed (optional).</summary>
    [Id(1)] public string? FacilityId { get; set; }

    /// <summary>Facility display name (optional).</summary>
    [Id(2)] public string? FacilityName { get; set; }

    /// <summary>Date the batch was opened / payments collected.</summary>
    [Id(3)] public DateTime BatchDate { get; set; }

    /// <summary>Date and time the batch was actually processed/posted (optional).</summary>
    [Id(4)] public DateTime? ProcessedDate { get; set; }

    /// <summary>Payment method for all payments in this batch (e.g., CASH, CHECK, EFT).</summary>
    [Id(5)] public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Sum of all payment amounts in this batch.</summary>
    [Id(6)] public decimal TotalAmount { get; set; }

    /// <summary>Check number if batch is a single-check payment (optional).</summary>
    [Id(7)] public string? CheckNumber { get; set; }

    /// <summary>Receipt range (e.g., "R001–R015") for cashier audit trail (optional).</summary>
    [Id(8)] public string? ReceiptRange { get; set; }

    /// <summary>Individual payment lines in this batch.</summary>
    [Id(9)] public List<ARBatchPaymentEntry> Payments { get; set; } = new();

    /// <summary>Whether this batch has been posted to the individual AR accounts.</summary>
    [Id(10)] public bool IsPosted { get; set; }

    /// <summary>User ID of the staff member who posted this batch (optional).</summary>
    [Id(11)] public string? PostedByUserId { get; set; }

    /// <summary>UTC timestamp when the batch was posted (optional).</summary>
    [Id(12)] public DateTime? PostedDate { get; set; }

    /// <summary>Free-text notes about this batch.</summary>
    [Id(13)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this batch record was first created.</summary>
    [Id(14)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
