// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
