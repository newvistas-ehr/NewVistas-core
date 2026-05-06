// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Home Telehealth Alert Grain — an alert generated from an out-of-range reading.
/// Based on VistA HOME TELEHEALTH ALERT file (#720.9).
/// Grain key: "HT-ALERT:{guid}"
/// MUMPS routines: HTMONREC.m, HTALERT.m
/// </summary>
public interface IHomeTelehealthAlertGrain : IGrainWithStringKey
{
    /// <summary>Returns the full alert record.</summary>
    Task<HomeTelehealthAlertState> GetAsync();

    /// <summary>
    /// Creates a new alert from an out-of-range reading.
    /// Corresponds to VistA HTALERT CREATE.
    /// </summary>
    Task CreateAsync(
        string alertId,
        string patientId,
        string readingId,
        HtMeasurementType measurementType,
        decimal? value1,
        decimal? value2,
        HtAlertSeverity severity,
        string alertDescription);

    /// <summary>
    /// Acknowledges the alert — clinician has seen it and responded.
    /// Corresponds to VistA HTALERT ACK.
    /// </summary>
    Task AcknowledgeAsync(string clinicianId, string clinicianName, string? clinicalResponse);

    /// <summary>
    /// Resolves the alert — issue has been addressed clinically.
    /// Corresponds to VistA HTALERT RESOLVE.
    /// </summary>
    Task ResolveAsync(string clinicianId, string clinicianName, string? clinicalResponse);

    /// <summary>
    /// Dismisses the alert — determined to be non-actionable.
    /// </summary>
    Task DismissAsync(string clinicianId, string clinicianName, string? clinicalResponse);
}
