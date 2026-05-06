// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// CQM Measure Definition Grain — stores an eCQM specification.
/// Simple CRUD grain for measure metadata and criteria definitions.
/// </summary>
public class CqmMeasureGrain : Grain, ICqmMeasureGrain
{
    private readonly IPersistentState<CqmMeasureState> _state;

    public CqmMeasureGrain(
        [PersistentState("cqmMeasureState", "cqmMeasureStore")] IPersistentState<CqmMeasureState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.MeasureId))
        {
            string key = this.GetPrimaryKeyString();
            // Key format: "CQM:{measureId}"
            int colonIdx = key.IndexOf(':');
            _state.State.MeasureId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SaveMeasureAsync(CqmMeasureState measure)
    {
        _state.State.MeasureId = measure.MeasureId;
        _state.State.Title = measure.Title;
        _state.State.Description = measure.Description;
        _state.State.NqfNumber = measure.NqfNumber;
        _state.State.Version = measure.Version;
        _state.State.Steward = measure.Steward;
        _state.State.MeasureType = measure.MeasureType;
        _state.State.ClinicalDomain = measure.ClinicalDomain;
        _state.State.InitialPopulation = measure.InitialPopulation;
        _state.State.Denominator = measure.Denominator;
        _state.State.DenominatorExclusions = measure.DenominatorExclusions;
        _state.State.Numerator = measure.Numerator;
        _state.State.NumeratorExclusions = measure.NumeratorExclusions;
        _state.State.IsActive = measure.IsActive;
        _state.State.ReportingPrograms = measure.ReportingPrograms;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<CqmMeasureState> GetMeasureAsync() => Task.FromResult(_state.State);

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// CQM Measure Index Grain — listing of all registered measures.
/// </summary>
public class CqmMeasureIndexGrain : Grain, ICqmMeasureIndexGrain
{
    private readonly IPersistentState<CqmMeasureIndexState> _state;

    public CqmMeasureIndexGrain(
        [PersistentState("cqmMeasureIndexState", "cqmMeasureIndexStore")] IPersistentState<CqmMeasureIndexState> state)
    {
        _state = state;
    }

    public async Task AddMeasureAsync(CqmMeasureSummary summary)
    {
        _state.State.Measures.RemoveAll(m => m.MeasureId == summary.MeasureId);
        _state.State.Measures.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveMeasureAsync(string measureId)
    {
        _state.State.Measures.RemoveAll(m => m.MeasureId == measureId);
        await _state.WriteStateAsync();
    }

    public Task<List<CqmMeasureSummary>> GetAllMeasuresAsync()
        => Task.FromResult(_state.State.Measures.ToList());

    public Task<List<CqmMeasureSummary>> GetActiveMeasuresAsync()
        => Task.FromResult(_state.State.Measures.Where(m => m.IsActive).ToList());
}
