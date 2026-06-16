// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>Top-level category of a Medicine procedure (VistA Medicine Package #691-699).</summary>
[GenerateSerializer]
public enum MedProcedureCategory
{
    Cardiology = 0,
    PulmonaryFunction = 1,
    GIEndoscopy = 2,
    Electrocardiogram = 3,
    Other = 4
}

/// <summary>Lifecycle status of a Medicine procedure.</summary>
[GenerateSerializer]
public enum MedProcedureStatus
{
    Ordered = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>Sub-type of cardiology study.</summary>
[GenerateSerializer]
public enum CardiologyStudyType
{
    ECG = 0,
    Echocardiogram = 1,
    StressTest = 2,
    HolterMonitor = 3,
    CardiacCatheterization = 4,
    NuclearCardiology = 5,
    CardiacMRI = 6,
    Other = 7
}

/// <summary>Cardiac rhythm interpreted from ECG/Holter.</summary>
[GenerateSerializer]
public enum CardiacRhythm
{
    Unknown = 0,
    Normal = 1,
    SinusTachycardia = 2,
    SinusBradycardia = 3,
    AtrialFibrillation = 4,
    AtrialFlutter = 5,
    SupraventricularTachycardia = 6,
    VentricularTachycardia = 7,
    VentricularFibrillation = 8,
    HeartBlock = 9,
    PrematureAtrialContractions = 10,
    PrematureVentricularContractions = 11,
    PacedRhythm = 12
}

/// <summary>Type of gastrointestinal/endoscopy procedure.</summary>
[GenerateSerializer]
public enum EndoscopyType
{
    Unknown = 0,
    EGD = 1,
    Colonoscopy = 2,
    Sigmoidoscopy = 3,
    ERCP = 4,
    Bronchoscopy = 5,
    Enteroscopy = 6,
    Capsule = 7,
    EUS = 8
}

/// <summary>Quality of bowel preparation for colonoscopy (Boston Bowel Prep Scale basis).</summary>
[GenerateSerializer]
public enum BowelPrepQuality
{
    NotApplicable = 0,
    Excellent = 1,
    Good = 2,
    Fair = 3,
    Poor = 4,
    Inadequate = 5
}

// ── State classes ─────────────────────────────────────────────────────────────

/// <summary>
/// State for a single Medicine procedure record.
/// Maps to VistA Medicine Package files #691-699.
/// MUMPS routines: MDAPI.m, MDEV.m, MDECHO.m, MDPFT.m, MDEC.m, MDGI.m
/// </summary>
[GenerateSerializer]
public class MedProcedureState
{
    /// <summary>Unique procedure identifier (grain key). (.01)</summary>
    [Id(0)] public string ProcedureId { get; set; } = string.Empty;

    /// <summary>Owning patient identifier. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Top-level category of this procedure. (.03)</summary>
    [Id(2)] public MedProcedureCategory Category { get; set; } = MedProcedureCategory.Other;

    /// <summary>Procedure code (CPT or local). (.04)</summary>
    [Id(3)] public string ProcedureCode { get; set; } = string.Empty;

    /// <summary>Descriptive name of the procedure. (.05)</summary>
    [Id(4)] public string ProcedureDescription { get; set; } = string.Empty;

    /// <summary>Current lifecycle status. (.06)</summary>
    [Id(5)] public MedProcedureStatus Status { get; set; } = MedProcedureStatus.Ordered;

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

    // ── Cardiology-specific ────────────────────────────────────────────────

    /// <summary>Sub-type of cardiology study. (.30)</summary>
    [Id(18)] public CardiologyStudyType? CardiologyStudyType { get; set; }

    /// <summary>Ejection fraction percentage (Echo). (.31)</summary>
    [Id(19)] public decimal? LvEjectionFraction { get; set; }

    /// <summary>LV diastolic function assessment (Echo). (.32)</summary>
    [Id(20)] public string? LvDiastolicFunction { get; set; }

    /// <summary>Valvular findings description (Echo). (.33)</summary>
    [Id(21)] public string? ValvularFindings { get; set; }

    /// <summary>Peak workload achieved in METs (stress test). (.34)</summary>
    [Id(22)] public decimal? PeakMets { get; set; }

    /// <summary>Percentage of target heart rate achieved (stress test). (.35)</summary>
    [Id(23)] public decimal? TargetHeartRatePct { get; set; }

    /// <summary>Inducible ischemia detected on stress test. (.36)</summary>
    [Id(24)] public bool? InducibleIschemia { get; set; }

    /// <summary>Duration of Holter recording in hours. (.37)</summary>
    [Id(25)] public int? HolterDurationHours { get; set; }

    /// <summary>Number of arrhythmia events captured on Holter. (.38)</summary>
    [Id(26)] public int? HolterArrhythmiaEvents { get; set; }

    /// <summary>Access site for cardiac catheterization. (.39)</summary>
    [Id(27)] public string? CathAccessSite { get; set; }

    /// <summary>Coronary artery findings (cath). (.40)</summary>
    [Id(28)] public string? CoronaryFindings { get; set; }

    /// <summary>Intervention performed during cath (e.g., PCI, stent). (.41)</summary>
    [Id(29)] public string? CathIntervention { get; set; }

    // ── ECG-specific ────────────────────────────────────────────────────────

    /// <summary>Ventricular rate in bpm. (.50)</summary>
    [Id(30)] public int? EcgRate { get; set; }

    /// <summary>Interpreted cardiac rhythm. (.51)</summary>
    [Id(31)] public CardiacRhythm? EcgRhythm { get; set; }

    /// <summary>PR interval in milliseconds. (.52)</summary>
    [Id(32)] public int? EcgPrIntervalMs { get; set; }

