// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Manages PT goals for a patient and body group.
/// Key format: "PTGOAL:{patientId}:{bodyGroup}"
/// </summary>
public class PTGoalGrain : Grain, IPTGoalGrain
{
    private readonly IPersistentState<PTGoalState> _state;

    public PTGoalGrain(
        [PersistentState("ptGoalState", "physTherapyGoalStore")]
        IPersistentState<PTGoalState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            string key = this.GetPrimaryKeyString();
            string[] parts = key.Split(':');
            // Key format: PTGOAL:{patientId}:{bodyGroup}
            _state.State.PatientId = parts.Length > 1 ? parts[1] : key;
            if (parts.Length > 2 && Enum.TryParse<BodyGroup>(parts[2], out BodyGroup bg))
                _state.State.BodyGroup = bg;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PTGoalState> GetGoalsAsync() => Task.FromResult(_state.State);

    public async Task<string> AddGoalAsync(PTGoal goal)
    {
        goal.GoalId = Guid.NewGuid().ToString();
        goal.BodyGroup = _state.State.BodyGroup;
        goal.CreatedDate = DateTime.UtcNow;
        goal.LastModifiedDate = DateTime.UtcNow;

        _state.State.Goals.Add(goal);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return goal.GoalId;
    }

    public async Task UpdateGoalAsync(string goalId, GoalStatus? status, decimal? currentValue, string? notes)
    {
        PTGoal? goal = _state.State.Goals.FirstOrDefault(g => g.GoalId == goalId);
        if (goal == null) return;

        if (status.HasValue) goal.Status = status.Value;
        if (currentValue.HasValue) goal.CurrentValue = currentValue.Value;
        if (notes != null) goal.Notes = notes;
        goal.LastModifiedDate = DateTime.UtcNow;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProgressEntryAsync(string goalId, decimal value, string? notes)
    {
        PTGoal? goal = _state.State.Goals.FirstOrDefault(g => g.GoalId == goalId);
        if (goal == null) return;

        goal.ProgressEntries.Add(new PTGoalProgressEntry
        {
            Date = DateTime.UtcNow,
            Value = value,
            Notes = notes
        });
        goal.CurrentValue = value;
        goal.LastModifiedDate = DateTime.UtcNow;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveGoalAsync(string goalId)
    {
        int removed = _state.State.Goals.RemoveAll(g => g.GoalId == goalId);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<PTGoal>> GetActiveGoalsAsync()
        => Task.FromResult(_state.State.Goals.Where(g => g.Status == GoalStatus.Active).ToList());
}
