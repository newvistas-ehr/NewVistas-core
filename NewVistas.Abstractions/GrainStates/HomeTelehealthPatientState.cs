// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Enrollment and monitoring state for a Home Telehealth patient.
/// Based on VistA HOME TELEHEALTH PATIENT file (#720).
/// MUMPS routines: TIUHTE.m, HTPATIEN.m, HTMONREC.m
/// </summary>
[GenerateSerializer]
public class HomeTelehealthPatientState
{
    /// <summary>
    /// Patient identifier (.01) — pointer to PATIENT file (#2).
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the patient is actively enrolled in HT program (.02).
    /// </summary>
    [Id(1)]
    public bool IsEnrolled { get; set; }

    /// <summary>
    /// Date patient was enrolled in the HT program (.03).
    /// </summary>
    [Id(2)]
    public DateTime? EnrollmentDate { get; set; }

    /// <summary>
    /// Date patient was disenrolled from the HT program (.04).
    /// </summary>
    [Id(3)]
    public DateTime? DisenrollmentDate { get; set; }

    /// <summary>
    /// Reason for disenrollment (.05).
    /// </summary>
    [Id(4)]
    public string? DisenrollmentReason { get; set; }

    /// <summary>
    /// Care coordinator staff ID (.06) — pointer to NEW PERSON file (#200).
    /// </summary>
    [Id(5)]
    public string? CareCoordinatorId { get; set; }

    /// <summary>
    /// Care coordinator display name (.07).
    /// </summary>
    [Id(6)]
    public string? CareCoordinatorName { get; set; }

    /// <summary>
    /// Primary care provider ID (.08) — pointer to NEW PERSON file (#200).
    /// </summary>
    [Id(7)]
    public string? PrimaryCareProviderId { get; set; }

    /// <summary>
    /// Primary care provider display name (.09).
    /// </summary>
    [Id(8)]
    public string? PrimaryCareProviderName { get; set; }

    /// <summary>
    /// Monitoring protocol / care template assigned (.10).
    /// </summary>
    [Id(9)]
    public HtCareProtocol Protocol { get; set; }

    /// <summary>
    /// Devices currently assigned to this patient (sub-file #720.01).
    /// </summary>
    [Id(10)]
    public List<HtAssignedDevice> AssignedDevices { get; set; } = new();

    /// <summary>
    /// Threshold rules that trigger alerts for out-of-range readings (sub-file #720.02).
    /// </summary>
    [Id(11)]
    public List<HtAlertThreshold> AlertThresholds { get; set; } = new();

    /// <summary>
    /// Free-text clinical notes about the enrollment (.11).
    /// </summary>
    [Id(12)]
    public string? Notes { get; set; }

    /// <summary>
    /// Date the record was first created.
    /// </summary>
    [Id(13)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date the record was last modified.
    /// </summary>
    [Id(14)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Monitoring protocol / care template for a Home Telehealth patient.
/// </summary>
[GenerateSerializer]
public enum HtCareProtocol
{
    Standard = 0,
    Hypertension = 1,
    Diabetes = 2,
    CongestiveHeartFailure = 3,
    COPD = 4,
    PostSurgical = 5,
    Custom = 6
}

/// <summary>
/// Device assigned to a patient for remote monitoring.
/// </summary>
[GenerateSerializer]
public class HtAssignedDevice
{
    /// <summary>Device grain key.</summary>
    [Id(0)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Human-readable device name.</summary>
    [Id(1)]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Type of device (BP monitor, scale, etc.).</summary>
    [Id(2)]
    public HtDeviceType DeviceType { get; set; }

    /// <summary>Date the device was assigned to this patient.</summary>
    [Id(3)]
    public DateTime AssignedDate { get; set; }

    /// <summary>Date the device was returned (null = still assigned).</summary>
    [Id(4)]
    public DateTime? ReturnedDate { get; set; }
}

/// <summary>
/// Alert threshold rule for a specific measurement type.
/// Readings outside LowValue–HighValue trigger an alert.
/// </summary>
[GenerateSerializer]
public class HtAlertThreshold
{
    /// <summary>Measurement type this threshold applies to.</summary>
    [Id(0)]
    public HtMeasurementType MeasurementType { get; set; }

    /// <summary>Low alert threshold for primary value (e.g. systolic BP, weight, glucose).</summary>
    [Id(1)]
    public decimal? LowValue { get; set; }

    /// <summary>High alert threshold for primary value.</summary>
    [Id(2)]
    public decimal? HighValue { get; set; }

    /// <summary>Low alert threshold for secondary value (e.g. diastolic BP).</summary>
    [Id(3)]
    public decimal? LowValue2 { get; set; }

    /// <summary>High alert threshold for secondary value (e.g. diastolic BP).</summary>
    [Id(4)]
    public decimal? HighValue2 { get; set; }
}
