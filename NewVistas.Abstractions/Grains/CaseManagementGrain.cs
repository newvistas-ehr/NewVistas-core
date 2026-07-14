// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>A patient's case-management spine (grain key <c>CASE-MGMT:{patientId}</c>).</summary>
public class CaseManagementGrain : Grain, ICaseManagementGrain
{
    private readonly IPersistentState<CaseManagementState> _state;

    public CaseManagementGrain(
        [PersistentState("caseManagementState", "caseManagementStore")]
        IPersistentState<CaseManagementState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.PatientId = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> AddGoalAsync(string description, CaseGoalDomain domain, DateTime? targetDate, string? sourceReference, string byUser)
    {
        string goalId = Guid.NewGuid().ToString();
        _state.State.Goals.Add(new CaseGoal
        {
            GoalId = goalId,
            Description = description,
            Domain = domain,
            Status = CaseGoalStatus.Active,
            TargetDate = targetDate,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = byUser,
            SourceReference = sourceReference
        });
        await SaveAsync();
        return goalId;
    }

    public async Task UpdateGoalStatusAsync(string goalId, CaseGoalStatus status, string byUser)
    {
        CaseGoal g = Goal(goalId);
        g.Status = status;
        await SaveAsync();
    }

    public async Task<string> AddWorkStepAsync(string goalId, string description, DateTime? dueDate, string byUser)
    {
        CaseGoal g = Goal(goalId);
        string stepId = Guid.NewGuid().ToString();
        g.WorkSteps.Add(new CaseWorkStep { StepId = stepId, Description = description, Status = CaseWorkStepStatus.Pending, DueDate = dueDate });
        await SaveAsync();
        return stepId;
    }

    public async Task UpdateWorkStepStatusAsync(string goalId, string stepId, CaseWorkStepStatus status, string byUser)
    {
        CaseWorkStep step = Goal(goalId).WorkSteps.FirstOrDefault(s => s.StepId == stepId)
            ?? throw new InvalidOperationException("Work step not found.");
        step.Status = status;
        step.CompletedDate = status == CaseWorkStepStatus.Done ? DateTime.UtcNow : null;
        await SaveAsync();
    }

    public async Task<string> AddFollowUpAsync(string goalId, DateTime date, string note, string byUser)
    {
        CaseGoal g = Goal(goalId);
        string followUpId = Guid.NewGuid().ToString();
        g.FollowUps.Add(new CaseFollowUp { FollowUpId = followUpId, Date = date, Note = note });
        await SaveAsync();
        return followUpId;
    }

    public async Task CompleteFollowUpAsync(string goalId, string followUpId, string byUser)
    {
        CaseFollowUp f = Goal(goalId).FollowUps.FirstOrDefault(x => x.FollowUpId == followUpId)
            ?? throw new InvalidOperationException("Follow-up not found.");
        f.Completed = true;
        await SaveAsync();
    }

    public async Task RecordOutcomeAsync(string goalId, bool achieved, string note, string byUser)
    {
        CaseGoal g = Goal(goalId);
        g.OutcomeAchieved = achieved;
        g.OutcomeNote = note;
        g.OutcomeDate = DateTime.UtcNow;
        g.Status = achieved ? CaseGoalStatus.Achieved : CaseGoalStatus.Discontinued;
        await SaveAsync();
    }

    public Task<CaseManagementState> GetAsync() => Task.FromResult(_state.State);

    private CaseGoal Goal(string goalId) =>
        _state.State.Goals.FirstOrDefault(g => g.GoalId == goalId)
        ?? throw new InvalidOperationException("Goal not found.");

    private async Task SaveAsync()
    {
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await GrainFactory.GetGrain<ICaseManagementIndexGrain>("CASE-MGMT-INDEX").AddOrUpdateAsync(new CaseloadEntry
        {
            PatientId = _state.State.PatientId,
            ActiveGoalCount = _state.State.Goals.Count(g => g.Status == CaseGoalStatus.Active),
            LastModifiedDate = _state.State.LastModifiedDate
        });
    }
}

/// <summary>Caseload directory (grain key <c>CASE-MGMT-INDEX</c>).</summary>
public class CaseManagementIndexGrain : Grain, ICaseManagementIndexGrain
{
    private readonly IPersistentState<CaseManagementIndexState> _state;

    public CaseManagementIndexGrain(
        [PersistentState("caseManagementIndexState", "caseManagementIndexStore")]
        IPersistentState<CaseManagementIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(CaseloadEntry entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.PatientId))
            return;
        _state.State.Entries.RemoveAll(e => e.PatientId == entry.PatientId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<CaseloadEntry>> GetCaseloadAsync() =>
        Task.FromResult(_state.State.Entries.OrderByDescending(e => e.LastModifiedDate).ToList());
}
