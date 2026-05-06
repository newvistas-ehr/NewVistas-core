// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class RtCourseIndexState
{
    [Id(0)] public List<RtCourseIndexEntry> Courses { get; set; } = new();
}

public class RadiationTherapyCourseIndexGrain : Grain, IRadiationTherapyCourseIndexGrain
{
    private readonly IPersistentState<RtCourseIndexState> _state;

    public RadiationTherapyCourseIndexGrain(
        [PersistentState("rtCourseIndexState", "rtCourseIndexStore")] IPersistentState<RtCourseIndexState> state)
    {
        _state = state;
    }

    public Task<List<RtCourseIndexEntry>> GetAllCoursesAsync() =>
        Task.FromResult(_state.State.Courses
            .OrderByDescending(c => c.TreatmentStartDate ?? DateTime.MinValue)
            .ToList());

    public Task<List<RtCourseIndexEntry>> GetActiveCoursesAsync() =>
        Task.FromResult(_state.State.Courses
            .Where(c => c.Status == RtCourseStatus.Active || c.Status == RtCourseStatus.OnHold)
            .OrderByDescending(c => c.TreatmentStartDate ?? DateTime.MinValue)
            .ToList());

    public Task<List<RtCourseIndexEntry>> GetCompletedCoursesAsync() =>
        Task.FromResult(_state.State.Courses
            .Where(c => c.Status == RtCourseStatus.Completed)
            .OrderByDescending(c => c.TreatmentCompletionDate ?? DateTime.MinValue)
            .ToList());

    public async Task UpsertCourseAsync(RtCourseIndexEntry entry)
    {
        int idx = _state.State.Courses.FindIndex(c => c.CourseId == entry.CourseId);
        if (idx >= 0)
            _state.State.Courses[idx] = entry;
        else
            _state.State.Courses.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveCourseAsync(string courseId)
    {
        int idx = _state.State.Courses.FindIndex(c => c.CourseId == courseId);
        if (idx >= 0)
        {
            _state.State.Courses.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
