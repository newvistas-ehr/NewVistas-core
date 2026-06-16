// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Clinical Reminder grain based on VistA CLINICAL REMINDER file (#811.9) and results
/// </summary>
[GenerateSerializer]
public class ClinicalReminderState
{
    [Id(0)]
    public string ReminderId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Reminder Definition (.01) � pointer to REMINDER DEFINITION file (#811.9)
    /// </summary>
    [Id(2)]
    public string ReminderName { get; set; } = string.Empty;

    [Id(3)]
    public string? ReminderDefinitionId { get; set; }

    /// <summary>
    /// Status � DUE, APPLICABLE, NOT APPLICABLE, DONE, RESOLVED
    /// </summary>
    [Id(4)]
    public string Status { get; set; } = "DUE";

    /// <summary>
    /// Due Date
    /// </summary>
    [Id(5)]
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Last Occurrence Date
    /// </summary>
    [Id(6)]
    public DateTime? LastOccurrenceDate { get; set; }

    /// <summary>
    /// Next Due Date � calculated from frequency
    /// </summary>
    [Id(7)]
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Frequency � how often the reminder recurs
    /// </summary>
    [Id(8)]
    public string? Frequency { get; set; }

    /// <summary>
    /// Category � IMMUNIZATION, SCREENING, COUNSELING, EXAM, LAB, etc.
    /// </summary>
    [Id(9)]
    public string? Category { get; set; }

    /// <summary>
    /// Priority � HIGH, NORMAL, LOW
    /// </summary>
    [Id(10)]
    public string? Priority { get; set; }

    [Id(11)]
    public string? EvaluatedByProviderId { get; set; }

    [Id(12)]
    public string? EvaluatedByProviderName { get; set; }

    [Id(13)]
    public DateTime? EvaluationDateTime { get; set; }

    [Id(14)]
    public string? Comments { get; set; }

    [Id(15)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── GAP 10: Clinical Reminder Evaluation ──────────────────────────────────

    /// <summary>
    /// True when the reminder is applicable to this patient.
    /// APPLICABLE is distinct from DUE — a reminder may be applicable but not yet due.
    /// Mirrors VistA rReminders.pas EvaluateReminder() APPLICABLE status.
    /// </summary>
    [Id(17)]
    public bool IsApplicable { get; set; }

    /// <summary>
    /// Structured evaluation result from the reminder engine.
    /// Values: DUE, APPLICABLE, NOT APPLICABLE, DONE, RESOLVED, N/A
    /// Mirrors VistA reminder evaluation output.
    /// </summary>
    [Id(18)]
    public string? EvaluationResult { get; set; }

    /// <summary>
    /// Cover sheet display level — controls whether this reminder appears on the
    /// patient cover sheet. Mirrors VistA reminder definition cover sheet level field.
    /// 0 = Do not show, 1 = Show if applicable, 2 = Always show
    /// </summary>
    [Id(19)]
    public int CoverSheetLevel { get; set; }

    /// <summary>
    /// Date/time the reminder engine last evaluated this patient's reminder status
    /// </summary>
    [Id(20)]
    public DateTime? LastEvaluatedDateTime { get; set; }

    /// <summary>
    /// True when a mental health test is required to complete this reminder.
    /// Mirrors VistA rReminders.pas VerifyMentalHealthTestComplete().
    /// </summary>
    [Id(21)]
    public bool RequiresMentalHealthTest { get; set; }

    /// <summary>
    /// True when MST (Military Sexual Trauma) data was captured via this reminder.
    /// Mirrors VistA rReminders.pas SaveMSTDataFromReminder().
    /// </summary>
    [Id(22)]
    public bool MstDataCaptured { get; set; }
}
