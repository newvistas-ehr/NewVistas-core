// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>Per-patient assessment index grain — maintains newest-first ordering.</summary>
public class NursingAssessmentIndexGrain : Grain, INursingAssessmentIndexGrain
{
    private readonly IPersistentState<NursingAssessmentIndexState> _state;

    public NursingAssessmentIndexGrain(
        [PersistentState("nursingAssessmentIndexState", "nursingAssessmentIndexStore")]
        IPersistentState<NursingAssessmentIndexState> state)
    {
        _state = state;
    }

    public Task<NursingAssessmentIndexState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task AddEntryAsync(NursingAssessmentIndexEntry entry)
    {
        // Replace any stale entry with the same ID before inserting
        _state.State.Assessments.RemoveAll(a => a.AssessmentId == entry.AssessmentId);
        _state.State.Assessments.Add(entry);

        // Keep newest first
        _state.State.Assessments.Sort(
            (a, b) => b.AssessmentDateTime.CompareTo(a.AssessmentDateTime));

        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(string assessmentId, NursingAssessmentStatus status)
    {
        NursingAssessmentIndexEntry? entry =
            _state.State.Assessments.FirstOrDefault(a => a.AssessmentId == assessmentId);

        if (entry is null) return;

        entry.Status = status;
        await _state.WriteStateAsync();
    }
}
