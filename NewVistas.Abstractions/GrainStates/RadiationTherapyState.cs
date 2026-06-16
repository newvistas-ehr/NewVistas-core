// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>Overall lifecycle status of a radiation therapy course.</summary>
[GenerateSerializer]
public enum RtCourseStatus
{
    Planned = 0,
    Simulated = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Discontinued = 5
}

/// <summary>Delivery status of a single radiation therapy fraction.</summary>
[GenerateSerializer]
public enum RtFractionStatus
{
    Scheduled = 0,
    Delivered = 1,
    Skipped = 2,
    Cancelled = 3
}

/// <summary>Radiation therapy delivery modality / technique.</summary>
[GenerateSerializer]
public enum RtModality
{
    Photon3D = 0,
    Electron = 1,
    Proton = 2,
    Neutron = 3,
    Brachytherapy = 4,
    SRS = 5,
    SBRT = 6,
    IMRT = 7,
    VMAT = 8,
    TomotherapyHelical = 9,
    Other = 10
}

/// <summary>Clinical intent of the radiation therapy course.</summary>
[GenerateSerializer]
public enum RtIntent
{
    Curative = 0,
    Palliative = 1,
    Prophylactic = 2,
    Adjuvant = 3,
    Neoadjuvant = 4,
    Unknown = 5
}

/// <summary>Laterality of the treatment site.</summary>
[GenerateSerializer]
public enum RtLaterality
{
    NotApplicable = 0,
    Right = 1,
    Left = 2,
    Bilateral = 3,
    Midline = 4,
    Unknown = 5
}

/// <summary>Brachytherapy dose-rate type.</summary>
[GenerateSerializer]
public enum BrachytherapyDoseRate
{
    NotApplicable = 0,
    LDR = 1,
    HDR = 2,
    PDR = 3,
    Permanent = 4
}

// ── State classes ─────────────────────────────────────────────────────────────

/// <summary>
/// State for a radiation therapy treatment course.
/// Maps to VistA File #135 (RADIATION THERAPY).
/// MUMPS routines: RORTS.m, RORTX.m, RORTP.m
/// Grain key pattern: "RT-COURSE:{guid}"
/// </summary>
[GenerateSerializer]
public class RtCourseState
{
    /// <summary>Unique course identifier (grain key). (.01)</summary>
    [Id(0)] public string CourseId { get; set; } = string.Empty;

    /// <summary>Owning patient identifier. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Human-readable course name (e.g., "Prostate IMRT 2025"). (.03)</summary>
    [Id(2)] public string CourseName { get; set; } = string.Empty;

    /// <summary>Current lifecycle status of this course. (.04)</summary>
    [Id(3)] public RtCourseStatus Status { get; set; } = RtCourseStatus.Planned;

    // ── Diagnosis ──────────────────────────────────────────────────────────

    /// <summary>Primary diagnosis ICD-10 code for which RT is prescribed. (.10)</summary>
    [Id(4)] public string DiagnosisCode { get; set; } = string.Empty;

    /// <summary>Diagnosis description. (.11)</summary>
    [Id(5)] public string DiagnosisText { get; set; } = string.Empty;

    // ── Treatment site ──────────────────────────────────────────────────────

    /// <summary>Anatomic site being irradiated (e.g., "Prostate and seminal vesicles"). (.20)</summary>
    [Id(6)] public string TreatmentSite { get; set; } = string.Empty;

    /// <summary>Laterality of treatment site. (.21)</summary>
    [Id(7)] public RtLaterality Laterality { get; set; } = RtLaterality.NotApplicable;

    // ── Prescription ────────────────────────────────────────────────────────

    /// <summary>Clinical intent of this course. (.30)</summary>
    [Id(8)] public RtIntent Intent { get; set; } = RtIntent.Curative;

    /// <summary>Radiation delivery modality / technique. (.31)</summary>
    [Id(9)] public RtModality Modality { get; set; } = RtModality.Photon3D;

    /// <summary>Total prescribed dose in cGy. (.32)</summary>
    [Id(10)] public int PrescribedDoseCgy { get; set; }

    /// <summary>Number of fractions planned. (.33)</summary>
    [Id(11)] public int FractionsPlanned { get; set; }

    /// <summary>Prescribed dose per fraction in cGy. (.34)</summary>
    [Id(12)] public int DosePerFractionCgy { get; set; }

