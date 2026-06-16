// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Current status of a blind rehabilitation outpatient visit (VistA File #782.3 field .03).</summary>
[GenerateSerializer]
public enum BRVisitStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4
}

/// <summary>Outcome of a completed blind rehabilitation outpatient visit.</summary>
[GenerateSerializer]
public enum BRVisitOutcome
{
    /// <summary>Patient demonstrated measurable progress toward goals.</summary>
    ProgressMade = 0,
    /// <summary>Patient maintained current skill level; no regression.</summary>
    Maintained = 1,
    /// <summary>No significant change observed.</summary>
    NoChange = 2,
    /// <summary>Regression noted; additional intervention needed.</summary>
    Regression = 3
}

// ─── Supporting Record ────────────────────────────────────────────────────────

/// <summary>Lightweight index entry for a BR outpatient visit.</summary>
[GenerateSerializer]
public class BROutpatientVisitIndexEntry
{
    /// <summary>Unique visit identifier.</summary>
    [Id(0)]
    public string VisitId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Date of the visit.</summary>
    [Id(2)]
    public DateTime VisitDate { get; set; }

    /// <summary>Training area covered during this visit.</summary>
    [Id(3)]
    public BRTrainingArea TrainingArea { get; set; }

    /// <summary>Name of the therapist.</summary>
    [Id(4)]
    public string TherapistName { get; set; } = string.Empty;

    /// <summary>Current visit status.</summary>
    [Id(5)]
    public BRVisitStatus Status { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Blind Rehabilitation Outpatient Visit State — a single outpatient BR training session.
/// Maps to VistA BLIND REHABILITATION OUTPATIENT VISIT file (#782.3).
/// </summary>
[GenerateSerializer]
public class BROutpatientVisitState
{
    /// <summary>Unique identifier for this visit (.01).</summary>
    [Id(0)]
    public string VisitId { get; set; } = string.Empty;

    /// <summary>Patient identifier (.02).</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Date of the outpatient visit (.03).</summary>
    [Id(2)]
    public DateTime VisitDate { get; set; }

    /// <summary>Training area covered during this visit (.04).</summary>
    [Id(3)]
    public BRTrainingArea TrainingArea { get; set; }

    /// <summary>Identifier of the rehabilitation therapist (.05).</summary>
    [Id(4)]
    public string TherapistId { get; set; } = string.Empty;

    /// <summary>Name of the rehabilitation therapist (.06).</summary>
    [Id(5)]
    public string TherapistName { get; set; } = string.Empty;

    /// <summary>Location where the session took place (.07).</summary>
    [Id(6)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Duration of the session in minutes (.08).</summary>
    [Id(7)]
    public int DurationMinutes { get; set; }

    /// <summary>Current visit status (.09).</summary>
    [Id(8)]
    public BRVisitStatus Status { get; set; } = BRVisitStatus.Scheduled;

    /// <summary>Specific skills addressed during this session (.10).</summary>
    [Id(9)]
    public List<string> SkillsAddressed { get; set; } = new();

    /// <summary>Therapist session notes (.11).</summary>
    [Id(10)]
    public string? SessionNotes { get; set; }

    /// <summary>Outcome summary recorded at completion (.12).</summary>
    [Id(11)]
    public string? OutcomeSummary { get; set; }

    /// <summary>Visit outcome classification (.13).</summary>
    [Id(12)]
    public BRVisitOutcome? Outcome { get; set; }

    /// <summary>Reason for cancellation (if applicable) (.14).</summary>
    [Id(13)]
    public string? CancellationReason { get; set; }

    /// <summary>Progress notes added to this visit.</summary>
    [Id(14)]
    public List<BRProgressNote> ProgressNotes { get; set; } = new();

    /// <summary>Date the visit record was created.</summary>
    [Id(15)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the visit record was last modified.</summary>
    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ─── Visit Index State ────────────────────────────────────────────────────────

/// <summary>Per-patient index of blind rehabilitation outpatient visits.</summary>
[GenerateSerializer]
public class BROutpatientVisitIndexState
{
    /// <summary>All outpatient visit index entries for this patient.</summary>
    [Id(0)]
    public List<BROutpatientVisitIndexEntry> Visits { get; set; } = new();
}
