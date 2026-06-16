// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Status of an EDI 270/271 eligibility verification transaction.
/// </summary>
[GenerateSerializer]
public enum EligibilityInquiryStatus
{
    /// <summary>Inquiry prepared but not yet submitted to payer.</summary>
    Draft = 0,

    /// <summary>Inquiry submitted to payer, awaiting response.</summary>
    Submitted = 1,

    /// <summary>Payer responded — patient is eligible.</summary>
    Eligible = 2,

    /// <summary>Payer responded — patient is not eligible.</summary>
    Ineligible = 3,

    /// <summary>Payer could not process the inquiry (error/reject).</summary>
    Error = 4,

    /// <summary>Inquiry expired without receiving a response.</summary>
    Expired = 5,
}

/// <summary>
/// Coverage level returned by the payer in the 271 response.
/// Maps to X12 271 EB01 (Eligibility or Benefit Information Code).
/// </summary>
[GenerateSerializer]
public enum CoverageLevel
{
    /// <summary>Coverage level not yet determined.</summary>
    Unknown = 0,

    /// <summary>Active coverage — full benefits.</summary>
    ActiveCoverage = 1,

    /// <summary>Active coverage — limited or restricted benefits.</summary>
    ActiveLimited = 2,

    /// <summary>Inactive coverage — not currently eligible.</summary>
    Inactive = 3,

    /// <summary>Coverage has been terminated.</summary>
    Terminated = 4,

    /// <summary>Pending — enrollment in process.</summary>
    Pending = 5,
}

/// <summary>
/// A single benefit detail line from the 271 response.
/// Maps to X12 271 loop 2110C/2110D EB segments.
/// </summary>
[GenerateSerializer]
public record EligibilityBenefitDetail
{
    /// <summary>Benefit type: DEDUCTIBLE, COPAY, COINSURANCE, OUT_OF_POCKET, LIMITATION, etc.</summary>
    [Id(0)] public string BenefitType { get; init; } = string.Empty;

    /// <summary>Service type code (e.g., "30"=Health Benefit Plan Coverage, "1"=Medical, "47"=Hospital).</summary>
    [Id(1)] public string? ServiceTypeCode { get; init; }

    /// <summary>Service type description.</summary>
    [Id(2)] public string? ServiceTypeDescription { get; init; }

    /// <summary>Time period qualifier: CALENDAR_YEAR, VISIT, REMAINING, LIFETIME, etc.</summary>
    [Id(3)] public string? TimePeriod { get; init; }

    /// <summary>Dollar amount (for deductible, copay, out-of-pocket max).</summary>
    [Id(4)] public decimal? Amount { get; init; }

    /// <summary>Percentage (for coinsurance, e.g., 0.20 = 20%).</summary>
    [Id(5)] public decimal? Percentage { get; init; }

    /// <summary>In-network or out-of-network indicator.</summary>
    [Id(6)] public string? NetworkIndicator { get; init; }

    /// <summary>Additional payer message or description.</summary>
    [Id(7)] public string? Message { get; init; }
}

/// <summary>
/// State for an insurance eligibility verification transaction (EDI 270 inquiry + 271 response).
/// Maps to VistA IB ELIGIBILITY VERIFICATION and X12 270/271 transaction sets.
/// MUMPS: IBCNEDE*.m, IBCNEUT.m, IBCNEDE1.m
/// Grain key: "ELIG-270:{guid}"
/// </summary>
[GenerateSerializer]
public class EligibilityInquiryState
{
    /// <summary>Unique inquiry identifier — grain key string. (.001)</summary>
    [Id(0)] public string InquiryId { get; set; } = string.Empty;

    /// <summary>Patient for whom eligibility is being verified. (.01)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Insurance plan being verified against. (.02)</summary>
    [Id(2)] public string? InsurancePlanId { get; set; }

    /// <summary>Patient's personal policy being verified. (.03)</summary>
    [Id(3)] public string? PersonalPolicyId { get; set; }

    /// <summary>Payer identifier (insurance company ID / NPI). (.04)</summary>
    [Id(4)] public string PayerId { get; set; } = string.Empty;

    /// <summary>Payer display name.</summary>
    [Id(5)] public string PayerName { get; set; } = string.Empty;

    /// <summary>Subscriber ID / member number sent in the 270. (.05)</summary>
    [Id(6)] public string SubscriberId { get; set; } = string.Empty;

    /// <summary>Subscriber name (last, first).</summary>
    [Id(7)] public string? SubscriberName { get; set; }

    /// <summary>Patient's relationship to subscriber (SELF, SPOUSE, CHILD, OTHER).</summary>
    [Id(8)] public string? RelationshipToSubscriber { get; set; }

    /// <summary>Patient date of birth sent in the 270 for verification.</summary>
    [Id(9)] public DateTime? PatientDateOfBirth { get; set; }

    /// <summary>Service type code(s) being inquired about (e.g., "30" = Health Benefit Plan Coverage).</summary>
    [Id(10)] public List<string> ServiceTypeCodes { get; set; } = new();

    /// <summary>Date of service for which eligibility is being verified.</summary>
    [Id(11)] public DateTime ServiceDate { get; set; }

    /// <summary>Current status of the eligibility inquiry.</summary>
    [Id(12)] public EligibilityInquiryStatus Status { get; set; }

    /// <summary>Date/time the inquiry was submitted to the payer.</summary>
    [Id(13)] public DateTime? SubmittedDate { get; set; }

    /// <summary>Date/time the response was received from the payer.</summary>
    [Id(14)] public DateTime? ResponseDate { get; set; }

    /// <summary>User who initiated the inquiry.</summary>
    [Id(15)] public string? InitiatedByUserId { get; set; }

    /// <summary>User display name.</summary>
    [Id(16)] public string? InitiatedByUserName { get; set; }

    // ─── 271 Response Fields ─────────────────────────────────────────────────

    /// <summary>Coverage level returned by the payer.</summary>
    [Id(17)] public CoverageLevel CoverageLevel { get; set; }

    /// <summary>Plan name as returned by the payer in the 271.</summary>
    [Id(18)] public string? ResponsePlanName { get; set; }

    /// <summary>Coverage effective date from the 271.</summary>
    [Id(19)] public DateTime? CoverageEffectiveDate { get; set; }

    /// <summary>Coverage termination date from the 271.</summary>
    [Id(20)] public DateTime? CoverageTerminationDate { get; set; }

    /// <summary>Group number as returned by the payer.</summary>
    [Id(21)] public string? ResponseGroupNumber { get; set; }

    /// <summary>Benefit details returned in the 271 EB segments.</summary>
    [Id(22)] public List<EligibilityBenefitDetail> BenefitDetails { get; set; } = new();

    /// <summary>Payer reject/error reason codes from AAA segments.</summary>
    [Id(23)] public List<string> RejectReasonCodes { get; set; } = new();

    /// <summary>Payer free-text message from the 271 response.</summary>
    [Id(24)] public string? PayerMessage { get; set; }

    /// <summary>X12 trace number for the 270 transaction.</summary>
    [Id(25)] public string? TraceNumber { get; set; }

    /// <summary>Optional notes entered by the user.</summary>
    [Id(26)] public string? Notes { get; set; }

    /// <summary>When this record was created.</summary>
    [Id(27)] public DateTime CreatedDate { get; set; }

    /// <summary>When this record was last modified.</summary>
    [Id(28)] public DateTime LastModifiedDate { get; set; }
}
