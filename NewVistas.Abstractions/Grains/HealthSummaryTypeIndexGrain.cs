// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Health Summary Type Index Grain — singleton index of all configured report templates.
/// VistA HEALTH SUMMARY TYPE file (#142).
/// </summary>
public class HealthSummaryTypeIndexGrain : Grain, IHealthSummaryTypeIndexGrain
{
    private readonly IPersistentState<HealthSummaryTypeIndexState> _state;

    public HealthSummaryTypeIndexGrain(
        [PersistentState("healthSummaryTypeIndexState", "healthSummaryTypeIndexStore")]
        IPersistentState<HealthSummaryTypeIndexState> state)
    {
        _state = state;
    }

    public Task<List<HealthSummaryTypeIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<HealthSummaryTypeIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == HealthSummaryTypeStatus.Active)
            .ToList());

    public async Task UpsertEntryAsync(HealthSummaryTypeIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.TypeId == entry.TypeId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string typeId)
    {
        _state.State.Entries.RemoveAll(e => e.TypeId == typeId);
        await _state.WriteStateAsync();
    }
}
