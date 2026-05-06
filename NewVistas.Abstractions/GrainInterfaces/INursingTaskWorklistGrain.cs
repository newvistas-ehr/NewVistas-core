// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Aggregated nursing task worklist for a single patient.
/// Combines due medications (MAR), vital sign schedules, care plan interventions,
/// and other ordered nursing activities into a unified task view.
/// Grain key: "NURS-TASKS:{patientId}"
/// </summary>
public interface INursingTaskWorklistGrain : IGrainWithStringKey
{
    Task<NursingTaskWorklistState> GetAsync();

    /// <summary>Regenerates the worklist from current MAR, care plan, and orders.</summary>
    Task RefreshAsync(
        List<NursingTask> medicationTasks,
        List<NursingTask> vitalTasks,
        List<NursingTask> interventionTasks,
        List<NursingTask> otherTasks);

    /// <summary>Adds a single task to the worklist.</summary>
    Task AddTaskAsync(NursingTask task);

    /// <summary>Marks a task as completed.</summary>
    Task CompleteTaskAsync(string taskId, string nurseId, string nurseName, string? notes);

    /// <summary>Marks a task as deferred with reason.</summary>
    Task DeferTaskAsync(string taskId, string? reason);

    /// <summary>Returns only tasks that are due or overdue.</summary>
    Task<List<NursingTask>> GetDueTasksAsync();

    /// <summary>Returns tasks filtered by category.</summary>
    Task<List<NursingTask>> GetTasksByCategoryAsync(NursingTaskCategory category);
}
