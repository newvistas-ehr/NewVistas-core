// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Health Summary Grain — persists a single generated health summary report.
/// </summary>
public class HealthSummaryGrain : Grain, IHealthSummaryGrain
{
    private readonly IPersistentState<HealthSummaryState> _state;

    public HealthSummaryGrain(
        [PersistentState("healthSummaryState", "healthSummaryStore")]
        IPersistentState<HealthSummaryState> state)
    {
        _state = state;
    }

    public Task<HealthSummaryState> GetAsync() => Task.FromResult(_state.State);

    public async Task SaveAsync(HealthSummaryState report)
    {
        _state.State.ReportId = report.ReportId;
        _state.State.PatientId = report.PatientId;
        _state.State.TypeId = report.TypeId;
        _state.State.TypeName = report.TypeName;
        _state.State.GeneratedDate = report.GeneratedDate;
        _state.State.GeneratedById = report.GeneratedById;
        _state.State.GeneratedByName = report.GeneratedByName;
        _state.State.Sections = report.Sections;
        await _state.WriteStateAsync();
    }
}
