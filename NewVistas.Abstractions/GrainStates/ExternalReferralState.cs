// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an external (community care) referral.
/// Maps to IHS RPMS RCIS referral record and VA Community Care referral tracking.
/// Tracks the full lifecycle of a referral sent to an outside provider/facility.
/// </summary>
[GenerateSerializer]
public class ExternalReferralState
{
    /// <summary>Unique referral ID (grain key, e.g., "EXT-REF:{guid}").</summary>
    [Id(0)]
    public string ReferralId { get; set; } = string.Empty;

    /// <summary>Patient this referral is for.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Referral type: SPECIALTY, DIAGNOSTIC, PROCEDURE, CONSULTATION, EMERGENCY.</summary>
    [Id(3)]
    public string ReferralType { get; set; } = string.Empty;

    /// <summary>External facility/organization name.</summary>
    [Id(4)]
    public string ExternalFacilityName { get; set; } = string.Empty;

    /// <summary>External facility ID (NPI, station number, etc.).</summary>
    [Id(5)]
    public string? ExternalFacilityId { get; set; }

    /// <summary>External provider name at the receiving facility.</summary>
    [Id(6)]
    public string? ExternalProviderName { get; set; }

    /// <summary>External provider ID (NPI).</summary>
    [Id(7)]
    public string? ExternalProviderId { get; set; }

    /// <summary>Purpose/reason for the referral.</summary>
    [Id(8)]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>Diagnosis or clinical indication.</summary>
    [Id(9)]
    public string? Diagnosis { get; set; }

    /// <summary>Urgency: ROUTINE, URGENT, STAT.</summary>
    [Id(10)]
    public string Urgency { get; set; } = "ROUTINE";

    /// <summary>Status: SUBMITTED, AUTHORIZED, SCHEDULED, IN_PROGRESS, COMPLETED, DENIED, CANCELLED.</summary>
    [Id(11)]
    public string Status { get; set; } = "SUBMITTED";

    /// <summary>Reason for current status (e.g., denial reason).</summary>
    [Id(12)]
    public string? StatusReason { get; set; }

    /// <summary>Provider who initiated the referral.</summary>
    [Id(13)]
    public string ReferredByProviderId { get; set; } = string.Empty;

    /// <summary>Name of referring provider.</summary>
    [Id(14)]
    public string ReferredByProviderName { get; set; } = string.Empty;

    /// <summary>Date the referral was submitted.</summary>
    [Id(15)]
    public DateTime ReferralDate { get; set; }

    /// <summary>Linked consult ID (if originated from a consult request).</summary>
    [Id(16)]
    public string? ConsultId { get; set; }

    /// <summary>Fee Basis authorization number (if applicable).</summary>
    [Id(17)]
    public string? AuthorizationNumber { get; set; }

    /// <summary>Scheduled appointment date/time at external facility.</summary>
    [Id(18)]
    public DateTime? AppointmentDateTime { get; set; }

    /// <summary>Appointment confirmation number from external facility.</summary>
    [Id(19)]
    public string? ConfirmationNumber { get; set; }

    /// <summary>Date the external care was completed.</summary>
    [Id(20)]
    public DateTime? CompletionDate { get; set; }

    /// <summary>Outcome notes from the external provider.</summary>
    [Id(21)]
    public string? OutcomeNotes { get; set; }

    /// <summary>Clinical findings from external care.</summary>
    [Id(22)]
    public string? ClinicalFindings { get; set; }

    /// <summary>Special instructions for the external provider.</summary>
    [Id(23)]
    public string? SpecialInstructions { get; set; }

    /// <summary>Follow-up notes and tracking entries.</summary>
    [Id(24)]
    public List<ReferralFollowUp> FollowUps { get; set; } = new();

    /// <summary>Attached document references (results, reports, etc.).</summary>
    [Id(25)]
    public List<ReferralDocument> Documents { get; set; } = new();

    /// <summary>Whether follow-up action is still needed.</summary>
    [Id(26)]
    public bool RequiresFollowUp { get; set; } = true;

