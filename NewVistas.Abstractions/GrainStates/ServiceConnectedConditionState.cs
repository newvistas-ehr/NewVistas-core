// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single service connected condition entry embedded in the patient grain.
/// Based on VistA SERVICE CONNECTED CONDITIONS file (#2.04, sub-file of PATIENT #2).
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class ScConditionEntry
{
    [Id(0)]
    public string ConditionId { get; set; } = string.Empty;

    /// <summary>
    /// Condition (.01) � the disability or condition
    /// </summary>
    [Id(2)]
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// ICD Diagnosis Code
    /// </summary>
    [Id(3)]
    public string? DiagnosisCode { get; set; }

    /// <summary>
    /// Disability Percentage (0�100)
    /// </summary>
    [Id(4)]
    public int? DisabilityPercentage { get; set; }

    /// <summary>
    /// Service Connected � YES or NO
    /// </summary>
    [Id(5)]
    public bool IsServiceConnected { get; set; } = true;

    /// <summary>
    /// Effective Date
    /// </summary>
    [Id(6)]
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Rating Decision Date
    /// </summary>
    [Id(7)]
    public DateTime? RatingDecisionDate { get; set; }

    /// <summary>
    /// Status � ACTIVE, INACTIVE
    /// </summary>
    [Id(8)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Combined Disability Percentage (the overall combined SC %)
    /// </summary>
    [Id(9)]
    public int? CombinedDisabilityPercentage { get; set; }

    /// <summary>
    /// Extremity Affected � if applicable
    /// </summary>
    [Id(10)]
    public string? ExtremityAffected { get; set; }

    /// <summary>
    /// Original Injury Date
    /// </summary>
    [Id(11)]
    public DateTime? OriginalInjuryDate { get; set; }

    [Id(12)]
    public string? Comments { get; set; }

    [Id(13)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(14)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Service Connected Percentage — 0-100%
    /// </summary>
    [Id(15)]
    public int? ServiceConnectedPercentage { get; set; }

    /// <summary>
    /// Rating Decision ID — reference to decision document
    /// </summary>
    [Id(16)]
    public string? RatingDecisionId { get; set; }

    /// <summary>
    /// Individual rated disabilities with separate percentages
    /// </summary>
    [Id(17)]
    public List<RatedDisability> RatedDisabilities { get; set; } = new();

    /// <summary>
    /// VA combined disability rating (computed via VA formula)
    /// </summary>
    [Id(18)]
    public int? CombinedRating { get; set; }

    /// <summary>
    /// Appeal Status — "NONE", "FILED", "IN_REVIEW", "DECIDED"
    /// </summary>
    [Id(19)]
    public string? AppealStatus { get; set; }

    /// <summary>
    /// Date appeal was filed
    /// </summary>
    [Id(20)]
    public DateTime? AppealFiledDate { get; set; }

    /// <summary>
    /// Last C&amp;P exam date
    /// </summary>
    [Id(21)]
    public DateTime? LastExamDate { get; set; }

    /// <summary>
    /// Scheduled re-evaluation date
    /// </summary>
    [Id(22)]
    public DateTime? NextExamDueDate { get; set; }

    /// <summary>
    /// Facility that performed exam
    /// </summary>
    [Id(23)]
    public string? ExaminingFacility { get; set; }

    /// <summary>
    /// Permanent and Total status
    /// </summary>
    [Id(24)]
    public bool IsPermanentAndTotal { get; set; }

    /// <summary>
    /// Special Monthly Compensation level if any
    /// </summary>
    [Id(25)]
    public string? SpecialMonthlyCompensation { get; set; }

    /// <summary>
    /// Tracking notes for this SC condition
    /// </summary>
    [Id(26)]
    public List<ScConditionNote> Notes { get; set; } = new();
}

/// <summary>
/// Individual rated disability within a service connected condition
/// </summary>
[GenerateSerializer]
public class RatedDisability
{
    /// <summary>
    /// Name of the rated condition
    /// </summary>
    [Id(0)]
    public string ConditionName { get; set; } = string.Empty;

    /// <summary>
    /// Rating percentage for this specific disability
    /// </summary>
    [Id(1)]
    public int RatingPercentage { get; set; }

    /// <summary>
    /// Date this rating became effective
    /// </summary>
    [Id(2)]
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// VA diagnostic code
    /// </summary>
    [Id(3)]
    public string? DiagnosticCode { get; set; }

    /// <summary>
    /// Static vs dynamic rating — static ratings are not subject to re-evaluation
    /// </summary>
    [Id(4)]
    public bool IsStatic { get; set; }
}

/// <summary>
/// Tracking note for SC condition
/// </summary>
[GenerateSerializer]
public class ScConditionNote
{
    /// <summary>
    /// Date the note was recorded
    /// </summary>
    [Id(0)]
    public DateTime NoteDate { get; set; }

    /// <summary>
    /// Author of the note
    /// </summary>
    [Id(1)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Note content
    /// </summary>
    [Id(2)]
    public string NoteText { get; set; } = string.Empty;
}
