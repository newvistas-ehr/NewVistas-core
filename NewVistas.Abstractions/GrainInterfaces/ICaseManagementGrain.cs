// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A patient's case-management spine (grain key <c>CASE-MGMT:{patientId}</c>): goals, the work-steps
/// toward each, scheduled follow-ups, and recorded outcomes. Maintains the caseload index. Program-
/// agnostic; composes with the clinical care plans rather than replacing them.
/// </summary>
public interface ICaseManagementGrain : IGrainWithStringKey
{
    /// <summary>Adds a goal (optionally citing the source that opened it). Returns the goal id.</summary>
    Task<string> AddGoalAsync(string description, CaseGoalDomain domain, DateTime? targetDate, string? sourceReference, string byUser);

    Task UpdateGoalStatusAsync(string goalId, CaseGoalStatus status, string byUser);

    /// <summary>Adds a work-step to a goal. Returns the step id.</summary>
    Task<string> AddWorkStepAsync(string goalId, string description, DateTime? dueDate, string byUser);

    Task UpdateWorkStepStatusAsync(string goalId, string stepId, CaseWorkStepStatus status, string byUser);

    /// <summary>Adds a scheduled follow-up to a goal. Returns the follow-up id.</summary>
    Task<string> AddFollowUpAsync(string goalId, DateTime date, string note, string byUser);

    Task CompleteFollowUpAsync(string goalId, string followUpId, string byUser);

    /// <summary>Records the goal's outcome (achieved or not, with a note) and closes it.</summary>
    Task RecordOutcomeAsync(string goalId, bool achieved, string note, string byUser);

    Task<CaseManagementState> GetAsync();
}

/// <summary>Caseload directory (grain key <c>CASE-MGMT-INDEX</c>) — patients with case-management goals.</summary>
public interface ICaseManagementIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(CaseloadEntry entry);
    Task<List<CaseloadEntry>> GetCaseloadAsync();
}