    /// <summary>Photon/electron beam energy (e.g., "6 MV", "18 MV", "9 MeV"). (.35)</summary>
    [Id(13)] public string? BeamEnergy { get; set; }

    // ── Brachytherapy-specific ───────────────────────────────────────────────

    /// <summary>Brachytherapy dose-rate type, if applicable. (.40)</summary>
    [Id(14)] public BrachytherapyDoseRate BrachyDoseRate { get; set; } = BrachytherapyDoseRate.NotApplicable;

    /// <summary>Brachytherapy isotope (e.g., "Ir-192", "I-125", "Pd-103"). (.41)</summary>
    [Id(15)] public string? BrachyIsotope { get; set; }

    // ── Boost ───────────────────────────────────────────────────────────────

    /// <summary>Whether a boost phase is part of this course. (.50)</summary>
    [Id(16)] public bool BoostFlag { get; set; }

    /// <summary>Boost target site/volume. (.51)</summary>
    [Id(17)] public string? BoostSite { get; set; }

    /// <summary>Boost prescribed dose in cGy. (.52)</summary>
    [Id(18)] public int? BoostDoseCgy { get; set; }

    /// <summary>Boost fractions planned. (.53)</summary>
    [Id(19)] public int? BoostFractionsPlanned { get; set; }

    // ── Team ────────────────────────────────────────────────────────────────

    /// <summary>Radiation oncologist identifier. (.60)</summary>
    [Id(20)] public string? OncologistId { get; set; }

    /// <summary>Radiation oncologist name. (.61)</summary>
    [Id(21)] public string? OncologistName { get; set; }

    /// <summary>Medical physicist identifier. (.62)</summary>
    [Id(22)] public string? PhysicistId { get; set; }

    /// <summary>Medical physicist name. (.63)</summary>
    [Id(23)] public string? PhysicistName { get; set; }

    /// <summary>Dosimetrist identifier. (.64)</summary>
    [Id(24)] public string? DosimetristId { get; set; }

    /// <summary>Dosimetrist name. (.65)</summary>
    [Id(25)] public string? DosimetristName { get; set; }

    // ── Machine ─────────────────────────────────────────────────────────────

    /// <summary>Primary treatment machine identifier. (.70)</summary>
    [Id(26)] public string? TreatmentMachineId { get; set; }

    /// <summary>Primary treatment machine name. (.71)</summary>
    [Id(27)] public string? TreatmentMachineName { get; set; }

    // ── Timeline ────────────────────────────────────────────────────────────

    /// <summary>Date of CT simulation. (.80)</summary>
    [Id(28)] public DateTime? SimulationDate { get; set; }

    /// <summary>Date the first treatment fraction was delivered. (.81)</summary>
    [Id(29)] public DateTime? TreatmentStartDate { get; set; }

    /// <summary>Date the course was completed. (.82)</summary>
    [Id(30)] public DateTime? TreatmentCompletionDate { get; set; }

    /// <summary>Date the course was discontinued, if applicable. (.83)</summary>
    [Id(31)] public DateTime? DiscontinuationDate { get; set; }

    /// <summary>Reason for discontinuation. (.84)</summary>
    [Id(32)] public string? DiscontinuationReason { get; set; }

    // ── Cumulative dose tracking ─────────────────────────────────────────────

    /// <summary>Total dose delivered to date in cGy (updated after each fraction). (.90)</summary>
    [Id(33)] public int TotalDeliveredDoseCgy { get; set; }

    /// <summary>Number of fractions actually delivered. (.91)</summary>
    [Id(34)] public int FractionsCompleted { get; set; }

    // ── Notes ────────────────────────────────────────────────────────────────

    /// <summary>Treatment planning / simulation notes. (.95)</summary>
    [Id(35)] public string? PlanningNotes { get; set; }

