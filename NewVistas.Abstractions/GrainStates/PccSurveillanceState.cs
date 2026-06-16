// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// Visit type classification for encounter surveillance — RPMS APCSSILI.m.
/// </summary>
[GenerateSerializer]
public enum PccVisitType
{
    Ambulatory = 0,
    Hospitalization = 1,
    Outpatient = 2,
    Emergency = 3,
    Telehealth = 4,
}

/// <summary>
/// Encounter classification for surveillance — RPMS ILI/H1N1/SRD categories.
/// </summary>
[GenerateSerializer]
public enum PccEncounterClassification
{
    Unclassified = 0,
    InfluenzaLikeIllness = 1,
    SevereRespiratoryDisease = 2,
    ReportableCommunicable = 3,
    ReportableEnvironmental = 4,
    ReportableOccupational = 5,
}

/// <summary>
/// PCC surveillance match status.
/// </summary>
[GenerateSerializer]
public enum PccSurveillanceMatchStatus
{
    Detected = 0,
    Reviewed = 1,
    Reported = 2,
    Exported = 3,
    FalsePositive = 4,
}

// ── Nested Types ─────────────────────────────────────────────────────────────

/// <summary>
/// Comorbidity flags detected at encounter — RPMS APCSSIL2.m fields 33-36.
/// </summary>
[GenerateSerializer]
public class PccComorbidityFlags
{
    [Id(0)] public bool Asthma { get; set; }
    [Id(1)] public bool Diabetes { get; set; }
    [Id(2)] public bool Obesity { get; set; }
    [Id(3)] public bool Pregnancy { get; set; }
    [Id(4)] public bool Immunocompromised { get; set; }
    [Id(5)] public bool ChronicLungDisease { get; set; }
    [Id(6)] public bool CardiovascularDisease { get; set; }
    [Id(7)] public decimal? Bmi { get; set; }
}

/// <summary>
/// Vital signs captured at encounter — RPMS APCSSIL2.m field 11.
/// </summary>
[GenerateSerializer]
public class PccEncounterVitals
{
    [Id(0)] public decimal? TemperatureF { get; set; }
    [Id(1)] public int? OxygenSaturationPct { get; set; }
    [Id(2)] public int? HeartRate { get; set; }
    [Id(3)] public int? RespiratoryRate { get; set; }
    [Id(4)] public int? BloodPressureSystolic { get; set; }
    [Id(5)] public int? BloodPressureDiastolic { get; set; }
}

/// <summary>
/// Encounter-level surveillance criterion — extends ECR trigger logic for encounter context.
/// </summary>
[GenerateSerializer]
public class PccSurveillanceCriterion
{
    /// <summary>Code to match (ICD-10, LOINC, CPT, SNOMED).</summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Code system.</summary>
    [Id(1)]
    public string CodeSystem { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Match type: "diagnosis", "lab-result", "procedure", "medication", "chief-complaint".</summary>
    [Id(3)]
    public string MatchType { get; set; } = "diagnosis";

    /// <summary>Value operator for lab results (optional).</summary>
    [Id(4)]
    public string? ValueOperator { get; set; }

    /// <summary>Threshold value for lab results (optional).</summary>
    [Id(5)]
    public string? ThresholdValue { get; set; }
}

// ── Main State Classes ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for a PCC Surveillance Configuration grain (PCC-SURV-CONFIG:{configId}).
/// Defines encounter-level surveillance criteria for a reportable condition.
/// Maps to RPMS PCC Surveillance taxonomy setup (APCSB.m, APCSA.m).
/// </summary>
[GenerateSerializer]
public class PccSurveillanceConfigState
{
    [Id(0)] public string ConfigId { get; set; } = string.Empty;

    /// <summary>Condition name (e.g., "Influenza-Like Illness", "Chlamydia", "Tuberculosis").</summary>
    [Id(1)]
    public string ConditionName { get; set; } = string.Empty;

    /// <summary>Encounter classification for matched encounters.</summary>
    [Id(2)]
    public PccEncounterClassification Classification { get; set; }

    /// <summary>Criteria codes — any match triggers the surveillance alert.</summary>
    [Id(3)]
    public List<PccSurveillanceCriterion> Criteria { get; set; } = new();

    /// <summary>Required visit types (if empty, all types match).</summary>
    [Id(4)]
    public List<PccVisitType> RequiredVisitTypes { get; set; } = new();

    /// <summary>Whether to detect comorbidity flags for matched encounters.</summary>
    [Id(5)]
    public bool DetectComorbidities { get; set; } = true;

    /// <summary>Whether to capture vital signs for matched encounters.</summary>
    [Id(6)]
    public bool CaptureVitals { get; set; } = true;

    /// <summary>Rolling window in days for encounter scanning (default 90 per RPMS).</summary>
    [Id(7)]
    public int ScanWindowDays { get; set; } = 90;

