// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Organ type being transplanted or awaited.</summary>
[GenerateSerializer]
public enum TransplantOrganType
{
    Kidney,
    Liver,
    Heart,
    Lung,
    Pancreas,
    SmallBowel,
    KidneyPancreas,
    HeartLung,
    Other,
}

/// <summary>Patient's current status on the transplant waitlist.</summary>
[GenerateSerializer]
public enum TransplantStatus
{
    /// <summary>Referred; evaluation not yet complete.</summary>
    PendingEvaluation,
    /// <summary>Formally listed on the UNOS waiting list.</summary>
    Listed,
    /// <summary>Temporarily inactive (on hold — medical or other reason).</summary>
    OnHold,
    /// <summary>Removed from waitlist (recovered, declined, or transferred).</summary>
    Removed,
    /// <summary>Transplant has been performed.</summary>
    Transplanted,
    /// <summary>Patient died while on waiting list.</summary>
    Deceased,
}

/// <summary>UNOS-inspired urgency/priority tier for waitlist ordering.</summary>
[GenerateSerializer]
public enum TransplantPriority
{
    /// <summary>Standard waitlist priority (calculated by MELD/PELD or wait time).</summary>
    Standard,
    /// <summary>Urgent — escalated priority due to deteriorating condition.</summary>
    Urgent,
    /// <summary>Status 1A — highest urgency (heart/liver — imminent risk of death).</summary>
    Status1A,
    /// <summary>Status 1B — high urgency, hospitalized or requires continuous support.</summary>
    Status1B,
}

/// <summary>Type of organ donor.</summary>
[GenerateSerializer]
public enum DonorType
{
    /// <summary>Donation after brain death (DBD) or donation after cardiac death (DCD).</summary>
    DeceasedDonor,
    /// <summary>Living related or unrelated donor.</summary>
    LivingDonor,
    /// <summary>Split-liver donation (one liver divided between two recipients).</summary>
    SplitLiver,
}

/// <summary>Current disposition of a donor organ.</summary>
[GenerateSerializer]
public enum DonorStatus
{
    /// <summary>Organ recovered and available for allocation.</summary>
    Available,
    /// <summary>Organ allocated to a specific recipient; awaiting transplant.</summary>
    Allocated,
    /// <summary>Transplant completed.</summary>
    Transplanted,
    /// <summary>Organ discarded (not suitable or no match found in time).</summary>
    Discarded,
    /// <summary>Cold ischemia time exceeded; organ no longer viable.</summary>
    Expired,
}

/// <summary>ABO blood type.</summary>
[GenerateSerializer]
public enum BloodType
{
    OPositive,
    ONegative,
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative,
}

// ── Index / Summary Types ──────────────────────────────────────────────────────