    /// <summary>General course notes. (.96)</summary>
    [Id(36)] public string? Notes { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────────

    /// <summary>Date this course record was created. (.99)</summary>
    [Id(37)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this course record was last modified. (.100)</summary>
    [Id(38)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient RT course index.</summary>
[GenerateSerializer]
public class RtCourseIndexEntry
{
    [Id(0)] public string CourseId { get; set; } = string.Empty;
    [Id(1)] public string CourseName { get; set; } = string.Empty;
    [Id(2)] public RtCourseStatus Status { get; set; }
    [Id(3)] public RtIntent Intent { get; set; }
    [Id(4)] public RtModality Modality { get; set; }
    [Id(5)] public string TreatmentSite { get; set; } = string.Empty;
    [Id(6)] public string DiagnosisCode { get; set; } = string.Empty;
    [Id(7)] public int PrescribedDoseCgy { get; set; }
    [Id(8)] public int FractionsPlanned { get; set; }
    [Id(9)] public int TotalDeliveredDoseCgy { get; set; }
    [Id(10)] public int FractionsCompleted { get; set; }
    [Id(11)] public DateTime? TreatmentStartDate { get; set; }
    [Id(12)] public DateTime? TreatmentCompletionDate { get; set; }
    [Id(13)] public string? OncologistName { get; set; }
}

// ── Fraction / Treatment state ─────────────────────────────────────────────────

/// <summary>
/// State for a single radiation therapy treatment fraction.
/// Maps to VistA File #135 treatment sub-records.
/// Grain key pattern: "RT-TX:{guid}"
/// </summary>
[GenerateSerializer]
public class RtTreatmentState
{
    /// <summary>Unique treatment/fraction identifier (grain key). (.01)</summary>
    [Id(0)] public string TreatmentId { get; set; } = string.Empty;

    /// <summary>Parent course identifier. (.02)</summary>
    [Id(1)] public string CourseId { get; set; } = string.Empty;

    /// <summary>Owning patient identifier. (.03)</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Sequential fraction number within the course. (.04)</summary>
    [Id(3)] public int FractionNumber { get; set; }

    /// <summary>Date and time this fraction was delivered. (.05)</summary>
    [Id(4)] public DateTime TreatmentDate { get; set; }

    /// <summary>Delivery status of this fraction. (.06)</summary>
    [Id(5)] public RtFractionStatus Status { get; set; } = RtFractionStatus.Scheduled;

    /// <summary>Dose delivered in cGy. (.07)</summary>
    [Id(6)] public int DoseDeliveredCgy { get; set; }

    /// <summary>Actual treatment delivery duration in minutes. (.08)</summary>
    [Id(7)] public int? TreatmentDurationMin { get; set; }

    /// <summary>Treatment machine identifier. (.09)</summary>
    [Id(8)] public string? MachineId { get; set; }

    /// <summary>Treatment machine name. (.10)</summary>
    [Id(9)] public string? MachineName { get; set; }

    /// <summary>Radiation therapist/technician identifier. (.11)</summary>
    [Id(10)] public string? TechnicianId { get; set; }

    /// <summary>Radiation therapist/technician name. (.12)</summary>
    [Id(11)] public string? TechnicianName { get; set; }

    /// <summary>Whether setup was verified with imaging (IGRT). (.13)</summary>
    [Id(12)] public bool SetupVerified { get; set; }

    /// <summary>Setup verification method used (e.g., "kV CBCT", "Portal imaging", "Surface guidance"). (.14)</summary>
    [Id(13)] public string? SetupMethod { get; set; }

    /// <summary>Maximum setup deviation from plan in mm (IGRT correction). (.15)</summary>
    [Id(14)] public decimal? SetupDeviationMm { get; set; }

    /// <summary>Whether treatment was interrupted. (.16)</summary>
    [Id(15)] public bool Interrupted { get; set; }

    /// <summary>Reason for interruption if applicable. (.17)</summary>
    [Id(16)] public string? InterruptionReason { get; set; }

    /// <summary>Reason this fraction was skipped or cancelled, if applicable. (.18)</summary>
    [Id(17)] public string? SkipReason { get; set; }

    /// <summary>Treatment-specific notes. (.19)</summary>
    [Id(18)] public string? Notes { get; set; }

    /// <summary>Date this record was created. (.20)</summary>
    [Id(19)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this record was last modified. (.21)</summary>
    [Id(20)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-course RT treatment index.</summary>
[GenerateSerializer]
public class RtTreatmentIndexEntry
{
    [Id(0)] public string TreatmentId { get; set; } = string.Empty;
    [Id(1)] public int FractionNumber { get; set; }
    [Id(2)] public DateTime TreatmentDate { get; set; }
    [Id(3)] public RtFractionStatus Status { get; set; }
    [Id(4)] public int DoseDeliveredCgy { get; set; }
    [Id(5)] public string? MachineName { get; set; }
    [Id(6)] public string? TechnicianName { get; set; }
    [Id(7)] public bool SetupVerified { get; set; }
    [Id(8)] public string? Notes { get; set; }
}
