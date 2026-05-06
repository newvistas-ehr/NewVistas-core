// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class NursingTaskWorklistGrain : Grain, INursingTaskWorklistGrain
{
    private readonly IPersistentState<NursingTaskWorklistState> _state;

    public NursingTaskWorklistGrain(
        [PersistentState("nursingTaskWorklistState", "nursingTaskWorklistStore")]
        IPersistentState<NursingTaskWorklistState> state)
    {
        _state = state;
    }

    public Task<NursingTaskWorklistState> GetAsync() => Task.FromResult(_state.State);

    public async Task RefreshAsync(
        List<NursingTask> medicationTasks,
        List<NursingTask> vitalTasks,
        List<NursingTask> interventionTasks,
        List<NursingTask> otherTasks)
    {
        // Keep completed/deferred tasks, replace pending ones
        List<NursingTask> kept = _state.State.Tasks
            .Where(t => t.Status == NursingTaskStatus.Completed || t.Status == NursingTaskStatus.Deferred)
            .ToList();

        kept.AddRange(medicationTasks);
        kept.AddRange(vitalTasks);
        kept.AddRange(interventionTasks);
        kept.AddRange(otherTasks);

        _state.State.Tasks = kept.OrderBy(t => t.DueDateTime).ToList();
        _state.State.LastRefreshedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTaskAsync(NursingTask task)
    {
        _state.State.Tasks.Add(task);
        _state.State.Tasks = _state.State.Tasks.OrderBy(t => t.DueDateTime).ToList();
        await _state.WriteStateAsync();
    }

    public async Task CompleteTaskAsync(string taskId, string nurseId, string nurseName, string? notes)
    {
        int idx = _state.State.Tasks.FindIndex(t => t.TaskId == taskId);
        if (idx < 0) return;

        NursingTask original = _state.State.Tasks[idx];
        _state.State.Tasks[idx] = original with
        {
            Status = NursingTaskStatus.Completed,
            CompletedDateTime = DateTime.UtcNow,
            CompletedByNurseId = nurseId,
            CompletedByNurseName = nurseName,
            Notes = notes ?? original.Notes,
        };
        await _state.WriteStateAsync();
    }

    public async Task DeferTaskAsync(string taskId, string? reason)
    {
        int idx = _state.State.Tasks.FindIndex(t => t.TaskId == taskId);
        if (idx < 0) return;

        NursingTask original = _state.State.Tasks[idx];
        _state.State.Tasks[idx] = original with
        {
            Status = NursingTaskStatus.Deferred,
            Notes = reason ?? original.Notes,
        };
        await _state.WriteStateAsync();
    }

    public Task<List<NursingTask>> GetDueTasksAsync()
        => Task.FromResult(
            _state.State.Tasks
                .Where(t => t.Status == NursingTaskStatus.Due || t.Status == NursingTaskStatus.Overdue)
                .ToList());

    public Task<List<NursingTask>> GetTasksByCategoryAsync(NursingTaskCategory category)
        => Task.FromResult(
            _state.State.Tasks.Where(t => t.Category == category).ToList());
}
