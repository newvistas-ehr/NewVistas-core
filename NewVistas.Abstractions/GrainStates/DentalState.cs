// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Dental Procedure Category ────────────────────────────────────────────────

/// <summary>ADA CDT code range category for a dental procedure.</summary>
[GenerateSerializer]
public enum DentalProcedureCategory
{
    /// <summary>D0100–D0999 Diagnostic (exams, x-rays).</summary>
    Diagnostic = 0,

    /// <summary>D1000–D1999 Preventive (cleanings, fluoride, sealants).</summary>
    Preventive = 1,

    /// <summary>D2000–D2999 Restorative (fillings, crowns, inlays).</summary>
    Restorative = 2,

    /// <summary>D3000–D3999 Endodontic (root canals, pulp therapy).</summary>
    Endodontic = 3,

    /// <summary>D4000–D4999 Periodontic (scaling, root planing, surgery).</summary>
    Periodontic = 4,

    /// <summary>D5000–D5999 Prosthodontic (dentures, partials, obturators).</summary>
    Prosthodontic = 5,

    /// <summary>D6000–D6999 Implant services.</summary>
    Implant = 6,

    /// <summary>D7000–D7999 Oral and maxillofacial surgery (extractions, biopsies).</summary>
    OralSurgery = 7,

    /// <summary>D8000–D8999 Orthodontic treatment.</summary>
    Orthodontic = 8,

    /// <summary>D9000–D9999 Adjunctive general services (anesthesia, bleaching, misc.).</summary>
    Adjunctive = 9,
}

// ─── Dental Treatment Status ──────────────────────────────────────────────────

/// <summary>Lifecycle status of a dental treatment record.</summary>
[GenerateSerializer]
public enum DentalTreatmentStatus
{
    /// <summary>Treatment is planned but not yet started.</summary>
    Planned = 0,

    /// <summary>Treatment is actively in progress (multi-visit procedure).</summary>
    InProgress = 1,

    /// <summary>Treatment has been completed successfully.</summary>
    Completed = 2,

    /// <summary>Patient has been referred to a specialist for this treatment.</summary>
    Referred = 3,

    /// <summary>Treatment was cancelled before completion.</summary>
    Cancelled = 4,
}

// ─── Dental Eligibility Status ────────────────────────────────────────────────

/// <summary>VA dental care eligibility status for the patient.</summary>
[GenerateSerializer]
public enum DentalEligibilityStatus
{
    /// <summary>Eligibility not yet determined.</summary>
    Unknown = 0,

    /// <summary>Patient is fully eligible for VA dental care.</summary>
    Eligible = 1,

    /// <summary>Patient is eligible for limited dental services only.</summary>
    Limited = 2,

    /// <summary>Patient is not eligible for VA dental care.</summary>
    Ineligible = 3,
}

// ─── Dental Periodontal Status ────────────────────────────────────────────────

/// <summary>Patient's current periodontal (gum disease) classification.</summary>
[GenerateSerializer]
public enum DentalPeriodontalStatus
{
    /// <summary>No evidence of gum disease.</summary>
    Healthy = 0,

    /// <summary>Localized gingivitis (gum inflammation in specific areas).</summary>
    GingivitisLocalized = 1,

    /// <summary>Generalized gingivitis affecting most of the mouth.</summary>
    GingivitisGeneralized = 2,

    /// <summary>Localized mild periodontitis (bone loss in specific areas).</summary>
    PeriodontitisLightLocalized = 3,

    /// <summary>Generalized mild periodontitis.</summary>
    PeriodontitisLightGeneralized = 4,

    /// <summary>Localized moderate periodontitis.</summary>
    PeriodontitisModerateLocalized = 5,

    /// <summary>Generalized moderate periodontitis.</summary>
    PeriodontitisModerateGeneralized = 6,

    /// <summary>Localized severe periodontitis.</summary>
    PeriodontitisSevereLocalized = 7,

    /// <summary>Generalized severe periodontitis.</summary>
    PeriodontitisSevereGeneralized = 8,

