// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Nursing Assessment Grain — VistA File #212.1 (NURSING ASSESSMENT).
/// Persists a single head-to-toe nursing assessment document.
/// </summary>
public class NursingAssessmentGrain : Grain, INursingAssessmentGrain
{
    private readonly IPersistentState<NursingAssessmentState> _state;

    public NursingAssessmentGrain(
        [PersistentState("nursingAssessmentState", "nursingAssessmentStore")]
        IPersistentState<NursingAssessmentState> state)
    {
        _state = state;
    }

    public Task<NursingAssessmentState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(NursingAssessmentState initialState)
    {
        if (!string.IsNullOrEmpty(_state.State.AssessmentId))
            return; // idempotent — already initialised

        _state.State = initialState;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SignAsync(string signedById, string signedByName, DateTime signedDateTime)
    {
        _state.State.Status = NursingAssessmentStatus.Signed;
        _state.State.SignedById = signedById;
        _state.State.SignedByName = signedByName;
        _state.State.SignedDateTime = signedDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AmendAsync(string? narrativeNotes, string amendedById, string amendedByName)
    {
        _state.State.Status = NursingAssessmentStatus.Amended;
        if (narrativeNotes is not null)
            _state.State.NarrativeNotes = narrativeNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
