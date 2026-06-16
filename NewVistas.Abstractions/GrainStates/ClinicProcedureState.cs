// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>Top-level category of a Clinical Procedure (VistA File #702).</summary>
[GenerateSerializer]
public enum ClinicProcedureCategory
{
    EEG = 0,
    EMG = 1,
    NerveConduction = 2,
    SleepStudy = 3,
    Audiometry = 4,
    Vestibular = 5,
    Electrophysiology = 6,
    TiltTable = 7,
    Other = 8
}

/// <summary>Lifecycle status of a Clinical Procedure.</summary>
[GenerateSerializer]
public enum ClinicProcedureStatus
{
    Ordered = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>Overall EEG interpretation classification.</summary>
[GenerateSerializer]
public enum EegAlertType
{
    Normal = 0,
    AbnormalFocal = 1,
    AbnormalGeneralized = 2,
    SeizureActivity = 3,
    SlowingGeneralized = 4,
    SlowingFocal = 5,
    Unknown = 6
}

/// <summary>Primary EMG/NCS finding classification.</summary>
[GenerateSerializer]
public enum EmgFindingType
{
    Normal = 0,
    Myopathy = 1,
    Neuropathy = 2,
    NmjDefect = 3,
    Denervation = 4,
    Reinnervation = 5,
    Unknown = 6
}

/// <summary>Type of sleep study performed.</summary>
[GenerateSerializer]
public enum SleepStudyType
{
    Diagnostic = 0,
    CpapTitration = 1,
    SplitNight = 2,
    Mslt = 3,
    Mwt = 4,
    Other = 5
}

/// <summary>Sleep apnea classification.</summary>
[GenerateSerializer]
public enum SleepApneaType
{
    None = 0,
    Obstructive = 1,
    Central = 2,
    Mixed = 3,
    Unknown = 4
}

/// <summary>Audiometric hearing loss classification.</summary>
[GenerateSerializer]
public enum HearingLossType
{
    None = 0,
    Conductive = 1,
    Sensorineural = 2,
    Mixed = 3,
    Unknown = 4
}

// ── State classes ─────────────────────────────────────────────────────────────

/// <summary>
/// State for a single Clinical Procedures record.
/// Maps to VistA File #702 (CLINICAL PROCEDURES).
/// MUMPS routines: CPRS.m, CPRSPAT.m
/// </summary>
[GenerateSerializer]
public class ClinicProcedureState
{
    /// <summary>Unique procedure identifier (grain key). (.01)</summary>
    [Id(0)] public string ProcedureId { get; set; } = string.Empty;

    /// <summary>Owning patient identifier. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Top-level category of this procedure. (.03)</summary>
    [Id(2)] public ClinicProcedureCategory Category { get; set; } = ClinicProcedureCategory.Other;

    /// <summary>Procedure code (CPT or local). (.04)</summary>
    [Id(3)] public string ProcedureCode { get; set; } = string.Empty;

    /// <summary>Descriptive name of the procedure. (.05)</summary>
    [Id(4)] public string ProcedureDescription { get; set; } = string.Empty;

    /// <summary>Current lifecycle status. (.06)</summary>
    [Id(5)] public ClinicProcedureStatus Status { get; set; } = ClinicProcedureStatus.Ordered;

    /// <summary>Date/time the procedure was ordered. (.07)</summary>
    [Id(6)] public DateTime OrderedDate { get; set; }

    /// <summary>Date/time the procedure is scheduled. (.08)</summary>
    [Id(7)] public DateTime? ScheduledDate { get; set; }

    /// <summary>Date/time the procedure was actually performed. (.09)</summary>
    [Id(8)] public DateTime? PerformedDate { get; set; }

    /// <summary>Ordering/performing provider identifier. (.10)</summary>
    [Id(9)] public string? ProviderId { get; set; }

    /// <summary>Ordering/performing provider name. (.11)</summary>
    [Id(10)] public string? ProviderName { get; set; }

    /// <summary>Location where procedure was performed. (.12)</summary>
    [Id(11)] public string? LocationId { get; set; }

    /// <summary>Location name. (.13)</summary>
    [Id(12)] public string? LocationName { get; set; }

    /// <summary>Clinical indication for the procedure. (.14)</summary>
    [Id(13)] public string? Indication { get; set; }

    /// <summary>Narrative findings of the procedure. (.20)</summary>
    [Id(14)] public string? Findings { get; set; }

    /// <summary>Clinical impression/conclusion. (.21)</summary>
    [Id(15)] public string? Impression { get; set; }

    /// <summary>Reason for cancellation, if applicable. (.22)</summary>
    [Id(16)] public string? CancellationReason { get; set; }

    /// <summary>Free-text notes. (.23)</summary>
    [Id(17)] public string? Notes { get; set; }

    // ── EEG-specific ────────────────────────────────────────────────────────

    /// <summary>Duration of EEG recording in minutes. (.30)</summary>
    [Id(18)] public int? EegDurationMinutes { get; set; }

    /// <summary>Background EEG activity description. (.31)</summary>
    [Id(19)] public string? EegBackground { get; set; }

    /// <summary>Overall EEG classification. (.32)</summary>
    [Id(20)] public EegAlertType? EegAlertType { get; set; }

