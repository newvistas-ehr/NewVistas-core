// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a Reportable Condition Trigger definition.
/// Used by the eCR system to detect reportable conditions from clinical data.
///
/// Maps to RCTC (Reportable Condition Trigger Codes) value sets maintained
/// by CSTE/APHL. Each trigger maps a clinical code (ICD-10, SNOMED, LOINC)
/// to a reportable condition with jurisdictional requirements.
///
/// Grain Key: "ECR-TRIGGER:{triggerId}"
/// </summary>
[GenerateSerializer]
public class EcrTriggerState
{
    /// <summary>Unique trigger identifier.</summary>
    [Id(0)]
    public string TriggerId { get; set; } = string.Empty;

    /// <summary>Condition name (e.g., "Measles", "Tuberculosis", "COVID-19").</summary>
    [Id(1)]
    public string ConditionName { get; set; } = string.Empty;

    /// <summary>Condition SNOMED code (e.g., "14189004" for measles).</summary>
    [Id(2)]
    public string? ConditionCode { get; set; }

    /// <summary>Code system for the condition code: "SNOMED", "ICD-10".</summary>
    [Id(3)]
    public string? ConditionCodeSystem { get; set; }

    /// <summary>
    /// Trigger codes — clinical codes that activate this reportable condition.
    /// ICD-10 diagnosis codes, LOINC lab codes, or SNOMED procedure codes.
    /// </summary>
    [Id(4)]
    public List<EcrTriggerCode> TriggerCodes { get; set; } = new();

    /// <summary>Jurisdictions where this condition is reportable (e.g., "US", "VA", "CA").</summary>
    [Id(5)]
    public List<string> Jurisdictions { get; set; } = new();

    /// <summary>Reporting timeframe requirement (e.g., "24 hours", "Immediately").</summary>
    [Id(6)]
    public string ReportingTimeframe { get; set; } = "24 hours";

    /// <summary>Whether this trigger is currently active.</summary>
    [Id(7)]
    public bool IsActive { get; set; } = true;

    /// <summary>Category: "communicable", "environmental", "occupational", "chronic".</summary>
    [Id(8)]
    public string Category { get; set; } = "communicable";

    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single trigger code within a reportable condition definition.
/// </summary>
[GenerateSerializer]
public class EcrTriggerCode
{
    /// <summary>Clinical code (e.g., "B05.*" for measles ICD-10, "21415-5" for LOINC).</summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Code system: "ICD-10", "LOINC", "SNOMED", "CPT".</summary>
    [Id(1)]
    public string CodeSystem { get; set; } = string.Empty;

    /// <summary>Human-readable description of the trigger code.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Trigger type: "diagnosis", "lab-order", "lab-result", "procedure".</summary>
    [Id(3)]
    public string TriggerType { get; set; } = "diagnosis";

    // ── Lab-Specific Fields (for value-based triggering) ────────────────────

    /// <summary>Specimen type (e.g., "Blood", "Urine", "Sputum", "Serum") — RPMS BLSMAP matching.</summary>
    [Id(4)]
    public string? SpecimenType { get; set; }

    /// <summary>
    /// Comparison operator for quantitative result matching.
    /// Values: "greater-than", "less-than", "greater-equal", "less-equal", "equals", "positive", "negative", "detected", "not-detected".
    /// When null, trigger fires on code presence alone (existing behavior).
    /// </summary>
    [Id(5)]
    public string? ValueOperator { get; set; }

    /// <summary>Threshold value for quantitative comparison (e.g., "1.0", "200", "400").</summary>
    [Id(6)]
    public string? ThresholdValue { get; set; }

    /// <summary>
    /// Result interpretation code that triggers the condition.
    /// Values: "POSITIVE", "REACTIVE", "DETECTED", "ABNORMAL", "CRITICAL".
    /// Used for qualitative lab results (e.g., HIV antibody positive).
    /// </summary>
    [Id(7)]
    public string? ResultInterpretation { get; set; }
}

/// <summary>
/// Index entry for listing reportable condition triggers.
/// </summary>
[GenerateSerializer]
public class EcrTriggerSummary
{
    [Id(0)]
    public string TriggerId { get; set; } = string.Empty;

    [Id(1)]
    public string ConditionName { get; set; } = string.Empty;

    [Id(2)]
    public string Category { get; set; } = string.Empty;

    [Id(3)]
    public bool IsActive { get; set; }

