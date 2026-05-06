// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class LabSurveillanceTaxonomyGrain : Grain, ILabSurveillanceTaxonomyGrain
{
    private readonly IPersistentState<LabSurveillanceTaxonomyState> _state;

    public LabSurveillanceTaxonomyGrain(
        [PersistentState("labSurvTaxState", "labSurveillanceTaxonomyStore")]
        IPersistentState<LabSurveillanceTaxonomyState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.TaxonomyId))
        {
            _state.State.TaxonomyId = this.GetPrimaryKeyString();
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<LabSurveillanceTaxonomyState> GetAsync() => Task.FromResult(_state.State);

    public async Task SaveAsync(
        string taxonomyName,
        string conditionName,
        string? conditionCode,
        string category,
        List<string>? jurisdictions,
        string reportingTimeframe,
        bool isActive)
    {
        _state.State.TaxonomyName = taxonomyName;
        _state.State.ConditionName = conditionName;
        _state.State.ConditionCode = conditionCode;
        _state.State.Category = category;
        _state.State.Jurisdictions = jurisdictions ?? new();
        _state.State.ReportingTimeframe = reportingTimeframe;
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCodeAsync(LabSurveillanceTaxonomyCode code)
    {
        if (!_state.State.Codes.Any(c => c.Code == code.Code && c.CodeSystem == code.CodeSystem))
            _state.State.Codes.Add(code);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveCodeAsync(string code, string codeSystem)
    {
        _state.State.Codes.RemoveAll(c => c.Code == code && c.CodeSystem == codeSystem);
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