    /// <summary>Whether epileptiform discharges/seizures were captured. (.33)</summary>
    [Id(21)] public bool? EegSeizureActivity { get; set; }

    /// <summary>Focal region of abnormality if applicable. (.34)</summary>
    [Id(22)] public string? EegFocalRegion { get; set; }

    /// <summary>Activation procedures used (e.g., hyperventilation, photic stimulation). (.35)</summary>
    [Id(23)] public List<string> EegActivations { get; set; } = new();

    // ── EMG-specific ────────────────────────────────────────────────────────

    /// <summary>Primary EMG finding classification. (.40)</summary>
    [Id(24)] public EmgFindingType? EmgFindingType { get; set; }

    /// <summary>List of muscles studied during EMG. (.41)</summary>
    [Id(25)] public List<string> EmgMusclesStudied { get; set; } = new();

    /// <summary>Spontaneous activity description (fibrillations, PSWs). (.42)</summary>
    [Id(26)] public string? EmgSpontaneousActivity { get; set; }

    /// <summary>Motor unit potential (MUP) description. (.43)</summary>
    [Id(27)] public string? EmgMupDescription { get; set; }

    // ── Nerve Conduction Study (NCS)-specific ────────────────────────────────

    /// <summary>Nerves studied during NCS. (.50)</summary>
    [Id(28)] public List<string> NcsNervesStudied { get; set; } = new();

    /// <summary>Mean motor conduction velocity in m/s. (.51)</summary>
    [Id(29)] public decimal? NcsMeanMotorVelocity { get; set; }

    /// <summary>Mean sensory conduction velocity in m/s. (.52)</summary>
    [Id(30)] public decimal? NcsMeanSensoryVelocity { get; set; }

    /// <summary>Whether F-waves were obtained. (.53)</summary>
    [Id(31)] public bool? NcsFWavesObtained { get; set; }

    /// <summary>NCS summary finding. (.54)</summary>
    [Id(32)] public EmgFindingType? NcsFindingType { get; set; }

    // ── Sleep Study-specific ─────────────────────────────────────────────────

    /// <summary>Type of sleep study performed. (.60)</summary>
    [Id(33)] public SleepStudyType? SleepStudyType { get; set; }

    /// <summary>Sleep apnea classification. (.61)</summary>
    [Id(34)] public SleepApneaType? SleepApneaType { get; set; }

    /// <summary>Apnea-Hypopnea Index (events per hour). (.62)</summary>
    [Id(35)] public decimal? ApneaHypopneaIndex { get; set; }

    /// <summary>Recommended or titrated CPAP pressure in cmH2O. (.63)</summary>
    [Id(36)] public decimal? CpapPressureCmH2O { get; set; }

    /// <summary>Sleep efficiency percentage. (.64)</summary>
    [Id(37)] public decimal? SleepEfficiencyPct { get; set; }

    /// <summary>Total sleep time in minutes. (.65)</summary>
    [Id(38)] public int? TotalSleepTimeMin { get; set; }

    /// <summary>Sleep onset latency in minutes. (.66)</summary>
    [Id(39)] public decimal? SleepLatencyMin { get; set; }

    /// <summary>REM sleep onset latency in minutes. (.67)</summary>
    [Id(40)] public decimal? RemLatencyMin { get; set; }

    // ── Audiometry-specific ──────────────────────────────────────────────────

    /// <summary>Hearing loss classification. (.70)</summary>
    [Id(41)] public HearingLossType? HearingLossType { get; set; }

    /// <summary>Right ear pure tone average (dB HL). (.71)</summary>
    [Id(42)] public decimal? RightEarPta { get; set; }

    /// <summary>Left ear pure tone average (dB HL). (.72)</summary>
    [Id(43)] public decimal? LeftEarPta { get; set; }

    /// <summary>Right ear speech discrimination score (%). (.73)</summary>
    [Id(44)] public decimal? SpeechDiscriminationRight { get; set; }

    /// <summary>Left ear speech discrimination score (%). (.74)</summary>
    [Id(45)] public decimal? SpeechDiscriminationLeft { get; set; }

    /// <summary>Tympanometry type right ear (A, B, C). (.75)</summary>
    [Id(46)] public string? TympanometryRight { get; set; }

    /// <summary>Tympanometry type left ear (A, B, C). (.76)</summary>
    [Id(47)] public string? TympanometryLeft { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────────

    /// <summary>Date the procedure record was created. (.90)</summary>
    [Id(48)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the procedure record was last modified. (.91)</summary>
    [Id(49)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient clinical procedure index.</summary>
[GenerateSerializer]
public class ClinicProcedureIndexEntry
{
    [Id(0)] public string ProcedureId { get; set; } = string.Empty;
    [Id(1)] public ClinicProcedureCategory Category { get; set; }
    [Id(2)] public string ProcedureCode { get; set; } = string.Empty;
    [Id(3)] public string ProcedureDescription { get; set; } = string.Empty;
    [Id(4)] public ClinicProcedureStatus Status { get; set; }
    [Id(5)] public DateTime OrderedDate { get; set; }
    [Id(6)] public DateTime? PerformedDate { get; set; }
    [Id(7)] public string? ProviderName { get; set; }
    [Id(8)] public string? LocationName { get; set; }
    [Id(9)] public string? Impression { get; set; }
}
