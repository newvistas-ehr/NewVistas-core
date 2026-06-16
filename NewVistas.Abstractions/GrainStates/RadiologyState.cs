// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Radiology grain based on VistA RAD/NUC MED ORDERS file (#75.1) and RADIOLOGY REPORTS file
/// </summary>
[GenerateSerializer]
public class RadiologyState
{
    [Id(0)]
    public string RadiologyId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Procedure (.01) � pointer to RAD/NUC MED PROCEDURES file (#71)
    /// </summary>
    [Id(2)]
    public string ProcedureName { get; set; } = string.Empty;

    [Id(3)]
    public string? ProcedureId { get; set; }

    [Id(4)]
    public string? CptCode { get; set; }

    /// <summary>
    /// Imaging Type � GENERAL RADIOLOGY, CT SCAN, MRI, ULTRASOUND, NUCLEAR MEDICINE, etc.
    /// </summary>
    [Id(5)]
    public string? ImagingType { get; set; }

    /// <summary>
    /// Status � PENDING, EXAMINED, COMPLETE, CANCELLED
    /// </summary>
    [Id(6)]
    public string Status { get; set; } = "PENDING";

    [Id(7)]
    public DateTime? OrderDateTime { get; set; }

    [Id(8)]
    public DateTime? ExamDateTime { get; set; }

    [Id(9)]
    public DateTime? ReportDateTime { get; set; }

    [Id(10)]
    public string? RequestingProviderId { get; set; }

    [Id(11)]
    public string? RequestingProviderName { get; set; }

    [Id(12)]
    public string? InterpretingPhysicianId { get; set; }

    [Id(13)]
    public string? InterpretingPhysicianName { get; set; }

    [Id(14)]
    public string? Urgency { get; set; }

    [Id(15)]
    public string? ClinicalHistory { get; set; }

    [Id(16)]
    public string? ReasonForStudy { get; set; }

    /// <summary>
    /// Report Text � the radiology report narrative
    /// </summary>
    [Id(17)]
    public string? ReportText { get; set; }

    /// <summary>
    /// Impression � the radiologist's impression/conclusion
    /// </summary>
    [Id(18)]
    public string? Impression { get; set; }

    [Id(19)]
    public string? OrderId { get; set; }

    [Id(20)]
    public string? LocationId { get; set; }

    [Id(21)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Diagnostic Code � critical or abnormal findings
    /// </summary>
    [Id(22)]
    public string? DiagnosticCode { get; set; }

    [Id(23)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(24)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Contrast agent used — e.g., "Omnipaque 300", "Gadolinium"
    /// </summary>
    [Id(25)]
    public string? ContrastAgent { get; set; }

    /// <summary>
    /// Contrast administration route — IV, ORAL, RECTAL, INTRATHECAL
    /// </summary>
    [Id(26)]
    public string? ContrastRoute { get; set; }

    /// <summary>
    /// Volume of contrast administered in milliliters
    /// </summary>
    [Id(27)]
    public double? ContrastVolumeMl { get; set; }

    /// <summary>
    /// Whether a contrast reaction occurred during the study
    /// </summary>
    [Id(28)]
    public bool? ContrastReactionOccurred { get; set; }

    /// <summary>
    /// Description of contrast reaction if one occurred
    /// </summary>
    [Id(29)]
    public string? ContrastReactionDetails { get; set; }

    /// <summary>
    /// Effective radiation dose in millisieverts (mSv)
    /// </summary>
    [Id(30)]
    public double? RadiationDoseMSv { get; set; }

    /// <summary>
    /// CT dose index volume in milligray (mGy)
    /// </summary>
    [Id(31)]
    public double? CtdiVol { get; set; }

    /// <summary>
    /// Dose-length product in mGy·cm
    /// </summary>
    [Id(32)]
    public double? DoseLengthProduct { get; set; }

    /// <summary>
    /// Report status — DRAFT, PRELIMINARY, FINAL, AMENDED, ADDENDED
    /// </summary>
    [Id(33)]
    public string? ReportStatus { get; set; }

    /// <summary>
    /// Radiologist who signed the report
    /// </summary>
    [Id(34)]
    public string? SignedById { get; set; }

    /// <summary>
    /// Name of the radiologist who signed the report
    /// </summary>
    [Id(35)]
    public string? SignedByName { get; set; }

    /// <summary>
    /// Date and time the report was signed
    /// </summary>
    [Id(36)]
    public DateTime? SignedDateTime { get; set; }

    /// <summary>
    /// Whether this study has a critical or unexpected finding requiring notification
    /// </summary>
    [Id(37)]
    public bool IsCriticalResult { get; set; }

    /// <summary>
    /// Date and time the ordering provider was notified of the critical result
    /// </summary>
    [Id(38)]
    public DateTime? CriticalResultNotifiedDateTime { get; set; }

    /// <summary>
    /// Person who was notified of the critical result
    /// </summary>
    [Id(39)]
    public string? CriticalResultNotifiedTo { get; set; }

    /// <summary>
    /// Person who acknowledged the critical result notification
    /// </summary>
    [Id(40)]
    public string? CriticalResultAcknowledgedBy { get; set; }

    /// <summary>
    /// Radiology technician who performed the exam
    /// </summary>
    [Id(41)]
    public string? TechnicianId { get; set; }

    /// <summary>
    /// Name of the radiology technician
    /// </summary>
    [Id(42)]
    public string? TechnicianName { get; set; }

    /// <summary>
    /// Body part imaged — CHEST, ABDOMEN, HEAD, SPINE, EXTREMITY, etc.
    /// </summary>
    [Id(43)]
    public string? BodyPart { get; set; }

    /// <summary>
    /// Laterality — LEFT, RIGHT, BILATERAL, N/A
    /// </summary>
    [Id(44)]
    public string? Laterality { get; set; }

    /// <summary>
    /// Linked prior study IDs for comparison
    /// </summary>
    [Id(45)]
    public List<string> PriorStudyIds { get; set; } = new();

    /// <summary>
    /// Amendment text added to the report after signing
    /// </summary>
    [Id(46)]
    public string? AmendmentText { get; set; }

    /// <summary>
    /// Date and time the amendment was made
    /// </summary>
    [Id(47)]
    public DateTime? AmendmentDateTime { get; set; }
}
