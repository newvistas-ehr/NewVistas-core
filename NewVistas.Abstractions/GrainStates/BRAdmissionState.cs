// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Current status of a blind rehabilitation admission (VistA File #782.2 field .03).</summary>
[GenerateSerializer]
public enum BRAdmissionStatus
{
    /// <summary>Referral submitted, awaiting acceptance by the center.</summary>
    Pending = 0,
    /// <summary>Accepted by the center; patient not yet admitted.</summary>
    Accepted = 1,
    /// <summary>Patient is currently admitted and receiving training.</summary>
    Active = 2,
    /// <summary>Patient has been discharged from the program.</summary>
    Discharged = 3,
    /// <summary>Admission was cancelled before it began.</summary>
    Cancelled = 4
}

/// <summary>Admission priority (VistA File #782.2 field .04).</summary>
[GenerateSerializer]
public enum BRAdmissionPriority
{
    Routine = 0,
    Urgent = 1,
    Emergency = 2
}

/// <summary>Discharge disposition for a blind rehabilitation admission (VistA File #782.2 field .09).</summary>
[GenerateSerializer]
public enum BRDischargeDisposition
{
    /// <summary>Successfully completed the planned program.</summary>
    CompletedProgram = 0,
    /// <summary>Partially completed; discharged due to medical reasons.</summary>
    MedicalDischarge = 1,
    /// <summary>Left against clinical advice.</summary>
    AgainstAdvice = 2,
    /// <summary>Transferred to another BR facility.</summary>
    Transferred = 3,
    /// <summary>Program discontinued at patient request.</summary>
    PatientRequest = 4,
    /// <summary>Deceased during admission.</summary>
    Deceased = 5,
    /// <summary>Other disposition.</summary>
    Other = 6
}

// ─── Supporting Records ───────────────────────────────────────────────────────

/// <summary>A single progress note entry within a BR admission.</summary>
[GenerateSerializer]
public class BRProgressNote
{
    /// <summary>Content of the progress note.</summary>
    [Id(0)]
    public string Note { get; set; } = string.Empty;

    /// <summary>Identifier of the author.</summary>
    [Id(1)]
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Name of the author.</summary>
    [Id(2)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Date and time the note was recorded.</summary>
    [Id(3)]
    public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Lightweight index entry for a BR admission.</summary>
[GenerateSerializer]
public class BRAdmissionIndexEntry
{
    /// <summary>Unique admission identifier.</summary>
    [Id(0)]
    public string AdmitId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Name of the BR center.</summary>
    [Id(2)]
    public string CenterName { get; set; } = string.Empty;

    /// <summary>Date of admission.</summary>
    [Id(3)]
    public DateTime AdmitDate { get; set; }

    /// <summary>Current admission status.</summary>
    [Id(4)]
    public BRAdmissionStatus Status { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Blind Rehabilitation Admission State — an inpatient stay at a BR center.
/// Maps to VistA BLIND REHABILITATION ADMISSION file (#782.2).
/// </summary>
[GenerateSerializer]
public class BRAdmissionState
{
    /// <summary>Unique identifier for this admission (.01).</summary>
    [Id(0)]
    public string AdmitId { get; set; } = string.Empty;

    /// <summary>Patient identifier (.02).</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Identifier of the BR training center (.03).</summary>
    [Id(2)]
    public string CenterId { get; set; } = string.Empty;

    /// <summary>Name of the BR training center (.04).</summary>
    [Id(3)]
    public string CenterName { get; set; } = string.Empty;

    /// <summary>Date of admission (.05).</summary>
    [Id(4)]
    public DateTime AdmitDate { get; set; }

    /// <summary>Planned discharge date (.06).</summary>
    [Id(5)]
    public DateTime? PlannedDischargeDate { get; set; }

    /// <summary>Actual discharge date (.07).</summary>
    [Id(6)]
    public DateTime? ActualDischargeDate { get; set; }

    /// <summary>Current admission status (.08).</summary>
    [Id(7)]
    public BRAdmissionStatus Status { get; set; } = BRAdmissionStatus.Pending;

    /// <summary>Admission priority (.09).</summary>
    [Id(8)]
    public BRAdmissionPriority Priority { get; set; } = BRAdmissionPriority.Routine;

    /// <summary>Training program areas included in this admission (.10).</summary>
    [Id(9)]
    public List<BRTrainingArea> ProgramAreas { get; set; } = new();

    /// <summary>Training areas completed at discharge (.11).</summary>
    [Id(10)]
    public List<BRTrainingArea> AreasCompleted { get; set; } = new();

    /// <summary>Identifier of the referring provider (.12).</summary>
    [Id(11)]
    public string ReferringProviderId { get; set; } = string.Empty;

    /// <summary>Name of the referring provider (.13).</summary>
    [Id(12)]
    public string ReferringProviderName { get; set; } = string.Empty;

    /// <summary>Patient goals for this admission (.14).</summary>
    [Id(13)]
    public string? Goals { get; set; }

    /// <summary>Discharge summary narrative (.15).</summary>
    [Id(14)]
    public string? DischargeSummary { get; set; }

    /// <summary>Discharge disposition (.16).</summary>
    [Id(15)]
    public BRDischargeDisposition? DischargeDisposition { get; set; }

    /// <summary>Follow-up plan documented at discharge (.17).</summary>
    [Id(16)]
    public string? FollowUpPlan { get; set; }

    /// <summary>Cancellation reason (if applicable).</summary>
    [Id(17)]
    public string? CancellationReason { get; set; }

    /// <summary>General admission notes.</summary>
    [Id(18)]
    public string? Notes { get; set; }

    /// <summary>Progress notes recorded during the admission.</summary>
    [Id(19)]
    public List<BRProgressNote> ProgressNotes { get; set; } = new();

    /// <summary>Date the admission record was created.</summary>
    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the admission record was last modified.</summary>
    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ─── Admission Index State ────────────────────────────────────────────────────

/// <summary>Per-patient index of blind rehabilitation admissions.</summary>
[GenerateSerializer]
public class BRAdmissionIndexState
{
    /// <summary>All admission index entries for this patient.</summary>
    [Id(0)]
    public List<BRAdmissionIndexEntry> Admissions { get; set; } = new();
}
