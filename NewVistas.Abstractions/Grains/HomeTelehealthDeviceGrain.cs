// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Home Telehealth Device Grain — a single remote monitoring device in the inventory.
/// Grain key: "HT-DEVICE:{deviceId}"
/// </summary>
public class HomeTelehealthDeviceGrain : Grain, IHomeTelehealthDeviceGrain
{
    private readonly IPersistentState<HomeTelehealthDeviceState> _state;

    public HomeTelehealthDeviceGrain(
        [PersistentState("htDeviceState", "htDeviceStore")] IPersistentState<HomeTelehealthDeviceState> state)
    {
        _state = state;
    }

    public Task<HomeTelehealthDeviceState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string deviceId,
        string deviceName,
        HtDeviceType deviceType,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? notes)
    {
        _state.State.DeviceId = deviceId;
        _state.State.DeviceName = deviceName;
        _state.State.DeviceType = deviceType;
        _state.State.Manufacturer = manufacturer;
        _state.State.Model = model;
        _state.State.SerialNumber = serialNumber;
        _state.State.Status = HtDeviceStatus.Available;
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignToPatientAsync(string patientId)
    {
        _state.State.Status = HtDeviceStatus.Assigned;
        _state.State.AssignedPatientId = patientId;
        _state.State.AssignedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReturnToInventoryAsync()
    {
        _state.State.Status = HtDeviceStatus.Available;
        _state.State.AssignedPatientId = null;
        _state.State.AssignedDate = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordCalibrationAsync(DateTime calibrationDate, DateTime nextDueDate)
    {
        _state.State.LastCalibrationDate = calibrationDate;
        _state.State.NextCalibrationDue = nextDueDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SendToMaintenanceAsync(string? notes)
    {
        _state.State.Status = HtDeviceStatus.InMaintenance;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RetireAsync(string? notes)
    {
        _state.State.Status = HtDeviceStatus.Retired;
        _state.State.AssignedPatientId = null;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
