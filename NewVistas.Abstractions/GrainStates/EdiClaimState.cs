// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// X12 837 claim type — Professional (CMS-1500) or Institutional (UB-04).
/// </summary>
[GenerateSerializer]
public enum EdiClaimType
{
    /// <summary>CMS-1500 / X12 837P — physician and outpatient services.</summary>
    Professional837P = 0,

    /// <summary>UB-04 / X12 837I — inpatient hospital and facility services.</summary>
    Institutional837I = 1,
}

/// <summary>
/// Lifecycle status of an EDI electronic claim.
/// </summary>
[GenerateSerializer]
public enum EdiClaimStatus
{
    /// <summary>Claim prepared but not yet added to a transmission batch.</summary>
    Draft = 0,

    /// <summary>Claim added to a transmission batch, awaiting send.</summary>
    InTransmission = 1,

    /// <summary>Transmission batch has been sent to payer.</summary>
    Transmitted = 2,

    /// <summary>Payer has acknowledged receipt (999 functional acknowledgment).</summary>
    Acknowledged = 3,

    /// <summary>Payer accepted the claim for adjudication.</summary>
    Accepted = 4,

    /// <summary>Payer rejected the claim (technical or editorial rejection).</summary>
    Rejected = 5,

    /// <summary>Claim paid in full by payer via ERA.</summary>
    Paid = 6,

    /// <summary>Claim denied by payer — no payment issued.</summary>
    Denied = 7,

    /// <summary>Payer paid less than billed; adjustment codes present.</summary>
    PartiallyPaid = 8,
}

/// <summary>
/// A single service line item within an EDI 837 claim.
/// Maps to loop 2400 (SV1/SV2) of the X12 837 transaction set.
/// </summary>
[GenerateSerializer]
public record EdiServiceLine
{
    /// <summary>Sequential line number within the claim (1-based).</summary>
    [Id(0)] public int LineNumber { get; init; }

    /// <summary>CPT or HCPCS procedure code.</summary>
    [Id(1)] public string ProcedureCode { get; init; } = string.Empty;

    /// <summary>Optional procedure modifier (e.g., "25", "59").</summary>
    [Id(2)] public string? ProcedureModifier { get; init; }

    /// <summary>Diagnosis pointer linking to the claim's diagnosis codes (e.g., "1", "1:2").</summary>
    [Id(3)] public string? DiagnosisPointer { get; init; }

    /// <summary>Amount billed for this service line.</summary>
    [Id(4)] public decimal BilledAmount { get; init; }

    /// <summary>Units of service rendered.</summary>
    [Id(5)] public decimal Units { get; init; } = 1m;
}

/// <summary>
/// State for an electronic claim (X12 837P/837I) submitted to an insurance payer.
/// Maps to VistA Files #361 (ELECTRONIC CLAIM) and #362 (ELECTRONIC CLAIM TRANSMISSION).
/// </summary>
[GenerateSerializer]
public class EdiClaimState
{
    /// <summary>Unique identifier for this claim — grain key string.</summary>
    [Id(0)] public string ClaimId { get; set; } = string.Empty;

    /// <summary>Patient for whom the claim is filed.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Source IB billing action (File #350) that triggered this claim.</summary>
    [Id(2)] public string? BillingActionId { get; set; }

    /// <summary>AR account to credit when ERA payment is received.</summary>
    [Id(3)] public string? ARAccountId { get; set; }

    /// <summary>Insurance plan ID from the IB insurance plan record.</summary>
    [Id(4)] public string? InsurancePlanId { get; set; }

    /// <summary>Patient's personal policy ID.</summary>
    [Id(5)] public string? PolicyId { get; set; }

    /// <summary>Payer (insurance company) identifier (PAYER ID / NPI).</summary>
    [Id(6)] public string PayerId { get; set; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(7)] public string PayerName { get; set; } = string.Empty;

    /// <summary>Whether this is a Professional (837P) or Institutional (837I) claim.</summary>
    [Id(8)] public EdiClaimType ClaimType { get; set; }

    /// <summary>Current processing status of the claim.</summary>
    [Id(9)] public EdiClaimStatus Status { get; set; }

    /// <summary>Total dollar amount billed to the payer.</summary>
    [Id(10)] public decimal TotalBilledAmount { get; set; }

    /// <summary>Amount allowed by the payer (set when ERA received).</summary>
    [Id(11)] public decimal? AllowedAmount { get; set; }

    /// <summary>Amount paid by the payer (set when ERA received).</summary>
    [Id(12)] public decimal? PaidAmount { get; set; }

    /// <summary>Contractual or other adjustment amount (set when ERA received).</summary>
    [Id(13)] public decimal? AdjustmentAmount { get; set; }

    /// <summary>CARC/RARC denial reason code (set when ERA received with denial).</summary>
    [Id(14)] public string? DenialReasonCode { get; set; }

    /// <summary>Human-readable denial reason description.</summary>
    [Id(15)] public string? DenialReasonDescription { get; set; }

    /// <summary>Individual service line items (loop 2400).</summary>
    [Id(16)] public List<EdiServiceLine> ServiceLines { get; set; } = new();

    /// <summary>ICD-10-CM diagnosis codes for this claim (loop 2300 HI segment).</summary>
    [Id(17)] public List<string> DiagnosisCodes { get; set; } = new();

    /// <summary>Date of first service on the claim.</summary>
    [Id(18)] public DateTime ServiceFromDate { get; set; }

    /// <summary>Date of last service (for multi-day inpatient stays).</summary>
    [Id(19)] public DateTime? ServiceToDate { get; set; }

    /// <summary>Transmission batch this claim belongs to.</summary>
    [Id(20)] public string? TransmissionId { get; set; }

    /// <summary>ERA that processed payment/denial for this claim.</summary>
    [Id(21)] public string? EraId { get; set; }

    /// <summary>Date the claim was sent to the payer.</summary>
    [Id(22)] public DateTime? SubmittedDate { get; set; }

    /// <summary>Date the payer acknowledged receipt of the claim.</summary>
    [Id(23)] public DateTime? AcknowledgedDate { get; set; }

    /// <summary>Date payment was received from the payer.</summary>
    [Id(24)] public DateTime? PaymentDate { get; set; }

    /// <summary>Optional free-text notes.</summary>
    [Id(25)] public string? Notes { get; set; }

    /// <summary>When this claim record was created.</summary>
    [Id(26)] public DateTime CreatedDate { get; set; }

    /// <summary>When this claim record was last modified.</summary>
    [Id(27)] public DateTime LastModifiedDate { get; set; }
}
