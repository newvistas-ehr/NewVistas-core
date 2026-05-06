// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implementation of <see cref="IFacilityGrain"/>.
/// Persists all state for a single facility or equipment record.
/// </summary>
public class FacilityGrain : Grain, IFacilityGrain
{
    private readonly IPersistentState<FacilityState> _state;

    public FacilityGrain(
        [PersistentState("engFacilityState", "engFacilityStore")]
        IPersistentState<FacilityState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.FacilityId))
        {
            _state.State.FacilityId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<FacilityState> GetFacilityAsync()
        => Task.FromResult(_state.State);

    public async Task UpsertAsync(
        string facilityName,
        FacilityCategory category,
        string? building,
        string? floor,
        string? room,
        string? departmentId,
        string? departmentName,
        string? equipmentType,
        string? serialNumber,
        string? model,
        string? manufacturer,
        DateTime? installationDate,
        DateTime? warrantyExpirationDate,
        string? description)
    {
        _state.State.FacilityName = facilityName;
        _state.State.Category = category;
        _state.State.Building = building;
        _state.State.Floor = floor;
        _state.State.Room = room;
        _state.State.DepartmentId = departmentId;
        _state.State.DepartmentName = departmentName;
        _state.State.EquipmentType = equipmentType;
        _state.State.SerialNumber = serialNumber;
        _state.State.Model = model;
        _state.State.Manufacturer = manufacturer;
        _state.State.InstallationDate = installationDate;
        _state.State.WarrantyExpirationDate = warrantyExpirationDate;
        _state.State.Description = description;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task IncrementWorkOrderCountAsync()
    {
        _state.State.WorkOrderCount++;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetUnderMaintenanceAsync()
    {
        _state.State.Status = FacilityStatus.UnderMaintenance;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync()
    {
        _state.State.Status = FacilityStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DecommissionAsync()
    {
        _state.State.Status = FacilityStatus.Decommissioned;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
