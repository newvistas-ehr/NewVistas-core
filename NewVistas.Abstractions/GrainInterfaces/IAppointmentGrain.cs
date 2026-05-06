// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Appointment Grain Interface based on VistA SDEC APPOINTMENT file (#409.84)
/// Represents a single appointment in the system
/// </summary>
public interface IAppointmentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete appointment state
    /// </summary>
    Task<GrainStates.AppointmentState> GetAppointmentAsync();

    /// <summary>
    /// Schedules a new appointment
    /// </summary>
    Task ScheduleAppointmentAsync(
        string patientId,
        string clinicId,
        string clinicName,
        DateTime appointmentDateTime,
        int durationMinutes,
        string? providerId,
        string? providerName,
        string? purpose,
        string? appointmentType,
        string? createdBy,
        bool isDoubleBook = false);

    /// <summary>
    /// Updates appointment details
    /// </summary>
    Task UpdateAppointmentAsync(
        DateTime? appointmentDateTime,
        int? durationMinutes,
        string? providerId,
        string? providerName,
        string? purpose,
        string? notes,
        string? modifiedBy);

    /// <summary>
    /// Checks in a patient for their appointment
    /// </summary>
    Task CheckInAsync(DateTime checkInDateTime);

    /// <summary>
    /// Checks out a patient after their appointment
    /// </summary>
    Task CheckOutAsync(DateTime checkOutDateTime);

    /// <summary>
    /// Cancels an appointment
    /// </summary>
    Task CancelAppointmentAsync(string cancellationReason, string? cancelledBy);

    /// <summary>
    /// Marks appointment as no-show
    /// </summary>
    Task MarkAsNoShowAsync();

    /// <summary>
    /// Marks appointment as completed
    /// </summary>
    Task CompleteAppointmentAsync();

    /// <summary>
    /// Updates appointment status
    /// </summary>
    Task UpdateStatusAsync(string status);

    /// <summary>
    /// Adds or updates appointment notes
    /// </summary>
    Task UpdateNotesAsync(string notes);

    /// <summary>
    /// Marks that a reminder has been sent
    /// </summary>
    Task MarkReminderSentAsync();

    /// <summary>
    /// Gets the appointment date/time
    /// </summary>
    Task<DateTime> GetAppointmentDateTimeAsync();

    /// <summary>
    /// Gets the patient ID for this appointment
    /// </summary>
    Task<string> GetPatientIdAsync();

    /// <summary>
    /// Checks if appointment is in the past
    /// </summary>
    Task<bool> IsPastAppointmentAsync();
}
