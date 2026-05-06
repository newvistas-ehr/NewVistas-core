// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PainAssessmentIndexGrain : Grain, IPainAssessmentIndexGrain
{
    private readonly IPersistentState<PainAssessmentIndexState> _state;

    public PainAssessmentIndexGrain(
        [PersistentState("painAssessmentIndexState", "painAssessmentIndexStore")]
        IPersistentState<PainAssessmentIndexState> state)
    { _state = state; }

    public Task<List<PainAssessmentIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(PainAssessmentIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.AssessmentId == entry.AssessmentId);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<PainAssessmentIndexEntry?> GetLatestAsync()
        => Task.FromResult(_state.State.Entries.FirstOrDefault());
}