    [Id(4)]
    public string ReportingTimeframe { get; set; } = string.Empty;

    [Id(5)]
    public int TriggerCodeCount { get; set; }
}

/// <summary>
/// Index state for listing all reportable condition triggers.
/// Grain Key: "ECR-TRIGGER-INDEX"
/// </summary>
[GenerateSerializer]
public class EcrTriggerIndexState
{
    [Id(0)]
    public List<EcrTriggerSummary> Triggers { get; set; } = new();
}

/// <summary>
/// State for an electronic Initial Case Report (eICR).
/// §170.315(f)(5) — the core eCR document that reports a patient's
/// reportable condition to public health.
///
/// Based on HL7 CDA eICR IG (urn:hl7ii:2.16.840.1.113883.10.20.15.2)
/// and FHIR eCR IG (http://hl7.org/fhir/us/ecr).
///
/// Grain Key: "ECR-CASE:{caseId}"
/// </summary>
[GenerateSerializer]
public class EcrCaseState
{
    /// <summary>Unique case identifier.</summary>
    [Id(0)]
    public string CaseId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (for report display).</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Trigger that initiated this case report.</summary>
    [Id(3)]
    public string TriggerId { get; set; } = string.Empty;

    /// <summary>Reportable condition name.</summary>
    [Id(4)]
    public string ConditionName { get; set; } = string.Empty;

    /// <summary>The specific clinical code that triggered the report.</summary>
    [Id(5)]
    public string TriggeringCode { get; set; } = string.Empty;

    /// <summary>Code system for the triggering code.</summary>
    [Id(6)]
    public string TriggeringCodeSystem { get; set; } = string.Empty;

    /// <summary>Description of the triggering event.</summary>
    [Id(7)]
    public string TriggeringDescription { get; set; } = string.Empty;

    /// <summary>
    /// Status: "triggered", "generated", "submitted", "acknowledged",
    /// "reportable", "not-reportable", "may-be-reportable", "no-rule-met", "error".
    /// </summary>
    [Id(8)]
    public string Status { get; set; } = "triggered";

    /// <summary>Date the case was initially triggered.</summary>
    [Id(9)]
    public DateTime TriggeredDate { get; set; }

    /// <summary>Date the eICR document was generated.</summary>
    [Id(10)]
    public DateTime? GeneratedDate { get; set; }

    /// <summary>Date submitted to public health.</summary>
    [Id(11)]
    public DateTime? SubmittedDate { get; set; }

    /// <summary>Date a Reportability Response was received.</summary>
    [Id(12)]
    public DateTime? ResponseDate { get; set; }

    /// <summary>Generated eICR XML/FHIR document content.</summary>
    [Id(13)]
    public string? EicrDocument { get; set; }

    /// <summary>Reportability Response content from public health.</summary>
    [Id(14)]
    public string? ReportabilityResponse { get; set; }

    /// <summary>Determination from the Reportability Response.</summary>
    [Id(15)]
    public string? ReportabilityDetermination { get; set; }

    /// <summary>Responsible jurisdiction(s).</summary>
    [Id(16)]
    public List<string> Jurisdictions { get; set; } = new();

    /// <summary>Clinical evidence supporting the case (problems, labs, etc.).</summary>
    [Id(17)]
    public List<string> ClinicalEvidence { get; set; } = new();

    /// <summary>Provider who was responsible at trigger time.</summary>
    [Id(18)]
    public string? ResponsibleProvider { get; set; }

    /// <summary>Facility where the trigger occurred.</summary>
    [Id(19)]
    public string? FacilityName { get; set; }

    /// <summary>Error message if generation/submission failed.</summary>
    [Id(20)]
    public string? ErrorMessage { get; set; }

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Index entry for listing eCR cases.
/// </summary>
[GenerateSerializer]
public class EcrCaseSummary
{
    [Id(0)]
    public string CaseId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string ConditionName { get; set; } = string.Empty;

    [Id(4)]
    public string Status { get; set; } = string.Empty;

    [Id(5)]
    public DateTime TriggeredDate { get; set; }

    [Id(6)]
    public string? ReportabilityDetermination { get; set; }
}

/// <summary>
/// Index state for listing all eCR cases.
/// Grain Key: "ECR-CASE-INDEX"
/// </summary>
[GenerateSerializer]
public class EcrCaseIndexState
{
    [Id(0)]
    public List<EcrCaseSummary> Cases { get; set; } = new();
}
