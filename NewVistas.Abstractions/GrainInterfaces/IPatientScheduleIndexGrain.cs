// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient Schedule Index Grain — fast-access list of appointments for a patient.
/// Key pattern: "SD-SCHED:{patientId}".
/// VistA File #44.4 appointment cross-reference (SDAM2.m scheduling index).
/// </summary>
public interface IPatientScheduleIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all appointments sorted by date descending, limited to <paramref name="max"/>.
    /// </summary>
    Task<List<GrainStates.AppointmentEntry>> GetAppointmentsAsync(int max = 50);

    /// <summary>
    /// Returns upcoming appointments (future date, status Scheduled or Checked In),
    /// sorted by date ascending.
    /// </summary>
    Task<List<GrainStates.AppointmentEntry>> GetUpcomingAsync(int max = 20);

    /// <summary>
    /// Adds a new entry or replaces an existing entry with the same AppointmentId.
    /// </summary>
    Task AddOrUpdateAsync(GrainStates.AppointmentEntry entry);
}
