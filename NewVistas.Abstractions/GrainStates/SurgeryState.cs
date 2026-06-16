// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Surgery grain based on VistA SURGERY file (#130)
/// </summary>
[GenerateSerializer]
public class SurgeryState
{
    [Id(0)]
    public string SurgeryId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Principal Operative Procedure (.01)
    /// </summary>
    [Id(2)]
    public string PrincipalProcedure { get; set; } = string.Empty;

    [Id(3)]
    public string? PrincipalProcedureCptCode { get; set; }

    /// <summary>
    /// Other Operative Procedures � multiple
    /// </summary>
    [Id(4)]
    public List<string> OtherProcedures { get; set; } = new();

    /// <summary>
    /// Date of Operation (.09)
    /// </summary>
    [Id(5)]
    public DateTime? DateOfOperation { get; set; }

    /// <summary>
    /// Time Operation Began (.22)
    /// </summary>
    [Id(6)]
    public DateTime? TimeOperationBegan { get; set; }

    /// <summary>
    /// Time Operation Ended (.23)
    /// </summary>
    [Id(7)]
    public DateTime? TimeOperationEnded { get; set; }

    /// <summary>
    /// Surgeon (.14) � pointer to NEW PERSON file (#200)
    /// </summary>
    [Id(8)]
    public string? SurgeonId { get; set; }

    [Id(9)]
    public string? SurgeonName { get; set; }

    [Id(10)]
    public string? FirstAssistantId { get; set; }

    [Id(11)]
    public string? FirstAssistantName { get; set; }

    /// <summary>
    /// Anesthesiologist (.31)
    /// </summary>
    [Id(12)]
    public string? AnesthesiologistId { get; set; }

    [Id(13)]
    public string? AnesthesiologistName { get; set; }

    /// <summary>
    /// Anesthesia Technique � GENERAL, SPINAL, LOCAL, MAC, etc.
    /// </summary>
    [Id(14)]
    public string? AnesthesiaTechnique { get; set; }

    /// <summary>
    /// Operative Report text
    /// </summary>
    [Id(15)]
    public string? OperativeReport { get; set; }

    /// <summary>
    /// Pre-Operative Diagnosis
    /// </summary>
    [Id(16)]
    public string? PreOpDiagnosis { get; set; }

    /// <summary>
    /// Post-Operative Diagnosis
    /// </summary>
    [Id(17)]
    public string? PostOpDiagnosis { get; set; }

    /// <summary>
    /// Surgical Specialty (.04) � pointer to SURGERY SPECIALTY file (#137.45)
    /// </summary>
    [Id(18)]
    public string? SurgicalSpecialty { get; set; }

    /// <summary>
    /// Wound Classification � CLEAN, CLEAN-CONTAMINATED, CONTAMINATED, DIRTY
    /// </summary>
    [Id(19)]
    public string? WoundClassification { get; set; }

    /// <summary>
    /// Status � SCHEDULED, IN PROGRESS, COMPLETED, CANCELLED
    /// </summary>
    [Id(20)]
    public string Status { get; set; } = "SCHEDULED";

    [Id(21)]
    public string? LocationId { get; set; }

    [Id(22)]
    public string? LocationName { get; set; }

    [Id(23)]
    public string? Comments { get; set; }

