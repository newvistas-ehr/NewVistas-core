// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class SafetyPlanIndexState
{
    [Id(0)] public List<SafetyPlanSummary> Plans { get; set; } = new();
}

public class SafetyPlanIndexGrain : Grain, ISafetyPlanIndexGrain
{
    private readonly IPersistentState<SafetyPlanIndexState> _state;

    public SafetyPlanIndexGrain(
        [PersistentState("spPlanIndexState", "spPlanIndexStore")] IPersistentState<SafetyPlanIndexState> state)
    {
        _state = state;
    }

    public Task<List<SafetyPlanSummary>> GetAllPlansAsync() =>
        Task.FromResult(_state.State.Plans
            .OrderByDescending(p => p.CreatedDate)
            .ToList());

    public Task<SafetyPlanSummary?> GetActivePlanAsync() =>
        Task.FromResult(_state.State.Plans
            .OrderByDescending(p => p.CreatedDate)
            .FirstOrDefault(p => p.Status == SafetyPlanStatus.Active || p.Status == SafetyPlanStatus.Draft));

    public async Task UpsertPlanAsync(SafetyPlanSummary summary)
    {
        int idx = _state.State.Plans.FindIndex(p => p.PlanId == summary.PlanId);
        if (idx >= 0)
            _state.State.Plans[idx] = summary;
        else
            _state.State.Plans.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemovePlanAsync(string planId)
    {
        int idx = _state.State.Plans.FindIndex(p => p.PlanId == planId);
        if (idx >= 0)
        {
            _state.State.Plans.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
