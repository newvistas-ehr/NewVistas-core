// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PainAssessmentGrain : Grain, IPainAssessmentGrain
{
    private readonly IPersistentState<PainAssessmentState> _state;

    public PainAssessmentGrain(
        [PersistentState("painAssessmentState", "painAssessmentStore")]
        IPersistentState<PainAssessmentState> state)
    { _state = state; }

    public Task<PainAssessmentState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(PainAssessmentState initialState)
    {
        string id = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;
        _state.State = initialState;
        _state.State.AssessmentId    = id;
        _state.State.Status          = NursingAssessmentStatus.Draft;
        _state.State.CreatedDate     = now;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
        return id;
    }

    public async Task SignAsync(string nurseId, string nurseName)
    {
        _state.State.Status           = NursingAssessmentStatus.Signed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordReassessmentAsync(
        int postInterventionScore, int minutesSinceIntervention, bool interventionEffective)
    {
        _state.State.PostInterventionScore     = postInterventionScore;
        _state.State.MinutesSinceIntervention  = minutesSinceIntervention;
        _state.State.InterventionEffective     = interventionEffective;
        _state.State.LastModifiedDate          = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
