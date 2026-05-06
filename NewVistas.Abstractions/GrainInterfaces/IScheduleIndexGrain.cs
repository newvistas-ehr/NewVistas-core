// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Clinic Schedule Index Grain — per-clinic appointment register.
/// Provides conflict detection (overlap) and daily capacity checks.
///
/// Key pattern: "CLINIC-SCHED:{clinicId}"
/// VistA File #44.4 — clinic appointment slot cross-reference (SDCO.m, SDCOU.m).
/// </summary>
public interface IScheduleIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all active appointment slots for the given date, ordered by time.
    /// Only includes Scheduled and Checked In entries.
    /// </summary>
    Task<List<ClinicScheduleEntry>> GetByDateAsync(DateTime date);

    /// <summary>
    /// Returns the count of active appointments booked on the given date.
    /// Used for daily capacity enforcement (MaxPatientsPerDay).
    /// </summary>
    Task<int> GetCountByDateAsync(DateTime date);

    /// <summary>
    /// Returns true if any active appointment slot overlaps with the proposed time window.
    /// Overlap condition: existingStart &lt; proposedEnd AND existingEnd &gt; proposedStart.
    /// </summary>
    Task<bool> HasOverlapAsync(DateTime proposedStart, int durationMinutes);

    /// <summary>
    /// Adds a new entry or replaces an existing entry with the same AppointmentId.
    /// </summary>
    Task AddOrUpdateAsync(ClinicScheduleEntry entry);

    /// <summary>
    /// Updates the status of an appointment slot by ID.
    /// </summary>
    Task UpdateStatusAsync(string appointmentId, string status);

    /// <summary>
    /// Returns available appointment slots for a given date.
    /// Generates time slots from startHour to endHour using slotDurationMinutes,
    /// then marks each as available or booked based on existing appointments.
    /// VistA reference: SDBUILD.m availability grid.
    /// </summary>
    Task<List<AvailableSlot>> GetAvailableSlotsAsync(
        DateTime date, int startHour, int endHour, int slotDurationMinutes);

    /// <summary>
    /// Returns available appointment slots constrained to provider availability windows.
    /// Generates slots only within the provided windows, applies scheduling tier
    /// labels (PATIENT vs STAFF), and checks against existing bookings.
    /// VistA reference: SDBUILD.m availability grid + SD Clinic Availability (#44.005).
    /// </summary>
    Task<List<AvailableSlot>> GetAvailableSlotsAsync(
        DateTime date,
        int slotDurationMinutes,
        List<AvailabilityWindow> availabilityWindows,
        ClinicSchedulingTierConfig? tierConfig);
}
