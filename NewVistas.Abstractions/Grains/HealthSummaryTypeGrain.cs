// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Health Summary Type Grain — manages a single configurable report template.
/// VistA HEALTH SUMMARY TYPE file (#142).
/// </summary>
public class HealthSummaryTypeGrain : Grain, IHealthSummaryTypeGrain
{
    private readonly IPersistentState<HealthSummaryTypeState> _state;

    public HealthSummaryTypeGrain(
        [PersistentState("healthSummaryTypeState", "healthSummaryTypeStore")]
        IPersistentState<HealthSummaryTypeState> state)
    {
        _state = state;
    }

    public Task<HealthSummaryTypeState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string typeId,
        string name,
        string? description,
        string createdById,
        string createdByName)
    {
        _state.State.TypeId = typeId;
        _state.State.Name = name;
        _state.State.Description = description;
        _state.State.Status = HealthSummaryTypeStatus.Active;
        _state.State.Components = new();
        _state.State.CreatedById = createdById;
        _state.State.CreatedByName = createdByName;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateAsync(string name, string? description)
    {
        _state.State.Name = name;
        _state.State.Description = description;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddOrUpdateComponentAsync(HealthSummaryComponentConfig component)
    {
        // Remove existing config for this component type, then add the new one
        _state.State.Components.RemoveAll(c => c.ComponentType == component.ComponentType);
        _state.State.Components.Add(component);

        // Keep sorted by display order
        _state.State.Components = _state.State.Components
            .OrderBy(c => c.DisplayOrder)
            .ToList();

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveComponentAsync(HealthSummaryComponentType componentType)
    {
        _state.State.Components.RemoveAll(c => c.ComponentType == componentType);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.Status = isActive
            ? HealthSummaryTypeStatus.Active
            : HealthSummaryTypeStatus.Inactive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
