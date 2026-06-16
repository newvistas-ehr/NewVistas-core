// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Clinical Reminder Grain implementation based on VistA CLINICAL REMINDER file (#811.9)
/// </summary>
public class ClinicalReminderGrain : Grain, IClinicalReminderGrain
{
    private readonly IPersistentState<ClinicalReminderState> _state;

    public ClinicalReminderGrain(
        [PersistentState("clinicalReminderState", "clinicalReminderStore")] IPersistentState<ClinicalReminderState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReminderId))
        {
            _state.State.ReminderId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ClinicalReminderState> GetReminderAsync() => Task.FromResult(_state.State);

    public async Task CreateReminderAsync(
        string patientId, string reminderName, string? reminderDefinitionId,
        string? category, string? priority, string? frequency, DateTime? dueDate)
    {
        _state.State.PatientId = patientId;
        _state.State.ReminderName = reminderName;
        _state.State.ReminderDefinitionId = reminderDefinitionId;
        _state.State.Category = category;
        _state.State.Priority = priority;
        _state.State.Frequency = frequency;
        _state.State.DueDate = dueDate;
        _state.State.Status = "DUE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDoneAsync(DateTime occurrenceDate, string? evaluatedByProviderId, string? evaluatedByProviderName)
    {
        _state.State.Status = "DONE";
        _state.State.LastOccurrenceDate = occurrenceDate;
        _state.State.EvaluatedByProviderId = evaluatedByProviderId;
        _state.State.EvaluatedByProviderName = evaluatedByProviderName;
        _state.State.EvaluationDateTime = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveAsync(string? comments)
    {
        _state.State.Status = "RESOLVED";
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDueDateAsync(DateTime nextDueDate)
    {
        _state.State.NextDueDate = nextDueDate;
        _state.State.Status = "DUE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkNotApplicableAsync(string? comments)
    {
        _state.State.Status = "NOT APPLICABLE";
        _state.State.IsApplicable = false;
        _state.State.EvaluationResult = "NOT APPLICABLE";
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 10: Evaluation Engine ─────────────────────────────────────────────

    public async Task RecordEvaluationResultAsync(
        string evaluationResult,
        bool isApplicable,
        DateTime evaluatedDateTime)
    {
        _state.State.EvaluationResult = evaluationResult;
        _state.State.IsApplicable = isApplicable;
        _state.State.LastEvaluatedDateTime = evaluatedDateTime;
        _state.State.Status = evaluationResult;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCoverSheetLevelAsync(int level)
    {
        _state.State.CoverSheetLevel = level;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkApplicableAsync()
    {
        _state.State.Status = "APPLICABLE";
        _state.State.IsApplicable = true;
        _state.State.EvaluationResult = "APPLICABLE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetMentalHealthTestRequiredAsync(bool required)
    {
        _state.State.RequiresMentalHealthTest = required;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordMstDataCapturedAsync()
    {
        _state.State.MstDataCaptured = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
