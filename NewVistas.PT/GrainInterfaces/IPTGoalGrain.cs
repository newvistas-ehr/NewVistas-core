// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Manages PT goals for a patient and body group.
/// Key format: "PTGOAL:{patientId}:{bodyGroup}"
/// </summary>
public interface IPTGoalGrain : IGrainWithStringKey
{
    /// <summary>Returns the full goal state for this patient and body group.</summary>
    Task<PTGoalState> GetGoalsAsync();

    /// <summary>Adds a new goal. Returns the generated goal ID.</summary>
    Task<string> AddGoalAsync(PTGoal goal);

    /// <summary>Updates an existing goal's status, current value, and/or notes.</summary>
    Task UpdateGoalAsync(string goalId, GoalStatus? status, decimal? currentValue, string? notes);

    /// <summary>Records a progress measurement for a goal.</summary>
    Task AddProgressEntryAsync(string goalId, decimal value, string? notes);

    /// <summary>Removes a goal by ID.</summary>
    Task RemoveGoalAsync(string goalId);

    /// <summary>Returns only active goals.</summary>
    Task<List<PTGoal>> GetActiveGoalsAsync();
}
