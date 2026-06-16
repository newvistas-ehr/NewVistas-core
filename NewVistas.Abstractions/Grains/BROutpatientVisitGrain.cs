// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Outpatient Visit Grain — grain key: "BR-VISIT:{visitId}"
/// </summary>
public class BROutpatientVisitGrain : Grain, IBROutpatientVisitGrain
{
    private readonly IPersistentState<BROutpatientVisitState> _state;

    public BROutpatientVisitGrain(
        [PersistentState("brOutpatientVisitState", "brOutpatientVisitStore")]
        IPersistentState<BROutpatientVisitState> state)
    {
        _state = state;
    }

    public Task<BROutpatientVisitState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string visitId,
        string patientId,
        DateTime visitDate,
        BRTrainingArea trainingArea,
        string therapistId,
        string therapistName,
        string location,
        int durationMinutes,
        string? sessionNotes,
        List<string> skillsAddressed)
    {
        _state.State.VisitId = visitId;
        _state.State.PatientId = patientId;
        _state.State.VisitDate = visitDate;
        _state.State.TrainingArea = trainingArea;
        _state.State.TherapistId = therapistId;
        _state.State.TherapistName = therapistName;
        _state.State.Location = location;
        _state.State.DurationMinutes = durationMinutes;
        _state.State.SessionNotes = sessionNotes;
        _state.State.SkillsAddressed = skillsAddressed;
        _state.State.Status = BRVisitStatus.Scheduled;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProgressNoteAsync(string note, string authorId, string authorName)
    {
        _state.State.ProgressNotes.Add(new BRProgressNote
        {
            Note = note,
            AuthorId = authorId,
            AuthorName = authorName,
            RecordedDate = DateTime.UtcNow
        });
        if (_state.State.Status == BRVisitStatus.Scheduled)
            _state.State.Status = BRVisitStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(string outcomeSummary, BRVisitOutcome outcome)
    {
        _state.State.OutcomeSummary = outcomeSummary;
        _state.State.Outcome = outcome;
        _state.State.Status = BRVisitStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason)
    {
        _state.State.CancellationReason = reason;
        _state.State.Status = BRVisitStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