    /// <summary>Patient has no remaining natural teeth.</summary>
    Edentulous = 9,
}

// ─── Dental Treatment Index Entry ─────────────────────────────────────────────

/// <summary>
/// Lightweight summary of a dental treatment for use in patient treatment indexes.
/// </summary>
[GenerateSerializer]
public class DentalTreatmentIndexEntry
{
    /// <summary>Unique treatment identifier (key of IDentalTreatmentGrain).</summary>
    [Id(0)] public string TreatmentId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>ADA CDT procedure code (e.g., "D2140").</summary>
    [Id(2)] public string ProcedureCode { get; set; } = string.Empty;

    /// <summary>Human-readable procedure description.</summary>
    [Id(3)] public string ProcedureDescription { get; set; } = string.Empty;

    /// <summary>High-level category of the procedure.</summary>
    [Id(4)] public DentalProcedureCategory ProcedureCategory { get; set; }

    /// <summary>Universal tooth number(s) affected (comma-separated, e.g., "14,15").</summary>
    [Id(5)] public string? ToothNumbers { get; set; }

    /// <summary>Date the treatment was performed or is planned.</summary>
    [Id(6)] public DateTime TreatmentDate { get; set; }

    /// <summary>Display name of the treating dentist or dental provider.</summary>
    [Id(7)] public string ProviderName { get; set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    [Id(8)] public DentalTreatmentStatus Status { get; set; }

    /// <summary>Charge amount for the procedure (nullable if not yet determined).</summary>
    [Id(9)] public decimal? ChargeAmount { get; set; }
}

// ─── Dental Patient — VistA File #228 DENTAL PATIENT ─────────────────────────

/// <summary>
/// Patient-level dental record storing eligibility, clinical status, and
/// aggregate dental health information.
/// Maps to VistA File #228 DENTAL PATIENT, managed by DENPAT.m routines.
/// Grain key: "DENTAL-PATIENT:{patientId}".
/// </summary>
[GenerateSerializer]
public class DentalPatientState
{
    /// <summary>Patient identifier (.01 field).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>VA dental care eligibility status for this patient.</summary>
    [Id(1)] public DentalEligibilityStatus EligibilityStatus { get; set; } = DentalEligibilityStatus.Unknown;

    /// <summary>Eligibility basis code (e.g., "SC", "POW", "CLASS6").</summary>
    [Id(2)] public string? EligibilityBasisCode { get; set; }

    /// <summary>Free-text description of the eligibility basis.</summary>
    [Id(3)] public string? EligibilityBasisDescription { get; set; }

    /// <summary>Date of the most recent comprehensive dental examination.</summary>
    [Id(4)] public DateTime? LastExamDate { get; set; }

    /// <summary>Date of the most recent dental x-rays taken.</summary>
    [Id(5)] public DateTime? LastXRayDate { get; set; }

    /// <summary>Date of the most recent dental cleaning / prophylaxis.</summary>
    [Id(6)] public DateTime? LastCleaningDate { get; set; }

    /// <summary>ID of the patient's primary VA dentist.</summary>
    [Id(7)] public string? PrimaryDentistId { get; set; }

    /// <summary>Display name of the patient's primary VA dentist.</summary>
    [Id(8)] public string? PrimaryDentistName { get; set; }

    /// <summary>Patient's current periodontal classification.</summary>
    [Id(9)] public DentalPeriodontalStatus PeriodontalStatus { get; set; } = DentalPeriodontalStatus.Healthy;

    /// <summary>Patient's prosthetic / denture status (free text, e.g., "Full upper denture").</summary>
    [Id(10)] public string? ProstheticStatus { get; set; }

    /// <summary>Number of remaining natural teeth (0 = edentulous).</summary>
    [Id(11)] public int? RemainingTeethCount { get; set; }

    /// <summary>Whether the patient is currently on fluoride supplementation.</summary>
    [Id(12)] public bool OnFluoride { get; set; }

