// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Home Telehealth Patient Grain — enrollment, device assignments, and alert thresholds.
/// Based on VistA HOME TELEHEALTH PATIENT file (#720).
/// Grain key: "HT-PATIENT:{patientId}"
/// MUMPS routines: HTPATIEN.m, HTMONREC.m
/// </summary>
public interface IHomeTelehealthPatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the patient's Home Telehealth enrollment record.</summary>
    Task<HomeTelehealthPatientState> GetAsync();

    /// <summary>
    /// Enrolls a patient in the Home Telehealth program.
    /// Corresponds to VistA HTPATIEN ENROLL.
    /// </summary>
    Task EnrollAsync(
        string patientId,
        string? careCoordinatorId,
        string? careCoordinatorName,
        string? primaryCareProviderId,
        string? primaryCareProviderName,
        HtCareProtocol protocol,
        string? notes);

    /// <summary>
    /// Disenrolls a patient from the Home Telehealth program.
    /// Corresponds to VistA HTPATIEN DISENROLL.
    /// </summary>
    Task DisenrollAsync(string? reason);

    /// <summary>
    /// Assigns a device to this patient.
    /// Corresponds to VistA HTPATIEN ADDDEV.
    /// </summary>
    Task AssignDeviceAsync(string deviceId, string deviceName, HtDeviceType deviceType);

    /// <summary>
    /// Records the return of an assigned device.
    /// Corresponds to VistA HTPATIEN RETDEV.
    /// </summary>
    Task ReturnDeviceAsync(string deviceId);

    /// <summary>
    /// Replaces all alert threshold rules for this patient.
    /// Corresponds to VistA HTMONREC SETTHRESH.
    /// </summary>
    Task SetAlertThresholdsAsync(List<HtAlertThreshold> thresholds);
}
