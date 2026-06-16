// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Pain assessment tool/scale used.
/// </summary>
[GenerateSerializer]
public enum PainAssessmentTool
{
    /// <summary>Standard 0-10 Numeric Rating Scale.</summary>
    NumericRatingScale = 0,
    /// <summary>Defense and Veterans Pain Rating Scale (0-10 with functional impact).</summary>
    DVPRS = 1,
    /// <summary>Wong-Baker FACES Pain Rating Scale (pediatric/non-verbal).</summary>
    WongBakerFACES = 2,
    /// <summary>FLACC scale (Face, Legs, Activity, Cry, Consolability — infants/non-verbal).</summary>
    FLACC = 3,
    /// <summary>CPOT (Critical-Care Pain Observation Tool — intubated/sedated).</summary>
    CPOT = 4,
    /// <summary>Visual Analog Scale (0-100mm).</summary>
    VisualAnalogScale = 5,
}

/// <summary>
/// DVPRS supplemental question category (how pain affects function).
/// </summary>
[GenerateSerializer]
public record DvprsSupplemental
{
    /// <summary>Activity interference (0-10): How much does pain interfere with activity?</summary>
    [Id(0)] public int? ActivityInterference { get; init; }

    /// <summary>Sleep interference (0-10): How much does pain interfere with sleep?</summary>
    [Id(1)] public int? SleepInterference { get; init; }

    /// <summary>Mood interference (0-10): How much does pain affect mood?</summary>
    [Id(2)] public int? MoodInterference { get; init; }

    /// <summary>Stress interference (0-10): How much does pain contribute to stress?</summary>
    [Id(3)] public int? StressInterference { get; init; }
}

/// <summary>
/// FLACC scoring components (0-2 each, total 0-10).
/// </summary>
[GenerateSerializer]
public record FlaccScore
{
    /// <summary>Face (0=Relaxed, 1=Occasional grimace, 2=Frequent grimace).</summary>
    [Id(0)] public int Face { get; init; }
    /// <summary>Legs (0=Relaxed, 1=Restless, 2=Kicking/drawn up).</summary>
    [Id(1)] public int Legs { get; init; }
    /// <summary>Activity (0=Lying quietly, 1=Squirming, 2=Arched/rigid).</summary>
    [Id(2)] public int Activity { get; init; }
    /// <summary>Cry (0=None, 1=Moans/whimpers, 2=Crying steadily).</summary>
    [Id(3)] public int Cry { get; init; }
    /// <summary>Consolability (0=Content, 1=Reassured by touch, 2=Difficult to console).</summary>
    [Id(4)] public int Consolability { get; init; }
    /// <summary>Total FLACC score (0-10).</summary>
    [Id(5)] public int Total { get; init; }
}

/// <summary>
/// State for a structured pain assessment using a validated tool.
/// Grain key: "NURS-PAIN:{guid}"
/// </summary>
[GenerateSerializer]
public class PainAssessmentState
{
    /// <summary>Unique assessment ID.</summary>
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Date/time of assessment.</summary>
    [Id(2)] public DateTime AssessmentDateTime { get; set; }

    /// <summary>Nurse performing the assessment.</summary>
    [Id(3)] public string NurseId { get; set; } = string.Empty;

    /// <summary>Nurse display name.</summary>
    [Id(4)] public string NurseName { get; set; } = string.Empty;

    /// <summary>Assessment tool used.</summary>
    [Id(5)] public PainAssessmentTool Tool { get; set; }

    // ─── Pain Rating ─────────────────────────────────────────────────────

    /// <summary>Pain score (0-10 for NRS/DVPRS/FACES/FLACC; 0-100 for VAS).</summary>
    [Id(6)] public int PainScore { get; set; }

    /// <summary>Pain location (body site).</summary>
    [Id(7)] public string? PainLocation { get; set; }

    /// <summary>Pain quality/character (sharp, dull, burning, throbbing, aching, pressure, stabbing).</summary>
    [Id(8)] public string? PainCharacter { get; set; }

    /// <summary>Pain onset (acute, chronic, intermittent).</summary>
    [Id(9)] public string? PainOnset { get; set; }

    /// <summary>What makes pain worse (aggravating factors).</summary>
    [Id(10)] public string? AggravatingFactors { get; set; }

    /// <summary>What makes pain better (alleviating factors).</summary>
    [Id(11)] public string? AlleviatingFactors { get; set; }

    /// <summary>Does pain radiate? Where?</summary>
    [Id(12)] public string? Radiation { get; set; }

    /// <summary>Acceptable pain level (patient's goal).</summary>
    [Id(13)] public int? AcceptablePainLevel { get; set; }

    // ─── DVPRS Supplemental ──────────────────────────────────────────────

    /// <summary>DVPRS supplemental questions (if DVPRS tool used).</summary>
    [Id(14)] public DvprsSupplemental? DvprsSupplemental { get; set; }

    // ─── FLACC Components ────────────────────────────────────────────────

    /// <summary>FLACC scale components (if FLACC tool used for non-verbal/pediatric).</summary>
    [Id(15)] public FlaccScore? FlaccComponents { get; set; }

    // ─── Intervention & Reassessment ─────────────────────────────────────

    /// <summary>Pain intervention provided (medication, repositioning, ice, etc.).</summary>
    [Id(16)] public string? InterventionProvided { get; set; }

    /// <summary>Whether this is a reassessment after intervention.</summary>
    [Id(17)] public bool IsReassessment { get; set; }

    /// <summary>ID of the initial assessment this is a reassessment of.</summary>
    [Id(18)] public string? InitialAssessmentId { get; set; }

    /// <summary>Pain score after intervention (for reassessment).</summary>
    [Id(19)] public int? PostInterventionScore { get; set; }

    /// <summary>Time elapsed since intervention (minutes).</summary>
    [Id(20)] public int? MinutesSinceIntervention { get; set; }

    /// <summary>Whether the intervention was effective (score decreased).</summary>
    [Id(21)] public bool? InterventionEffective { get; set; }

    // ─── Status ──────────────────────────────────────────────────────────

    /// <summary>Document status.</summary>
    [Id(22)] public NursingAssessmentStatus Status { get; set; } = NursingAssessmentStatus.Draft;

    /// <summary>Notes.</summary>
    [Id(23)] public string? Notes { get; set; }

    /// <summary>When created.</summary>
    [Id(24)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight pain assessment index entry.</summary>
[GenerateSerializer]
public record PainAssessmentIndexEntry
{
    [Id(0)] public string AssessmentId { get; init; } = string.Empty;
    [Id(1)] public DateTime AssessmentDateTime { get; init; }
    [Id(2)] public string NurseName { get; init; } = string.Empty;
    [Id(3)] public PainAssessmentTool Tool { get; init; }
    [Id(4)] public int PainScore { get; init; }
    [Id(5)] public string? PainLocation { get; init; }
    [Id(6)] public bool IsReassessment { get; init; }
    [Id(7)] public int? PostInterventionScore { get; init; }
    [Id(8)] public NursingAssessmentStatus Status { get; init; }
}

/// <summary>
/// Per-patient pain assessment index.
/// Grain key: "NURS-PAIN-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class PainAssessmentIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<PainAssessmentIndexEntry> Entries { get; set; } = new();
}
