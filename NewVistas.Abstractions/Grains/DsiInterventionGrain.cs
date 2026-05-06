// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// DSI Intervention Definition Grain — stores a CDS intervention definition.
/// </summary>
public class DsiInterventionGrain : Grain, IDsiInterventionGrain
{
    private readonly IPersistentState<DsiInterventionState> _state;

    public DsiInterventionGrain(
        [PersistentState("dsiInterventionState", "dsiInterventionStore")] IPersistentState<DsiInterventionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InterventionId))
        {
            string key = this.GetPrimaryKeyString();
            int colonIdx = key.IndexOf(':');
            _state.State.InterventionId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SaveInterventionAsync(DsiInterventionState intervention)
    {
        _state.State.InterventionId = intervention.InterventionId;
        _state.State.Title = intervention.Title;
        _state.State.Description = intervention.Description;
        _state.State.InterventionType = intervention.InterventionType;
        _state.State.ClinicalDomain = intervention.ClinicalDomain;
        _state.State.IsActive = intervention.IsActive;
        _state.State.SourceCitation = intervention.SourceCitation;
        _state.State.Developer = intervention.Developer;
        _state.State.FundingSource = intervention.FundingSource;
        _state.State.LastRevisedDate = intervention.LastRevisedDate;
        _state.State.ModelPurpose = intervention.ModelPurpose;
        _state.State.TrainingDataDescription = intervention.TrainingDataDescription;
        _state.State.PerformanceMetrics = intervention.PerformanceMetrics;
        _state.State.KnownLimitations = intervention.KnownLimitations;
        _state.State.FairnessAssessment = intervention.FairnessAssessment;
        _state.State.RiskManagement = intervention.RiskManagement;
        _state.State.InputDataRequirements = intervention.InputDataRequirements;
        _state.State.OutputDescription = intervention.OutputDescription;
        _state.State.TriggerCriteria = intervention.TriggerCriteria;
        _state.State.RecommendedAction = intervention.RecommendedAction;
        _state.State.Severity = intervention.Severity;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<DsiInterventionState> GetInterventionAsync() => Task.FromResult(_state.State);

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// DSI Intervention Index Grain — listing of all interventions.
/// </summary>
public class DsiInterventionIndexGrain : Grain, IDsiInterventionIndexGrain
{
    private readonly IPersistentState<DsiInterventionIndexState> _state;

    public DsiInterventionIndexGrain(
        [PersistentState("dsiInterventionIndexState", "dsiInterventionIndexStore")] IPersistentState<DsiInterventionIndexState> state)
    {
        _state = state;
    }

    public async Task AddInterventionAsync(DsiInterventionSummary summary)
    {
        _state.State.Interventions.RemoveAll(i => i.InterventionId == summary.InterventionId);
        _state.State.Interventions.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveInterventionAsync(string interventionId)
    {
        _state.State.Interventions.RemoveAll(i => i.InterventionId == interventionId);
        await _state.WriteStateAsync();
    }

    public Task<List<DsiInterventionSummary>> GetAllInterventionsAsync()
        => Task.FromResult(_state.State.Interventions.ToList());

    public Task<List<DsiInterventionSummary>> GetActiveInterventionsAsync()
        => Task.FromResult(_state.State.Interventions.Where(i => i.IsActive).ToList());
}
