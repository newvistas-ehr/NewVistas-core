// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Emergency Severity Index (ESI) triage level — standard 5-level system.
/// ESI 1 = most urgent (resuscitation), ESI 5 = least urgent (non-urgent).
/// </summary>
[GenerateSerializer]
public enum TriageLevel
{
    /// <summary>Not yet triaged.</summary>
    NotTriaged = 0,
    /// <summary>ESI 1 — Resuscitation: immediate life-saving intervention needed.</summary>
    Esi1_Resuscitation = 1,
    /// <summary>ESI 2 — Emergent: high risk of deterioration, severe pain/distress.</summary>
    Esi2_Emergent = 2,
    /// <summary>ESI 3 — Urgent: requires two or more resources, stable vital signs.</summary>
    Esi3_Urgent = 3,
    /// <summary>ESI 4 — Less Urgent: requires one resource.</summary>
    Esi4_LessUrgent = 4,
    /// <summary>ESI 5 — Non-Urgent: no resources expected.</summary>
    Esi5_NonUrgent = 5,
}

/// <summary>
/// Triage disposition after assessment.
/// </summary>
[GenerateSerializer]
public enum TriageDisposition
{
    Pending = 0,
    /// <summary>Patient admitted to inpatient bed.</summary>
    Admit = 1,
    /// <summary>Patient held for observation.</summary>
    Observation = 2,
    /// <summary>Patient discharged from ED.</summary>
    Discharge = 3,
    /// <summary>Patient transferred to another facility.</summary>
    Transfer = 4,
    /// <summary>Patient left without being seen.</summary>
    LWBS = 5,
    /// <summary>Patient left against medical advice.</summary>
    AMA = 6,
}

/// <summary>
/// State for a nursing intake/triage assessment.
/// Combines ESI triage with vital signs, chief complaint, and initial nursing assessment.
/// Maps to VistA nursing intake and ED triage (NUR intake, File #212.1).
/// Grain key: "NURS-TRIAGE:{guid}"
/// </summary>
[GenerateSerializer]
public class NursingTriageState
{
    /// <summary>Unique triage assessment ID.</summary>
    [Id(0)] public string TriageId { get; set; } = string.Empty;

    /// <summary>Patient being assessed.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Date/time of triage assessment.</summary>
    [Id(2)] public DateTime TriageDateTime { get; set; }

    /// <summary>Triage nurse who performed the assessment.</summary>
    [Id(3)] public string NurseId { get; set; } = string.Empty;

    /// <summary>Nurse display name.</summary>
    [Id(4)] public string NurseName { get; set; } = string.Empty;

    /// <summary>Location (ED, clinic, ward).</summary>
    [Id(5)] public string? LocationId { get; set; }

    /// <summary>Location name.</summary>
    [Id(6)] public string? LocationName { get; set; }

    // ─── Chief Complaint & History ───────────────────────────────────────

    /// <summary>Chief complaint (reason for visit).</summary>
    [Id(7)] public string ChiefComplaint { get; set; } = string.Empty;

    /// <summary>History of present illness narrative.</summary>
    [Id(8)] public string? HistoryOfPresentIllness { get; set; }

    /// <summary>Onset of symptoms.</summary>
    [Id(9)] public string? OnsetDescription { get; set; }

    /// <summary>Known allergies at triage (brief).</summary>
    [Id(10)] public string? AllergyNote { get; set; }

    /// <summary>Current medications (brief).</summary>
    [Id(11)] public string? MedicationNote { get; set; }

    /// <summary>Relevant medical history.</summary>
    [Id(12)] public string? PastMedicalHistory { get; set; }

    // ─── Triage Vital Signs ──────────────────────────────────────────────

    /// <summary>Temperature (Fahrenheit).</summary>
    [Id(13)] public decimal? Temperature { get; set; }

    /// <summary>Heart rate (BPM).</summary>
    [Id(14)] public int? HeartRate { get; set; }

    /// <summary>Respiratory rate.</summary>
    [Id(15)] public int? RespiratoryRate { get; set; }

