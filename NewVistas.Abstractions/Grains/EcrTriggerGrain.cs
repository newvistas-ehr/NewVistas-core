// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Reportable Condition Trigger Grain — stores a single reportable condition definition.
/// </summary>
public class EcrTriggerGrain : Grain, IEcrTriggerGrain
{
    private readonly IPersistentState<EcrTriggerState> _state;

    public EcrTriggerGrain(
        [PersistentState("ecrTriggerState", "ecrTriggerStore")] IPersistentState<EcrTriggerState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.TriggerId))
        {
            string key = this.GetPrimaryKeyString();
            int colonIdx = key.IndexOf(':');
            _state.State.TriggerId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SaveTriggerAsync(EcrTriggerState trigger)
    {
        _state.State.TriggerId = trigger.TriggerId;
        _state.State.ConditionName = trigger.ConditionName;
        _state.State.ConditionCode = trigger.ConditionCode;
        _state.State.ConditionCodeSystem = trigger.ConditionCodeSystem;
        _state.State.TriggerCodes = trigger.TriggerCodes;
        _state.State.Jurisdictions = trigger.Jurisdictions;
        _state.State.ReportingTimeframe = trigger.ReportingTimeframe;
        _state.State.IsActive = trigger.IsActive;
        _state.State.Category = trigger.Category;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<EcrTriggerState> GetTriggerAsync() => Task.FromResult(_state.State);

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Reportable Condition Trigger Index Grain — listing of all triggers.
/// </summary>
public class EcrTriggerIndexGrain : Grain, IEcrTriggerIndexGrain
{
    private readonly IPersistentState<EcrTriggerIndexState> _state;

    public EcrTriggerIndexGrain(
        [PersistentState("ecrTriggerIndexState", "ecrTriggerIndexStore")] IPersistentState<EcrTriggerIndexState> state)
    {
        _state = state;
    }

    public async Task AddTriggerAsync(EcrTriggerSummary summary)
    {
        _state.State.Triggers.RemoveAll(t => t.TriggerId == summary.TriggerId);
        _state.State.Triggers.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveTriggerAsync(string triggerId)
    {
        _state.State.Triggers.RemoveAll(t => t.TriggerId == triggerId);
        await _state.WriteStateAsync();
    }

    public Task<List<EcrTriggerSummary>> GetAllTriggersAsync()
        => Task.FromResult(_state.State.Triggers.ToList());

    public Task<List<EcrTriggerSummary>> GetActiveTriggersAsync()
        => Task.FromResult(_state.State.Triggers.Where(t => t.IsActive).ToList());
}
