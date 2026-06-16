// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single invoice line item within a fee basis batch payment run.
/// </summary>
[GenerateSerializer]
public record FeeBatchPaymentEntry
{
    /// <summary>Fee basis invoice grain key string (without prefix).</summary>
    [Id(0)] public string InvoiceId { get; init; } = string.Empty;

    /// <summary>Parent fee basis authorization ID.</summary>
    [Id(1)] public string AuthorizationId { get; init; } = string.Empty;

    /// <summary>Patient associated with this invoice.</summary>
    [Id(2)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Patient display name.</summary>
    [Id(3)] public string PatientName { get; init; } = string.Empty;

    /// <summary>Vendor (community care provider) ID.</summary>
    [Id(4)] public string VendorId { get; init; } = string.Empty;

    /// <summary>Vendor display name.</summary>
    [Id(5)] public string VendorName { get; init; } = string.Empty;

    /// <summary>Amount to be paid for this invoice.</summary>
    [Id(6)] public decimal PaidAmount { get; init; }
}

/// <summary>
/// State for a fee basis batch payment run — grouping approved invoices
/// for bulk vendor payment disbursement.
/// Maps to VistA File #162.7 (FEE BASIS BATCH PAYMENT).
/// MUMPS: FBCH*.m, FBAA*.m
/// </summary>
[GenerateSerializer]
public class FeeBatchPaymentState
{
    /// <summary>Unique identifier for this batch — grain key string.</summary>
    [Id(0)] public string BatchId { get; set; } = string.Empty;

    /// <summary>Primary vendor for this batch (optional if multi-vendor).</summary>
    [Id(1)] public string? VendorId { get; set; }

    /// <summary>Primary vendor display name.</summary>
    [Id(2)] public string? VendorName { get; set; }

    /// <summary>Date of this payment batch (check date or EFT date).</summary>
    [Id(3)] public DateTime BatchDate { get; set; }

    /// <summary>Payment method (e.g., "Check", "EFT", "ACH").</summary>
    [Id(4)] public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Check number or EFT trace number.</summary>
    [Id(5)] public string? CheckNumber { get; set; }

    /// <summary>Sum of all invoice paid amounts in this batch.</summary>
    [Id(6)] public decimal TotalAmount { get; set; }

    /// <summary>Invoice line items included in this batch payment run.</summary>
    [Id(7)] public List<FeeBatchPaymentEntry> InvoiceEntries { get; set; } = new();

    /// <summary>Whether this batch has been posted (invoices marked as Paid).</summary>
    [Id(8)] public bool IsPosted { get; set; }

    /// <summary>User ID who posted this batch.</summary>
    [Id(9)] public string? PostedByUserId { get; set; }

    /// <summary>Display name of the user who posted this batch.</summary>
    [Id(10)] public string? PostedByUserName { get; set; }

    /// <summary>When this batch was posted.</summary>
    [Id(11)] public DateTime? PostedDate { get; set; }

    /// <summary>Optional free-text notes.</summary>
    [Id(12)] public string? Notes { get; set; }

    /// <summary>When this batch record was created.</summary>
    [Id(13)] public DateTime CreatedDate { get; set; }

    /// <summary>When this batch record was last modified.</summary>
    [Id(14)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight summary of a fee basis batch payment for index queries.
/// </summary>
[GenerateSerializer]
public record FeeBatchPaymentIndexEntry
{
    /// <summary>Batch grain key string.</summary>
    [Id(0)] public string BatchId { get; init; } = string.Empty;

    /// <summary>Primary vendor display name (null if multi-vendor).</summary>
    [Id(1)] public string? VendorName { get; init; }

    /// <summary>Date of this payment batch.</summary>
    [Id(2)] public DateTime BatchDate { get; init; }

    /// <summary>Payment method string (e.g., "Check", "EFT").</summary>
    [Id(3)] public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Total amount across all invoices in this batch.</summary>
    [Id(4)] public decimal TotalAmount { get; init; }

    /// <summary>Number of invoices in this batch.</summary>
    [Id(5)] public int InvoiceCount { get; init; }

    /// <summary>Whether the batch has been posted.</summary>
    [Id(6)] public bool IsPosted { get; init; }

    /// <summary>When the batch was posted (null if not yet posted).</summary>
    [Id(7)] public DateTime? PostedDate { get; init; }
}

/// <summary>
/// Singleton index of all fee basis batch payments.
/// Grain key: "FEE-BATCH-IDX"
/// </summary>
[GenerateSerializer]
public class FeeBatchPaymentIndexState
{
    /// <summary>All batch payment entries.</summary>
    [Id(0)] public List<FeeBatchPaymentIndexEntry> Entries { get; set; } = new();
}