    /// <summary>Systolic blood pressure.</summary>
    [Id(16)] public int? SystolicBP { get; set; }

    /// <summary>Diastolic blood pressure.</summary>
    [Id(17)] public int? DiastolicBP { get; set; }

    /// <summary>Oxygen saturation (SpO2 %).</summary>
    [Id(18)] public decimal? SpO2 { get; set; }

    /// <summary>Pain score (0-10).</summary>
    [Id(19)] public int? PainScore { get; set; }

    /// <summary>Pain location.</summary>
    [Id(20)] public string? PainLocation { get; set; }

    /// <summary>Weight (lbs).</summary>
    [Id(21)] public decimal? Weight { get; set; }

    // ─── ESI Triage Assessment ───────────────────────────────────────────

    /// <summary>ESI triage level (1-5).</summary>
    [Id(22)] public TriageLevel TriageLevel { get; set; } = TriageLevel.NotTriaged;

    /// <summary>Expected number of resources needed (for ESI 3-5 differentiation).</summary>
    [Id(23)] public int? ExpectedResources { get; set; }

    /// <summary>Whether patient is in acute distress at triage.</summary>
    [Id(24)] public bool IsAcuteDistress { get; set; }

    /// <summary>Whether patient arrived by ambulance.</summary>
    [Id(25)] public bool ArrivedByAmbulance { get; set; }

    /// <summary>Mode of arrival (AMBULATORY, WHEELCHAIR, STRETCHER, AMBULANCE).</summary>
    [Id(26)] public string? ModeOfArrival { get; set; }

    // ─── Nursing Assessment ──────────────────────────────────────────────

    /// <summary>Level of consciousness (ALERT, VERBAL, PAIN, UNRESPONSIVE — AVPU).</summary>
    [Id(27)] public string? LevelOfConsciousness { get; set; }

    /// <summary>Skin assessment (WARM_DRY, COOL_CLAMMY, DIAPHORETIC, CYANOTIC).</summary>
    [Id(28)] public string? SkinAssessment { get; set; }

    /// <summary>Morse fall risk score.</summary>
    [Id(29)] public int? MorseFallRiskScore { get; set; }

    /// <summary>Braden pressure injury risk score.</summary>
    [Id(30)] public int? BradenScore { get; set; }

    /// <summary>Suicide/self-harm screening result.</summary>
    [Id(31)] public string? SuicideScreenResult { get; set; }

    /// <summary>Substance use screening note.</summary>
    [Id(32)] public string? SubstanceUseScreen { get; set; }

    // ─── Disposition & Status ────────────────────────────────────────────

    /// <summary>Disposition after triage.</summary>
    [Id(33)] public TriageDisposition Disposition { get; set; } = TriageDisposition.Pending;

    /// <summary>Whether the triage has been signed/completed.</summary>
    [Id(34)] public NursingAssessmentStatus Status { get; set; } = NursingAssessmentStatus.Draft;

    /// <summary>Triage narrative notes.</summary>
    [Id(35)] public string? Notes { get; set; }

    /// <summary>Date signed.</summary>
    [Id(36)] public DateTime? SignedDate { get; set; }

    /// <summary>When created.</summary>
    [Id(37)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(38)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight triage index entry.
/// </summary>
[GenerateSerializer]
public record NursingTriageIndexEntry
{
    [Id(0)] public string TriageId { get; init; } = string.Empty;
    [Id(1)] public DateTime TriageDateTime { get; init; }
    [Id(2)] public string NurseName { get; init; } = string.Empty;
    [Id(3)] public string ChiefComplaint { get; init; } = string.Empty;
    [Id(4)] public TriageLevel TriageLevel { get; init; }
    [Id(5)] public TriageDisposition Disposition { get; init; }
    [Id(6)] public NursingAssessmentStatus Status { get; init; }
    [Id(7)] public int? PainScore { get; init; }
}

/// <summary>
/// Per-patient triage index.
/// Grain key: "NURS-TRIAGE-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class NursingTriageIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<NursingTriageIndexEntry> Entries { get; set; } = new();
}
