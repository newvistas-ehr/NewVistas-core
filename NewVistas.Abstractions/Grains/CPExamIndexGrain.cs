// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class CPExamIndexState
{
    [Id(0)] public List<CPExamIndexEntry> Exams { get; set; } = new();
}

public class CPExamIndexGrain : Grain, ICPExamIndexGrain
{
    private readonly IPersistentState<CPExamIndexState> _state;

    public CPExamIndexGrain(
        [PersistentState("cpExamIndexState", "cpExamIndexStore")] IPersistentState<CPExamIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertExamAsync(CPExamIndexEntry entry)
    {
        CPExamIndexEntry? existing = _state.State.Exams.Find(e => e.ExamId == entry.ExamId);
        if (existing is not null)
            _state.State.Exams.Remove(existing);
        _state.State.Exams.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<CPExamIndexEntry>> GetAllExamsAsync()
    {
        List<CPExamIndexEntry> result = _state.State.Exams
            .OrderByDescending(e => e.ScheduledDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CPExamIndexEntry>> GetScheduledExamsAsync()
    {
        List<CPExamIndexEntry> result = _state.State.Exams
            .Where(e => e.Status is CPExamStatus.Scheduled or CPExamStatus.Rescheduled)
            .OrderBy(e => e.ScheduledDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CPExamIndexEntry>> GetCompletedExamsAsync()
    {
        List<CPExamIndexEntry> result = _state.State.Exams
            .Where(e => e.Status == CPExamStatus.Completed)
            .OrderByDescending(e => e.CompletedDate)
            .ToList();
        return Task.FromResult(result);
    }
}
