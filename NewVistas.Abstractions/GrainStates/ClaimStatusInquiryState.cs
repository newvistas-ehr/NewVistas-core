// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Status of an EDI 276/277 claim status inquiry transaction.
/// </summary>
[GenerateSerializer]
public enum ClaimStatusInquiryStatus
{
    /// <summary>Inquiry created but not yet submitted.</summary>
    Draft = 0,
    /// <summary>Submitted to payer, awaiting response.</summary>
    Submitted = 1,
    /// <summary>Payer responded with claim status information.</summary>
    ResponseReceived = 2,
    /// <summary>Payer returned an error or could not process.</summary>
    Error = 3,
}

/// <summary>
/// Claim status category from the 277 response (STC segment).
/// Maps to X12 277 Health Care Claim Status Category Codes.
/// </summary>
[GenerateSerializer]
public enum ClaimStatusCategory
{
    Unknown = 0,
    /// <summary>A0–A6: Acknowledgment / Receipt</summary>
    Received = 1,
    /// <summary>P0–P5: Pending</summary>
    Pending = 2,
    /// <summary>F0–F4: Finalized / Payment</summary>
    Finalized = 3,
    /// <summary>R0–R16: Request for additional info</summary>
    AdditionalInfoRequested = 4,
    /// <summary>E0–E4: Error / Rejection</summary>
    Rejected = 5,
    /// <summary>D0: Denied</summary>
    Denied = 6,
}

/// <summary>
/// A single claim status detail line from the 277 response.
/// Maps to X12 277 STC (Status Information) segment.
/// </summary>
[GenerateSerializer]
public record ClaimStatusDetail
{
    /// <summary>Claim status category code (e.g., "A0"=Forwarded, "P1"=In Process).</summary>
    [Id(0)] public string StatusCategoryCode { get; init; } = string.Empty;

    /// <summary>Claim status category description.</summary>
    [Id(1)] public string StatusCategoryDescription { get; init; } = string.Empty;

    /// <summary>Claim status code (detail within category).</summary>
    [Id(2)] public string? StatusCode { get; init; }

    /// <summary>Status code description.</summary>
    [Id(3)] public string? StatusCodeDescription { get; init; }

    /// <summary>Effective date of this status.</summary>
    [Id(4)] public DateTime? EffectiveDate { get; init; }

    /// <summary>Total claim charge amount from payer's records.</summary>
    [Id(5)] public decimal? TotalChargeAmount { get; init; }

    /// <summary>Amount paid or allowed by payer.</summary>
    [Id(6)] public decimal? PaymentAmount { get; init; }

    /// <summary>Adjudication date.</summary>
    [Id(7)] public DateTime? AdjudicationDate { get; init; }

    /// <summary>Check/EFT number for payment.</summary>
    [Id(8)] public string? CheckNumber { get; init; }

    /// <summary>Payer-specific free text message.</summary>
    [Id(9)] public string? Message { get; init; }
}

/// <summary>
/// State for an EDI 276 claim status inquiry and 277 response.
/// Maps to VistA IB Claim Status (IBCSC*.m) and X12 276/277.
/// Grain key: "CSI-276:{guid}"
/// </summary>
[GenerateSerializer]
public class ClaimStatusInquiryState
{
    /// <summary>Inquiry ID — grain key.</summary>
    [Id(0)] public string InquiryId { get; set; } = string.Empty;

    /// <summary>Patient associated with this claim.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>EDI claim ID being inquired about.</summary>
    [Id(2)] public string ClaimId { get; set; } = string.Empty;

    /// <summary>Payer ID for this inquiry.</summary>
    [Id(3)] public string PayerId { get; set; } = string.Empty;

    /// <summary>Payer name.</summary>
    [Id(4)] public string PayerName { get; set; } = string.Empty;

    /// <summary>Current inquiry status.</summary>
    [Id(5)] public ClaimStatusInquiryStatus Status { get; set; }

    /// <summary>Date the 276 was submitted.</summary>
    [Id(6)] public DateTime? SubmittedDate { get; set; }

    /// <summary>Date the 277 response was received.</summary>
    [Id(7)] public DateTime? ResponseDate { get; set; }

    /// <summary>Overall claim status category from the 277.</summary>
    [Id(8)] public ClaimStatusCategory ClaimStatusCategory { get; set; }

    /// <summary>Status detail lines from the 277 STC segments.</summary>
    [Id(9)] public List<ClaimStatusDetail> StatusDetails { get; set; } = new();

    /// <summary>Total billed amount (echoed from the 276).</summary>
    [Id(10)] public decimal? TotalBilledAmount { get; set; }

    /// <summary>Payer's reported paid amount (from 277).</summary>
    [Id(11)] public decimal? ReportedPaidAmount { get; set; }

    /// <summary>X12 trace number for the 276.</summary>
    [Id(12)] public string? TraceNumber { get; set; }

    /// <summary>User who initiated the inquiry.</summary>
    [Id(13)] public string? InitiatedByUserId { get; set; }

    /// <summary>User display name.</summary>
    [Id(14)] public string? InitiatedByUserName { get; set; }

    /// <summary>Payer reject reason codes (from AAA segments).</summary>
    [Id(15)] public List<string> RejectReasonCodes { get; set; } = new();

    /// <summary>Payer free-text message.</summary>
    [Id(16)] public string? PayerMessage { get; set; }

    /// <summary>Optional notes.</summary>
    [Id(17)] public string? Notes { get; set; }

    /// <summary>When created.</summary>
    [Id(18)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(19)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight index entry for claim status inquiries.
/// </summary>
[GenerateSerializer]
public record ClaimStatusInquiryIndexEntry
{
    [Id(0)] public string InquiryId { get; init; } = string.Empty;
    [Id(1)] public string ClaimId { get; init; } = string.Empty;
    [Id(2)] public string PayerName { get; init; } = string.Empty;
    [Id(3)] public ClaimStatusInquiryStatus Status { get; init; }
    [Id(4)] public ClaimStatusCategory ClaimStatusCategory { get; init; }
    [Id(5)] public DateTime? SubmittedDate { get; init; }
    [Id(6)] public DateTime? ResponseDate { get; init; }
}

/// <summary>
/// Per-patient index of claim status inquiries.
/// Grain key: "CSI-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class ClaimStatusInquiryIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<ClaimStatusInquiryIndexEntry> Entries { get; set; } = new();
}
