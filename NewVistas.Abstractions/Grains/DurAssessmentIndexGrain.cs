// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient index grain for Drug Utilization Review assessments.
/// Provides efficient list queries without activating individual assessment grains.
///
/// Key format: DUR-IDX:{patientId}
/// </summary>
public class DurAssessmentIndexGrain : Grain, IDurAssessmentIndexGrain
{
    private readonly IPersistentState<DurAssessmentIndexState> _state;

    public DurAssessmentIndexGrain(
        [PersistentState("durAssessmentIndexState", "durAssessmentIndexStore")]
        IPersistentState<DurAssessmentIndexState> state)
    {
        _state = state;
    }

    public Task<List<DurAssessmentIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<DurAssessmentIndexEntry>> GetPendingReviewAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == DurAssessmentStatus.Pending || e.Status == DurAssessmentStatus.Failed)
            .ToList());

    public Task<List<DurAssessmentIndexEntry>> GetByStatusAsync(DurAssessmentStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public Task<DurAssessmentIndexEntry?> GetByPrescriptionAsync(string prescriptionId)
        => Task.FromResult(_state.State.Entries.FirstOrDefault(e => e.PrescriptionId == prescriptionId));

    public async Task AddEntryAsync(DurAssessmentIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(
        string assessmentId,
        DurAssessmentStatus status,
        DurOutcome overallOutcome,
        int failedCheckCount,
        int warningCheckCount)
    {
        DurAssessmentIndexEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.AssessmentId == assessmentId);

        if (entry is not null)
        {
            entry.Status = status;
            entry.OverallOutcome = overallOutcome;
            entry.FailedCheckCount = failedCheckCount;
            entry.WarningCheckCount = warningCheckCount;
            await _state.WriteStateAsync();
        }
    }
}