    /// <summary>Jurisdictions where reportable.</summary>
    [Id(8)]
    public List<string> Jurisdictions { get; set; } = new();

    /// <summary>Reporting timeframe (e.g., "24 hours", "Immediately").</summary>
    [Id(9)]
    public string ReportingTimeframe { get; set; } = "24 hours";

    /// <summary>Whether this configuration is active.</summary>
    [Id(10)]
    public bool IsActive { get; set; } = true;

    [Id(11)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persistent state for a PCC Surveillance Match grain (PCC-SURV-MATCH:{matchId}).
/// Records a single encounter that matched surveillance criteria, with full context.
/// Maps to RPMS APCSSIL2.m 106-field encounter record.
/// </summary>
[GenerateSerializer]
public class PccSurveillanceMatchState
{
    [Id(0)] public string MatchId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string? PatientName { get; set; }

    /// <summary>Configuration that triggered this match.</summary>
    [Id(3)]
    public string ConfigId { get; set; } = string.Empty;

    /// <summary>Condition name from the config.</summary>
    [Id(4)]
    public string ConditionName { get; set; } = string.Empty;

    /// <summary>Match status lifecycle.</summary>
    [Id(5)]
    public PccSurveillanceMatchStatus Status { get; set; }

    /// <summary>Encounter classification.</summary>
    [Id(6)]
    public PccEncounterClassification Classification { get; set; }

    // ── Encounter Context ────────────────────────────────────────────────

    /// <summary>Visit/encounter date.</summary>
    [Id(7)]
    public DateTime EncounterDate { get; set; }

    /// <summary>Visit type.</summary>
    [Id(8)]
    public PccVisitType VisitType { get; set; }

    /// <summary>Chief complaint (structured text).</summary>
    [Id(9)]
    public string? ChiefComplaint { get; set; }

    /// <summary>Facility/clinic name.</summary>
    [Id(10)]
    public string? FacilityName { get; set; }

    /// <summary>Discharge date (for hospitalizations).</summary>
    [Id(11)]
    public DateTime? DischargeDate { get; set; }

    /// <summary>Provider name.</summary>
    [Id(12)]
    public string? ProviderName { get; set; }

    // ── Clinical Evidence ────────────────────────────────────────────────

    /// <summary>Matching diagnosis codes from the encounter.</summary>
    [Id(13)]
    public List<string> MatchingDiagnoses { get; set; } = new();

    /// <summary>Matching procedure codes from the encounter.</summary>
    [Id(14)]
    public List<string> MatchingProcedures { get; set; } = new();

    /// <summary>Matching lab results from the encounter.</summary>
    [Id(15)]
    public List<string> MatchingLabResults { get; set; } = new();

    /// <summary>Matching medications from the encounter.</summary>
    [Id(16)]
    public List<string> MatchingMedications { get; set; } = new();

    // ── Comorbidity Flags ────────────────────────────────────────────────

    /// <summary>Comorbidity flags detected at encounter.</summary>
    [Id(17)]
    public PccComorbidityFlags? Comorbidities { get; set; }

    // ── Vital Signs ──────────────────────────────────────────────────────

    /// <summary>Vital signs captured at encounter.</summary>
    [Id(18)]
    public PccEncounterVitals? Vitals { get; set; }

    // ── Export ────────────────────────────────────────────────────────────

    /// <summary>Date match was exported to public health.</summary>
    [Id(19)]
    public DateTime? ExportedDate { get; set; }

    /// <summary>Export file reference or transmission ID.</summary>
    [Id(20)]
    public string? ExportReference { get; set; }

    [Id(21)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(22)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index Entries ────────────────────────────────────────────────────────────

[GenerateSerializer]
public class PccSurveillanceConfigIndexEntry
{
    [Id(0)] public string ConfigId { get; set; } = string.Empty;
    [Id(1)] public string ConditionName { get; set; } = string.Empty;
    [Id(2)] public PccEncounterClassification Classification { get; set; }
    [Id(3)] public int CriteriaCount { get; set; }
    [Id(4)] public bool IsActive { get; set; }
}

[GenerateSerializer]
public class PccSurveillanceConfigIndexState
{
    [Id(0)] public List<PccSurveillanceConfigIndexEntry> Entries { get; set; } = new();
}

[GenerateSerializer]
public class PccSurveillanceMatchIndexEntry
{
    [Id(0)] public string MatchId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string ConditionName { get; set; } = string.Empty;
    [Id(3)] public PccSurveillanceMatchStatus Status { get; set; }
    [Id(4)] public PccEncounterClassification Classification { get; set; }
    [Id(5)] public DateTime EncounterDate { get; set; }
    [Id(6)] public PccVisitType VisitType { get; set; }
    [Id(7)] public DateTime CreatedDate { get; set; }
}

[GenerateSerializer]
public class PccSurveillanceMatchIndexState
{
    [Id(0)] public List<PccSurveillanceMatchIndexEntry> Entries { get; set; } = new();
}
