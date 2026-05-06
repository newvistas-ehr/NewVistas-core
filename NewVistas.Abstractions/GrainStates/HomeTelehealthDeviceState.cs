// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Remote patient monitoring device inventory record.
/// Based on VistA HOME TELEHEALTH DEVICE file (#720.7).
/// </summary>
[GenerateSerializer]
public class HomeTelehealthDeviceState
{
    /// <summary>
    /// Unique device identifier (.01).
    /// </summary>
    [Id(0)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable device name (.02) (e.g., "A&amp;D UA-651BLE Blood Pressure Monitor").
    /// </summary>
    [Id(1)]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Type of physiological measurement device (.03).
    /// </summary>
    [Id(2)]
    public HtDeviceType DeviceType { get; set; }

    /// <summary>
    /// Device manufacturer (.04).
    /// </summary>
    [Id(3)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model number (.05).
    /// </summary>
    [Id(4)]
    public string? Model { get; set; }

    /// <summary>
    /// Device serial number (.06).
    /// </summary>
    [Id(5)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Current device availability status (.07).
    /// </summary>
    [Id(6)]
    public HtDeviceStatus Status { get; set; }

    /// <summary>
    /// Patient ID to whom this device is currently assigned (.08).
    /// </summary>
    [Id(7)]
    public string? AssignedPatientId { get; set; }

    /// <summary>
    /// Date this device was assigned to its current patient (.09).
    /// </summary>
    [Id(8)]
    public DateTime? AssignedDate { get; set; }

    /// <summary>
    /// Date of last calibration / maintenance (.10).
    /// </summary>
    [Id(9)]
    public DateTime? LastCalibrationDate { get; set; }

    /// <summary>
    /// Date next calibration is due (.11).
    /// </summary>
    [Id(10)]
    public DateTime? NextCalibrationDue { get; set; }

    /// <summary>
    /// Free-text notes about the device (.12).
    /// </summary>
    [Id(11)]
    public string? Notes { get; set; }

    /// <summary>
    /// Date this record was created.
    /// </summary>
    [Id(12)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date this record was last modified.
    /// </summary>
    [Id(13)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of remote patient monitoring device.
/// </summary>
[GenerateSerializer]
public enum HtDeviceType
{
    BloodPressureMonitor = 0,
    Scale = 1,
    Glucometer = 2,
    PulseOximeter = 3,
    Thermometer = 4,
    PeakFlowMeter = 5,
    ECGMonitor = 6,
    Other = 7
}

/// <summary>
/// Availability status of a Home Telehealth device.
/// </summary>
[GenerateSerializer]
public enum HtDeviceStatus
{
    Available = 0,
    Assigned = 1,
    InMaintenance = 2,
    Retired = 3
}

/// <summary>
/// Summary entry in the system-wide device inventory index.
/// </summary>
[GenerateSerializer]
public class HtDeviceIndexEntry
{
    /// <summary>Device grain key.</summary>
    [Id(0)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Human-readable device name.</summary>
    [Id(1)]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Device type.</summary>
    [Id(2)]
    public HtDeviceType DeviceType { get; set; }

    /// <summary>Serial number.</summary>
    [Id(3)]
    public string? SerialNumber { get; set; }

    /// <summary>Current availability status.</summary>
    [Id(4)]
    public HtDeviceStatus Status { get; set; }

    /// <summary>Patient ID currently assigned, or null if available.</summary>
    [Id(5)]
    public string? AssignedPatientId { get; set; }
}

/// <summary>
/// System-wide device inventory index.
/// Grain key: "HT-DEVICE-IDX" (singleton)
/// </summary>
[GenerateSerializer]
public class HomeTelehealthDeviceIndexState
{
    /// <summary>
    /// All device summaries in the inventory.
    /// </summary>
    [Id(0)]
    public List<HtDeviceIndexEntry> Entries { get; set; } = new();
}