/// <summary>Lightweight waitlist entry for the system-wide index.</summary>
[GenerateSerializer]
public class TransplantWaitlistEntry
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Organ type the patient needs.</summary>
    [Id(2)] public TransplantOrganType OrganType { get; set; }

    /// <summary>Current waitlist status.</summary>
    [Id(3)] public TransplantStatus Status { get; set; }

    /// <summary>Urgency/priority tier.</summary>
    [Id(4)] public TransplantPriority Priority { get; set; }

    /// <summary>Date patient was listed on the waitlist.</summary>
    [Id(5)] public DateTime ListedDate { get; set; }

    /// <summary>Patient blood type (for quick compatibility filtering).</summary>
    [Id(6)] public BloodType BloodType { get; set; }

    /// <summary>Patient age in years at time of listing.</summary>
    [Id(7)] public int AgeYears { get; set; }

    /// <summary>Primary diagnosis driving transplant need.</summary>
    [Id(8)] public string PrimaryDiagnosis { get; set; } = string.Empty;

    /// <summary>UNOS registration identifier.</summary>
    [Id(9)] public string? UnosId { get; set; }

    /// <summary>Transplant center location identifier.</summary>
    [Id(10)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Date the entry was last modified.</summary>
    [Id(11)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight donor organ summary for the system-wide index.</summary>
[GenerateSerializer]
public class TransplantDonorSummaryEntry
{
    /// <summary>Donor grain identifier.</summary>
    [Id(0)] public string DonorId { get; set; } = string.Empty;

    /// <summary>Organ type available for transplant.</summary>
    [Id(1)] public TransplantOrganType OrganType { get; set; }

    /// <summary>Type of donor (deceased, living, split).</summary>
    [Id(2)] public DonorType DonorType { get; set; }

    /// <summary>Donor blood type.</summary>
    [Id(3)] public BloodType BloodType { get; set; }

    /// <summary>Current disposition of the organ.</summary>
    [Id(4)] public DonorStatus Status { get; set; }

    /// <summary>Donor age in years.</summary>
    [Id(5)] public int DonorAgeYears { get; set; }

    /// <summary>Organ recovery date/time.</summary>
    [Id(6)] public DateTime RecoveryDateTime { get; set; }

    /// <summary>Organ viability expiration (end of cold ischemia window).</summary>
    [Id(7)] public DateTime? ExpirationDateTime { get; set; }

    /// <summary>Location where organ was recovered.</summary>
    [Id(8)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Patient ID the organ has been allocated to (if any).</summary>
    [Id(9)] public string? MatchedPatientId { get; set; }

    /// <summary>Patient name the organ has been allocated to (if any).</summary>
    [Id(10)] public string? MatchedPatientName { get; set; }
}

// ── State Classes ──────────────────────────────────────────────────────────────

/// <summary>
/// Full transplant record for a patient on (or removed from) the waiting list.
/// Inspired by UNOS/OPTN patient registration data.
/// </summary>
[GenerateSerializer]
public class TransplantPatientState
{
    /// <summary>(.01) Patient identifier (grain key).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>(.02) Patient full name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>(.03) Patient date of birth.</summary>
    [Id(2)] public DateTime? DateOfBirth { get; set; }

    /// <summary>(.04) Organ type needed.</summary>
    [Id(3)] public TransplantOrganType OrganType { get; set; }

    /// <summary>(.05) Current waitlist status.</summary>
    [Id(4)] public TransplantStatus Status { get; set; }

    /// <summary>(.06) Urgency/priority tier.</summary>
    [Id(5)] public TransplantPriority Priority { get; set; }

    /// <summary>(.07) Date patient was listed (or re-listed).</summary>
    [Id(6)] public DateTime? ListedDate { get; set; }

    /// <summary>(.08) Date patient was removed from the waitlist (if applicable).</summary>
    [Id(7)] public DateTime? RemovedDate { get; set; }

    /// <summary>(.09) Reason for removal from waitlist.</summary>
    [Id(8)] public string? RemovalReason { get; set; }

    /// <summary>(.10) UNOS patient registration number.</summary>
    [Id(9)] public string? UnosId { get; set; }

    /// <summary>(.11) Patient ABO blood type.</summary>
    [Id(10)] public BloodType BloodType { get; set; }

    /// <summary>(.12) HLA (Human Leukocyte Antigen) typing string (e.g., "A2,A24,B7,B35,DR1,DR4").</summary>
    [Id(11)] public string? HlaTyping { get; set; }

    /// <summary>(.13) Panel Reactive Antibody percentage (0–100); high values indicate sensitization.</summary>
    [Id(12)] public decimal? PanelReactiveAntibodyPct { get; set; }

    /// <summary>(.14) Primary diagnosis driving transplant need.</summary>
    [Id(13)] public string PrimaryDiagnosis { get; set; } = string.Empty;

    /// <summary>(.15) ICD-10 diagnosis code.</summary>
    [Id(14)] public string? DiagnosisCode { get; set; }

    /// <summary>(.16) Patient weight in kilograms.</summary>
    [Id(15)] public decimal? WeightKg { get; set; }

    /// <summary>(.17) Patient height in centimeters.</summary>
    [Id(16)] public decimal? HeightCm { get; set; }

    /// <summary>(.18) Calculated MELD/PELD score (liver) or equivalent urgency score.</summary>
    [Id(17)] public decimal? CalculatedMeldScore { get; set; }

    /// <summary>Accepted donor types (e.g., deceased only, or living also acceptable).</summary>
    [Id(18)] public List<DonorType> AcceptedDonorTypes { get; set; } = new();

    /// <summary>(.19) Transplant center location identifier.</summary>
    [Id(19)] public string LocationId { get; set; } = string.Empty;

    /// <summary>(.20) Transplant center location name.</summary>
    [Id(20)] public string LocationName { get; set; } = string.Empty;

    /// <summary>(.21) Referring provider user ID.</summary>
    [Id(21)] public string? ReferringProviderId { get; set; }

    /// <summary>(.22) Referring provider full name.</summary>
    [Id(22)] public string? ReferringProviderName { get; set; }

    /// <summary>(.23) Transplant surgeon user ID (set when transplant performed).</summary>
    [Id(23)] public string? TransplantSurgeonId { get; set; }

    /// <summary>(.24) Transplant surgeon full name.</summary>
    [Id(24)] public string? TransplantSurgeonName { get; set; }

    /// <summary>(.25) Date transplant was performed.</summary>
    [Id(25)] public DateTime? TransplantDate { get; set; }

    /// <summary>(.26) Donor grain ID used for the transplant.</summary>
    [Id(26)] public string? TransplantDonorId { get; set; }

    /// <summary>(.27) General notes.</summary>
    [Id(27)] public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(28)] public DateTime CreatedDate { get; set; }

    /// <summary>Date this record was last modified.</summary>
    [Id(29)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Full state for a donor organ record.
/// Tracks the organ from recovery through allocation and transplant (or discard).
/// </summary>
[GenerateSerializer]
public class TransplantDonorState
{
    /// <summary>(.01) Donor grain identifier.</summary>
    [Id(0)] public string DonorId { get; set; } = string.Empty;

    /// <summary>(.02) Type of donor (deceased, living, split).</summary>
    [Id(1)] public DonorType DonorType { get; set; }

    /// <summary>(.03) Organ type.</summary>
    [Id(2)] public TransplantOrganType OrganType { get; set; }

    /// <summary>(.04) Donor name (or de-identified label for deceased).</summary>
    [Id(3)] public string DonorName { get; set; } = string.Empty;

    /// <summary>(.05) Donor date of birth.</summary>
    [Id(4)] public DateTime? DateOfBirth { get; set; }

    /// <summary>(.06) Donor ABO blood type.</summary>
    [Id(5)] public BloodType BloodType { get; set; }

    /// <summary>(.07) Donor weight in kilograms.</summary>
    [Id(6)] public decimal? WeightKg { get; set; }

    /// <summary>(.08) Donor height in centimeters.</summary>
    [Id(7)] public decimal? HeightCm { get; set; }

    /// <summary>(.09) Cause of death (for deceased donors).</summary>
    [Id(8)] public string? CauseOfDeath { get; set; }

    /// <summary>(.10) Cross-clamp date/time (start of cold ischemia for deceased donors).</summary>
    [Id(9)] public DateTime? CrossClampDateTime { get; set; }

    /// <summary>(.11) Organ recovery (procurement) date/time.</summary>
    [Id(10)] public DateTime RecoveryDateTime { get; set; }

    /// <summary>(.12) Viability expiration — end of maximum cold ischemia window.</summary>
    [Id(11)] public DateTime? ExpirationDateTime { get; set; }

    /// <summary>(.13) HLA typing of donor organ.</summary>
    [Id(12)] public string? HlaTyping { get; set; }

    /// <summary>(.14) Cold ischemia time in hours (calculated or entered).</summary>
    [Id(13)] public decimal? ColdIschemiaTimeHours { get; set; }

    /// <summary>(.15) Recovery location identifier.</summary>
    [Id(14)] public string LocationId { get; set; } = string.Empty;

    /// <summary>(.16) Recovery location name.</summary>
    [Id(15)] public string LocationName { get; set; } = string.Empty;

    /// <summary>(.17) ID of staff member who performed recovery.</summary>
    [Id(16)] public string RecoveredById { get; set; } = string.Empty;

    /// <summary>(.18) Name of staff member who performed recovery.</summary>
    [Id(17)] public string RecoveredByName { get; set; } = string.Empty;

    /// <summary>(.19) Current disposition of this organ.</summary>
    [Id(18)] public DonorStatus Status { get; set; }

    /// <summary>(.20) Patient ID this organ has been allocated to.</summary>
    [Id(19)] public string? AllocatedToPatientId { get; set; }

    /// <summary>(.21) Patient name this organ has been allocated to.</summary>
    [Id(20)] public string? AllocatedToPatientName { get; set; }

    /// <summary>(.22) Date/time organ was allocated to recipient.</summary>
    [Id(21)] public DateTime? AllocationDateTime { get; set; }

    /// <summary>(.23) Date/time transplant was performed.</summary>
    [Id(22)] public DateTime? TransplantDateTime { get; set; }

    /// <summary>(.24) Reason for discarding the organ (if applicable).</summary>
    [Id(23)] public string? DiscardReason { get; set; }

    /// <summary>(.25) General notes.</summary>
    [Id(24)] public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(25)] public DateTime CreatedDate { get; set; }

    /// <summary>Date this record was last modified.</summary>
    [Id(26)] public DateTime LastModifiedDate { get; set; }
}