    /// <summary>QRS duration in milliseconds. (.53)</summary>
    [Id(33)] public int? EcgQrsDurationMs { get; set; }

    /// <summary>Corrected QT interval in milliseconds. (.54)</summary>
    [Id(34)] public int? EcgQtcMs { get; set; }

    /// <summary>QRS axis in degrees. (.55)</summary>
    [Id(35)] public int? EcgAxisDegrees { get; set; }

    /// <summary>Overall ECG interpretation text. (.56)</summary>
    [Id(36)] public string? EcgInterpretation { get; set; }

    /// <summary>Whether ECG is within normal limits. (.57)</summary>
    [Id(37)] public bool? EcgIsNormal { get; set; }

    // ── Pulmonary Function-specific ─────────────────────────────────────────

    /// <summary>FEV1 in liters (spirometry). (.60)</summary>
    [Id(38)] public decimal? PftFev1 { get; set; }

    /// <summary>FEV1 as percent of predicted. (.61)</summary>
    [Id(39)] public decimal? PftFev1PctPredicted { get; set; }

    /// <summary>FVC in liters (spirometry). (.62)</summary>
    [Id(40)] public decimal? PftFvc { get; set; }

    /// <summary>FVC as percent of predicted. (.63)</summary>
    [Id(41)] public decimal? PftFvcPctPredicted { get; set; }

    /// <summary>FEV1/FVC ratio. (.64)</summary>
    [Id(42)] public decimal? PftFev1FvcRatio { get; set; }

    /// <summary>Diffusing capacity (DLCO) in mL/min/mmHg. (.65)</summary>
    [Id(43)] public decimal? PftDlco { get; set; }

    /// <summary>DLCO as percent of predicted. (.66)</summary>
    [Id(44)] public decimal? PftDlcoPctPredicted { get; set; }

    /// <summary>Total lung capacity in liters. (.67)</summary>
    [Id(45)] public decimal? PftTlc { get; set; }

    /// <summary>Residual volume in liters. (.68)</summary>
    [Id(46)] public decimal? PftRv { get; set; }

    /// <summary>PFT pattern: obstructive defect detected. (.69)</summary>
    [Id(47)] public bool? PftObstructive { get; set; }

    /// <summary>PFT pattern: restrictive defect detected. (.70)</summary>
    [Id(48)] public bool? PftRestrictive { get; set; }

    /// <summary>Significant bronchodilator response. (.71)</summary>
    [Id(49)] public bool? PftBronchodilatorResponse { get; set; }

    /// <summary>Arterial blood gas — pH. (.72)</summary>
    [Id(50)] public decimal? AbgPh { get; set; }

    /// <summary>Arterial blood gas — PaO2 in mmHg. (.73)</summary>
    [Id(51)] public decimal? AbgPao2 { get; set; }

    /// <summary>Arterial blood gas — PaCO2 in mmHg. (.74)</summary>
    [Id(52)] public decimal? AbgPaco2 { get; set; }

    /// <summary>Arterial blood gas — HCO3 in mEq/L. (.75)</summary>
    [Id(53)] public decimal? AbgHco3 { get; set; }

    /// <summary>Arterial blood gas — SaO2 percent. (.76)</summary>
    [Id(54)] public decimal? AbgSao2 { get; set; }

    // ── GI/Endoscopy-specific ───────────────────────────────────────────────

    /// <summary>Type of endoscopic procedure. (.80)</summary>
    [Id(55)] public EndoscopyType? EndoscopyType { get; set; }

    /// <summary>Bowel preparation quality (colonoscopy). (.81)</summary>
    [Id(56)] public BowelPrepQuality? BowelPrepQuality { get; set; }

    /// <summary>Cecum reached on colonoscopy. (.82)</summary>
    [Id(57)] public bool? CecumReached { get; set; }

    /// <summary>Distance scope was advanced (cm). (.83)</summary>
    [Id(58)] public int? ScopeAdvancedCm { get; set; }

    /// <summary>Whether biopsy specimens were taken. (.84)</summary>
    [Id(59)] public bool? BiopsyTaken { get; set; }

    /// <summary>List of biopsy sites/descriptions. (.85)</summary>
    [Id(60)] public List<string> BiopsySites { get; set; } = new();

    /// <summary>Number of polyps found. (.86)</summary>
    [Id(61)] public int? PolypCount { get; set; }

    /// <summary>Descriptions of polyps found (size, location, morphology). (.87)</summary>
    [Id(62)] public List<string> PolypDescriptions { get; set; } = new();

    /// <summary>Endoscopic interventions performed (polypectomy, hemostasis, etc.). (.88)</summary>
    [Id(63)] public List<string> EndoscopicInterventions { get; set; } = new();

    // ── Audit ────────────────────────────────────────────────────────────────

    /// <summary>Date the procedure record was created. (.90)</summary>
    [Id(64)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the procedure record was last modified. (.91)</summary>
    [Id(65)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient procedure index.</summary>
[GenerateSerializer]
public class MedProcedureIndexEntry
{
    [Id(0)] public string ProcedureId { get; set; } = string.Empty;
    [Id(1)] public MedProcedureCategory Category { get; set; }
    [Id(2)] public string ProcedureCode { get; set; } = string.Empty;
    [Id(3)] public string ProcedureDescription { get; set; } = string.Empty;
    [Id(4)] public MedProcedureStatus Status { get; set; }
    [Id(5)] public DateTime OrderedDate { get; set; }
    [Id(6)] public DateTime? PerformedDate { get; set; }
    [Id(7)] public string? ProviderName { get; set; }
    [Id(8)] public string? LocationName { get; set; }
    [Id(9)] public string? Impression { get; set; }
}