    [Id(27)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(28)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ─── Contract Health Services (CHS / PRC) — IHS-specific ───────────────
    // These fields are used only when this referral is funded through the
    // tribe's Contract Health Service program (25 CFR Part 136). For
    // referrals to internal departments or non-CHS community care, all of
    // these stay at their defaults.

    /// <summary>
    /// True when this referral is funded through Contract Health Services
    /// and requires the CHS authorization workflow (eligibility check, fund
    /// approval, alternate-resources verification).
    /// </summary>
    [Id(29)]
    public bool IsChsReferral { get; set; }

    /// <summary>
    /// IHS Medical Priority Class (per 25 CFR § 136.23): "I" emergent,
    /// "II" acute, "III" non-emergent acute, "IV" chronic tertiary,
    /// "V" excluded. CHS dollars are typically allocated by class with
    /// classes IV–V deferred when funds are constrained.
    /// </summary>
    [Id(30)]
    public string? MedicalPriorityClass { get; set; }

    /// <summary>Estimated cost of the external service in USD, supplied at request time.</summary>
    [Id(31)]
    public decimal? EstimatedCost { get; set; }

    /// <summary>Authorized dollar amount in USD; set by the CHS coordinator at approval time.</summary>
    [Id(32)]
    public decimal? AuthorizedAmount { get; set; }

    /// <summary>
    /// Whether the requesting provider verified that the patient has been
    /// referred to alternate resources (Medicare/Medicaid/private insurance)
    /// before requesting CHS funding. Required by CHS as the payer of last
    /// resort.
    /// </summary>
    [Id(33)]
    public bool AlternateResourcesChecked { get; set; }

    /// <summary>Free-text note about alternate-resource status (e.g., "patient has Medicare Part B but service not covered").</summary>
    [Id(34)]
    public string? AlternateResourcesNote { get; set; }

    /// <summary>Date the CHS coordinator approved or denied the authorization.</summary>
    [Id(35)]
    public DateTime? ChsAuthorizationDate { get; set; }

    /// <summary>User id of the CHS coordinator who approved or denied.</summary>
    [Id(36)]
    public string? ChsAuthorizedById { get; set; }

    /// <summary>Display name of the CHS coordinator who approved or denied.</summary>
    [Id(37)]
    public string? ChsAuthorizedByName { get; set; }
}

[GenerateSerializer]
public class ReferralFollowUp
{
    [Id(0)]
    public DateTime FollowUpDate { get; set; }

    [Id(1)]
    public string AuthorName { get; set; } = string.Empty;

    [Id(2)]
    public string Note { get; set; } = string.Empty;
}

[GenerateSerializer]
public class ReferralDocument
{
    [Id(0)]
    public string DocumentId { get; set; } = string.Empty;

    [Id(1)]
    public string DocumentType { get; set; } = string.Empty;

    [Id(2)]
    public string Description { get; set; } = string.Empty;

    [Id(3)]
    public DateTime AttachedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Index entry for the system-level external referral index.
/// </summary>
[GenerateSerializer]
public class ExternalReferralIndexEntry
{
    [Id(0)]
    public string ReferralId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string ReferralType { get; set; } = string.Empty;

    [Id(4)]
    public string ExternalFacilityName { get; set; } = string.Empty;

    [Id(5)]
    public string Status { get; set; } = string.Empty;

    [Id(6)]
    public string Urgency { get; set; } = string.Empty;

    [Id(7)]
    public DateTime ReferralDate { get; set; }

    [Id(8)]
    public DateTime? AppointmentDateTime { get; set; }

    [Id(9)]
    public string? ReferredByProviderName { get; set; }

    [Id(10)]
    public bool RequiresFollowUp { get; set; }

    /// <summary>True when this referral is funded through Contract Health Services.</summary>
    [Id(11)]
    public bool IsChsReferral { get; set; }

    /// <summary>IHS Medical Priority Class for CHS-funded referrals (I, II, III, IV, V).</summary>
    [Id(12)]
    public string? MedicalPriorityClass { get; set; }

    /// <summary>Authorized dollar amount in USD (null for non-CHS referrals or those still pending).</summary>
    [Id(13)]
    public decimal? AuthorizedAmount { get; set; }
}