    [Id(24)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(25)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ASA Physical Status Classification (1-6), pre-operative assessment
    /// </summary>
    [Id(26)]
    public int? AsaClassification { get; set; }

    /// <summary>
    /// Free text pre-operative evaluation notes
    /// </summary>
    [Id(27)]
    public string? PreOpAssessmentNotes { get; set; }

    /// <summary>
    /// Date/time when pre-operative assessment was performed
    /// </summary>
    [Id(28)]
    public DateTime? PreOpAssessmentDate { get; set; }

    [Id(29)]
    public string? PreOpAssessmentProviderId { get; set; }

    [Id(30)]
    public string? PreOpAssessmentProviderName { get; set; }

    /// <summary>
    /// Post-operative complications list
    /// </summary>
    [Id(31)]
    public List<SurgicalComplication> Complications { get; set; } = new();

    /// <summary>
    /// Implants/devices placed during surgery
    /// </summary>
    [Id(32)]
    public List<SurgicalImplant> Implants { get; set; } = new();

    /// <summary>
    /// Multiple surgical assistants (replaces single FirstAssistant pattern)
    /// </summary>
    [Id(33)]
    public List<SurgicalAssistant> Assistants { get; set; } = new();

    /// <summary>
    /// Estimated Blood Loss in mL
    /// </summary>
    [Id(34)]
    public int? EstimatedBloodLoss { get; set; }

    /// <summary>
    /// Anesthesia start time (string for flexibility)
    /// </summary>
    [Id(35)]
    public string? AnesthesiaStartTime { get; set; }

    /// <summary>
    /// Anesthesia end time (string for flexibility)
    /// </summary>
    [Id(36)]
    public string? AnesthesiaEndTime { get; set; }

    /// <summary>
    /// Anesthesia agents/drugs used (e.g., Propofol, Sevoflurane)
    /// </summary>
    [Id(37)]
    public List<string> AnesthesiaAgents { get; set; } = new();

    /// <summary>
    /// Sponge count correct: 1=yes, 0=no, null=not documented
    /// </summary>
    [Id(38)]
    public int? SpongeCountCorrect { get; set; }

    /// <summary>
    /// Needle count correct: 1=yes, 0=no, null=not documented
    /// </summary>
    [Id(39)]
    public int? NeedleCountCorrect { get; set; }

    /// <summary>
    /// Instrument count correct: 1=yes, 0=no, null=not documented
    /// </summary>
    [Id(40)]
    public int? InstrumentCountCorrect { get; set; }

    /// <summary>
    /// Pathology specimens collected during surgery
    /// </summary>
    [Id(41)]
    public List<SurgicalSpecimen> Specimens { get; set; } = new();

    /// <summary>
    /// Disposition after surgery — PACU, ICU, WARD, HOME, etc.
    /// </summary>
    [Id(42)]
    public string? DispositionAfterSurgery { get; set; }

    /// <summary>
    /// Blood products given (e.g., "2 UNITS PRBC", "1 UNIT FFP")
    /// </summary>
    [Id(43)]
    public List<string> BloodProductsGiven { get; set; } = new();
}

/// <summary>
/// Post-operative complication record
/// </summary>
[GenerateSerializer]
public class SurgicalComplication
{
    [Id(0)]
    public string ComplicationCode { get; set; } = string.Empty;

    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity — MINOR, MAJOR, DEATH
    /// </summary>
    [Id(2)]
    public string? Severity { get; set; }

    [Id(3)]
    public DateTime OccurrenceDate { get; set; }

    [Id(4)]
    public string? TreatmentAction { get; set; }
}

/// <summary>
/// Surgical implant/device record
/// </summary>
[GenerateSerializer]
public class SurgicalImplant
{
    [Id(0)]
    public string DeviceName { get; set; } = string.Empty;

    [Id(1)]
    public string? Manufacturer { get; set; }

    [Id(2)]
    public string? SerialNumber { get; set; }

    [Id(3)]
    public string? LotNumber { get; set; }

    [Id(4)]
    public string? BodySite { get; set; }
}

/// <summary>
/// Surgical assistant record
/// </summary>
[GenerateSerializer]
public class SurgicalAssistant
{
    [Id(0)]
    public string AssistantId { get; set; } = string.Empty;

    [Id(1)]
    public string AssistantName { get; set; } = string.Empty;

    /// <summary>
    /// Role — FIRST ASSISTANT, SECOND ASSISTANT, RESIDENT, SCRUB NURSE
    /// </summary>
    [Id(2)]
    public string? Role { get; set; }
}

/// <summary>
/// Surgical pathology specimen record
/// </summary>
[GenerateSerializer]
public class SurgicalSpecimen
{
    [Id(0)]
    public string SpecimenType { get; set; } = string.Empty;

    [Id(1)]
    public string? BodySite { get; set; }

    [Id(2)]
    public string? PathologyAccessionNumber { get; set; }

    [Id(3)]
    public DateTime CollectionDateTime { get; set; }
}
