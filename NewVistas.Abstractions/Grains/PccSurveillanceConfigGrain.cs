// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PccSurveillanceConfigGrain : Grain, IPccSurveillanceConfigGrain
{
    private readonly IPersistentState<PccSurveillanceConfigState> _state;

    public PccSurveillanceConfigGrain(
        [PersistentState("pccSurvConfigState", "pccSurvConfigStore")]
        IPersistentState<PccSurveillanceConfigState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ConfigId))
        {
            _state.State.ConfigId = this.GetPrimaryKeyString();
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PccSurveillanceConfigState> GetAsync() => Task.FromResult(_state.State);

    public async Task SaveAsync(
        string conditionName,
        PccEncounterClassification classification,
        List<PccSurveillanceCriterion>? criteria,
        List<PccVisitType>? requiredVisitTypes,
        bool detectComorbidities, bool captureVitals,
        int scanWindowDays,
        List<string>? jurisdictions, string reportingTimeframe,
        bool isActive)
    {
        _state.State.ConditionName = conditionName;
        _state.State.Classification = classification;
        _state.State.Criteria = criteria ?? new();
        _state.State.RequiredVisitTypes = requiredVisitTypes ?? new();
        _state.State.DetectComorbidities = detectComorbidities;
        _state.State.CaptureVitals = captureVitals;
        _state.State.ScanWindowDays = scanWindowDays;
        _state.State.Jurisdictions = jurisdictions ?? new();
        _state.State.ReportingTimeframe = reportingTimeframe;
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCriterionAsync(PccSurveillanceCriterion criterion)
    {
        if (!_state.State.Criteria.Any(c => c.Code == criterion.Code && c.CodeSystem == criterion.CodeSystem))
            _state.State.Criteria.Add(criterion);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
