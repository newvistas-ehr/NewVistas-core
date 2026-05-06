// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Home Telehealth Device Grain — a single remote monitoring device in the inventory.
/// Based on VistA HOME TELEHEALTH DEVICE file (#720.7).
/// Grain key: "HT-DEVICE:{deviceId}"
/// </summary>
public interface IHomeTelehealthDeviceGrain : IGrainWithStringKey
{
    /// <summary>Returns the full device record.</summary>
    Task<HomeTelehealthDeviceState> GetAsync();

    /// <summary>
    /// Creates a new device record in the inventory.
    /// Corresponds to VistA HTDEVICE CREATE.
    /// </summary>
    Task CreateAsync(
        string deviceId,
        string deviceName,
        HtDeviceType deviceType,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? notes);

    /// <summary>
    /// Marks the device as assigned to a patient.
    /// </summary>
    Task AssignToPatientAsync(string patientId);

    /// <summary>
    /// Returns the device to available status.
    /// </summary>
    Task ReturnToInventoryAsync();

    /// <summary>
    /// Updates calibration dates.
    /// </summary>
    Task RecordCalibrationAsync(DateTime calibrationDate, DateTime nextDueDate);

    /// <summary>
    /// Puts the device into maintenance status.
    /// </summary>
    Task SendToMaintenanceAsync(string? notes);

    /// <summary>
    /// Retires a device from service.
    /// </summary>
    Task RetireAsync(string? notes);
}
