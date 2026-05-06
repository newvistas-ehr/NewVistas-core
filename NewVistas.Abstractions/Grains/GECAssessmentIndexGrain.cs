// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class GECAssessmentIndexState
{
    [Id(0)] public List<GECAssessmentIndexEntry> Assessments { get; set; } = new();
}

public class GECAssessmentIndexGrain : Grain, IGECAssessmentIndexGrain
{
    private readonly IPersistentState<GECAssessmentIndexState> _state;

    public GECAssessmentIndexGrain(
        [PersistentState("gecAssessmentIndexState", "gecAssessmentIndexStore")] IPersistentState<GECAssessmentIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertAssessmentAsync(GECAssessmentIndexEntry entry)
    {
        GECAssessmentIndexEntry? existing = _state.State.Assessments.Find(a => a.AssessmentId == entry.AssessmentId);
        if (existing is not null)
            _state.State.Assessments.Remove(existing);
        _state.State.Assessments.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<GECAssessmentIndexEntry>> GetAllAssessmentsAsync()
    {
        List<GECAssessmentIndexEntry> result = _state.State.Assessments
            .OrderByDescending(a => a.AssessmentDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<GECAssessmentIndexEntry>> GetAssessmentsByTypeAsync(GECAssessmentType type)
    {
        List<GECAssessmentIndexEntry> result = _state.State.Assessments
            .Where(a => a.AssessmentType == type)
            .OrderByDescending(a => a.AssessmentDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<GECAssessmentIndexEntry?> GetLatestAssessmentAsync()
    {
        GECAssessmentIndexEntry? latest = _state.State.Assessments
            .OrderByDescending(a => a.AssessmentDate)
            .FirstOrDefault();
        return Task.FromResult(latest);
    }
}
