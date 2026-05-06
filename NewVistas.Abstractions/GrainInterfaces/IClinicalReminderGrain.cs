// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Clinical Reminder Grain Interface based on VistA CLINICAL REMINDER file (#811.9)
/// </summary>
public interface IClinicalReminderGrain : IGrainWithStringKey
{
    Task<GrainStates.ClinicalReminderState> GetReminderAsync();

    Task CreateReminderAsync(
        string patientId,
        string reminderName,
        string? reminderDefinitionId,
        string? category,
        string? priority,
        string? frequency,
        DateTime? dueDate);

    Task MarkDoneAsync(DateTime occurrenceDate, string? evaluatedByProviderId, string? evaluatedByProviderName);
    Task ResolveAsync(string? comments);
    Task UpdateDueDateAsync(DateTime nextDueDate);
    Task MarkNotApplicableAsync(string? comments);

    // ── GAP 10: Evaluation Engine ─────────────────────────────────────────────

    /// <summary>
    /// Records the result of the reminder evaluation engine for this patient.
    /// Mirrors VistA rReminders.pas EvaluateReminder() result.
    /// Sets IsApplicable, EvaluationResult, and LastEvaluatedDateTime.
    /// </summary>
    Task RecordEvaluationResultAsync(
        string evaluationResult,
        bool isApplicable,
        DateTime evaluatedDateTime);

    /// <summary>
    /// Sets the cover sheet display level for this reminder.
    /// 0 = Do not show, 1 = Show if applicable, 2 = Always show.
    /// Mirrors VistA reminder cover sheet level definition field.
    /// </summary>
    Task SetCoverSheetLevelAsync(int level);

    /// <summary>
    /// Marks this reminder as APPLICABLE — patient meets cohort criteria
    /// but reminder is not yet due. Mirrors VistA APPLICABLE status.
    /// </summary>
    Task MarkApplicableAsync();

    /// <summary>
    /// Records that a mental health test result was captured for this reminder.
    /// Mirrors VistA rReminders.pas VerifyMentalHealthTestComplete().
    /// </summary>
    Task SetMentalHealthTestRequiredAsync(bool required);

    /// <summary>
    /// Records that MST (Military Sexual Trauma) data was captured
    /// via this reminder interaction. Mirrors VistA rReminders.pas SaveMSTDataFromReminder().
    /// </summary>
    Task RecordMstDataCapturedAsync();
}
