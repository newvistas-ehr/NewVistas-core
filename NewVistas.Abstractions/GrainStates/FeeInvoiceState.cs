// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee invoice status ────────────────────────────────────────────────────────

/// <summary>Lifecycle status of a fee basis invoice submitted by a community care vendor.</summary>
[GenerateSerializer]
public enum FeeInvoiceStatus
{
    /// <summary>Invoice received from vendor; not yet reviewed.</summary>
    Received = 0,

    /// <summary>Invoice is currently under clinical/financial review.</summary>
    UnderReview = 1,

    /// <summary>Invoice approved for payment; awaiting disbursement.</summary>
    Approved = 2,

    /// <summary>Invoice rejected; payment will not be made.</summary>
    Rejected = 3,

    /// <summary>Invoice fully paid; disbursement complete.</summary>
    Paid = 4,

    /// <summary>Invoice processing placed on hold pending additional information.</summary>
    OnHold = 5,
}

// ─── Fee Invoice — VistA File #162.1 FEE BASIS INVOICE ───────────────────────

/// <summary>
/// An invoice submitted by a community care vendor for services rendered under a
/// fee basis authorization (VistA File #162.1).
/// Managed by FBPAID.m, FBCLAIM.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class FeeInvoiceState
{
    /// <summary>Unique identifier for this invoice (Guid).</summary>
    [Id(0)] public string InvoiceId { get; set; } = string.Empty;

    /// <summary>Authorization under which the services were rendered.</summary>
    [Id(1)] public string AuthorizationId { get; set; } = string.Empty;

    /// <summary>Patient who received the services.</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Vendor (community care provider) who submitted the invoice.</summary>
    [Id(3)] public string VendorId { get; set; } = string.Empty;

    /// <summary>Vendor display name at time of invoice submission.</summary>
    [Id(4)] public string VendorName { get; set; } = string.Empty;

    /// <summary>Vendor-assigned invoice number.</summary>
    [Id(5)] public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Current lifecycle status of this invoice.</summary>
    [Id(6)] public FeeInvoiceStatus Status { get; set; } = FeeInvoiceStatus.Received;

    /// <summary>Date services were rendered (start date if a range).</summary>
    [Id(7)] public DateTime ServiceDate { get; set; }

    /// <summary>End date of the service period (optional; for multi-day admissions).</summary>
    [Id(8)] public DateTime? ServiceDateEnd { get; set; }

    /// <summary>Type of service delivered (e.g., "Outpatient", "Inpatient").</summary>
    [Id(9)] public string ServiceType { get; set; } = string.Empty;

    /// <summary>Primary ICD-10 diagnosis code (optional).</summary>
    [Id(10)] public string? DiagnosisCode { get; set; }

    /// <summary>CPT/HCPCS procedure codes billed on this invoice.</summary>
    [Id(11)] public List<string> ProcedureCodes { get; set; } = new();

    /// <summary>Total dollar amount billed by the vendor.</summary>
    [Id(12)] public decimal BilledAmount { get; set; }

    /// <summary>Amount approved for payment after review (optional until approved).</summary>
    [Id(13)] public decimal? ApprovedAmount { get; set; }

    /// <summary>Amount actually disbursed (optional until paid).</summary>
    [Id(14)] public decimal? PaidAmount { get; set; }

    /// <summary>User ID of the VA reviewer who approved or rejected this invoice.</summary>
    [Id(15)] public string? ReviewedByUserId { get; set; }

    /// <summary>Display name of the VA reviewer.</summary>
    [Id(16)] public string? ReviewerName { get; set; }

    /// <summary>Date the invoice was reviewed (approved or rejected).</summary>
    [Id(17)] public DateTime? ReviewedDate { get; set; }

    /// <summary>Method used to pay the vendor (e.g., "EFT", "Check").</summary>
    [Id(18)] public string? PaymentMethod { get; set; }

    /// <summary>Date payment was disbursed to the vendor.</summary>
    [Id(19)] public DateTime? PaymentDate { get; set; }

    /// <summary>Check or EFT trace number used for payment (optional).</summary>
    [Id(20)] public string? CheckNumber { get; set; }

    /// <summary>Reason the invoice was rejected (populated only if Status = Rejected).</summary>
    [Id(21)] public string? RejectionReason { get; set; }

    /// <summary>Free-text notes about this invoice.</summary>
    [Id(22)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(23)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(24)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