    /// <summary>Patient-specific clinical notes or dental history summary.</summary>
    [Id(13)] public string? ClinicalNotes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(14)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ─── Dental Treatment — VistA File #228.1 DENTAL TREATMENT ───────────────────

/// <summary>
/// An individual dental treatment or procedure record for a patient.
/// Maps to VistA File #228.1 DENTAL TREATMENT, managed by DENTX.m / DENPROC.m.
/// Grain key: "DENTAL-TX:{guid}".
/// </summary>
[GenerateSerializer]
public class DentalTreatmentState
{
    /// <summary>Unique treatment identifier (grain key, typically a GUID).</summary>
    [Id(0)] public string TreatmentId { get; set; } = string.Empty;

    /// <summary>Patient identifier (.01 field).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Date on which the treatment was performed or is planned.</summary>
    [Id(2)] public DateTime TreatmentDate { get; set; }

    /// <summary>ADA CDT procedure code (e.g., "D2140" = amalgam, 1-surface permanent).</summary>
    [Id(3)] public string ProcedureCode { get; set; } = string.Empty;

    /// <summary>Human-readable procedure description matching the CDT code.</summary>
    [Id(4)] public string ProcedureDescription { get; set; } = string.Empty;

    /// <summary>High-level category of the CDT procedure code.</summary>
    [Id(5)] public DentalProcedureCategory ProcedureCategory { get; set; }

    /// <summary>
    /// Universal tooth number(s) affected by the procedure (e.g., [14, 15]).
    /// Empty list indicates a non-tooth-specific procedure (e.g., exam, full-mouth treatment).
    /// </summary>
    [Id(6)] public List<int> ToothNumbers { get; set; } = new();

    /// <summary>
    /// Tooth surfaces treated (e.g., "M", "D", "O", "F", "L").
    /// Used for restorative procedures; empty for non-surface-specific procedures.
    /// </summary>
    [Id(7)] public List<string> Surfaces { get; set; } = new();

    /// <summary>ID of the dental provider who performed or planned the treatment.</summary>
    [Id(8)] public string ProviderId { get; set; } = string.Empty;

    /// <summary>Display name of the dental provider.</summary>
    [Id(9)] public string ProviderName { get; set; } = string.Empty;

    /// <summary>Clinical location or clinic where treatment was rendered.</summary>
    [Id(10)] public string? LocationId { get; set; }

    /// <summary>Display name of the clinical location.</summary>
    [Id(11)] public string? LocationName { get; set; }

    /// <summary>Current lifecycle status of this treatment record.</summary>
    [Id(12)] public DentalTreatmentStatus Status { get; set; } = DentalTreatmentStatus.Planned;

    /// <summary>Primary ICD-10-CM diagnosis code justifying the dental procedure.</summary>
    [Id(13)] public string? DiagnosisCode { get; set; }

    /// <summary>Anesthesia type administered (e.g., "Local", "Nitrous Oxide", "General", "None").</summary>
    [Id(14)] public string? AnesthesiaType { get; set; }

    /// <summary>Charge amount for this procedure (nullable if not yet determined).</summary>
    [Id(15)] public decimal? ChargeAmount { get; set; }

    /// <summary>Clinical notes, findings, or complications related to this treatment.</summary>
    [Id(16)] public string? Notes { get; set; }

    /// <summary>
    /// Date the treatment was completed (set when Status transitions to Completed).
    /// </summary>
    [Id(17)] public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Reason for cancellation or referral (set when Status is Cancelled or Referred).
    /// </summary>
    [Id(18)] public string? StatusReason { get; set; }

    /// <summary>ID of the user who last updated this record.</summary>
    [Id(19)] public string? LastModifiedByUserId { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(20)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(21)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ─── Dental Treatment Index State ─────────────────────────────────────────────

/// <summary>
/// Per-patient index of all dental treatments.
/// Grain key: "DENTAL-TX-IDX:{patientId}".
/// </summary>
[GenerateSerializer]
public class DentalTreatmentIndexState
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Ordered list of treatment summary entries, newest first.</summary>
    [Id(1)] public List<DentalTreatmentIndexEntry> Treatments { get; set; } = new();
}
