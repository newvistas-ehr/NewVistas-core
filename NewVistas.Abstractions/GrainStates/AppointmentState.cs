// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of an Appointment grain based on VistA SDEC APPOINTMENT file (#409.84).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this appointment are persisted atomically with the
/// projection in a single <c>WriteStateAsync</c>.
/// </summary>

[GenerateSerializer]
public class AppointmentState : EventSourcedStateBase
{
    /// <summary>
    /// Appointment Internal Entry Number (IEN)
    /// </summary>
    [Id(0)]
    public string AppointmentId { get; set; } = string.Empty;

    /// <summary>
    /// Patient IEN - Reference to PATIENT file (#2)
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Clinic/Hospital Location IEN - Reference to HOSPITAL LOCATION file (#44)
    /// </summary>
    [Id(2)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>
    /// Clinic Name
    /// </summary>
    [Id(3)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>
    /// Appointment Date and Time
    /// </summary>
    [Id(4)]
    public DateTime AppointmentDateTime { get; set; }

    /// <summary>
    /// Appointment Type
    /// </summary>
    [Id(5)]
    public string? AppointmentType { get; set; }

    /// <summary>
    /// Appointment Status (e.g., Scheduled, Checked In, Completed, Cancelled, No-Show)
    /// </summary>
    [Id(6)]
    public string Status { get; set; } = "Scheduled";

    /// <summary>
    /// Check-in Date/Time
    /// </summary>
    [Id(7)]
    public DateTime? CheckInDateTime { get; set; }

    /// <summary>
    /// Check-out Date/Time
    /// </summary>
    [Id(8)]
    public DateTime? CheckOutDateTime { get; set; }

    /// <summary>
    /// Duration in minutes
    /// </summary>
    [Id(9)]
    public int DurationMinutes { get; set; } = 30;

    /// <summary>
    /// Provider IEN - Reference to NEW PERSON file (#200)
    /// </summary>
    [Id(10)]
    public string? ProviderId { get; set; }

    /// <summary>
    /// Provider Name
    /// </summary>
    [Id(11)]
    public string? ProviderName { get; set; }

    /// <summary>
    /// Appointment Purpose/Reason
    /// </summary>
    [Id(12)]
    public string? Purpose { get; set; }

    /// <summary>
    /// Appointment Notes
    /// </summary>
    [Id(13)]
    public string? Notes { get; set; }

    /// <summary>
    /// Cancellation Reason
    /// </summary>
    [Id(14)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Cancellation Date/Time
    /// </summary>
    [Id(15)]
    public DateTime? CancellationDateTime { get; set; }

    /// <summary>
    /// No-Show flag
    /// </summary>
    [Id(16)]
    public bool IsNoShow { get; set; }

    /// <summary>
    /// Reminder sent flag
    /// </summary>
    [Id(17)]
    public bool ReminderSent { get; set; }

    /// <summary>
    /// Date Record Created
    /// </summary>
    [Id(18)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the appointment
    /// </summary>
    [Id(20)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User who last modified the appointment
    /// </summary>
    [Id(21)]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// True when this appointment was booked with the double-book override active.
    /// </summary>
    [Id(22)]
    public bool IsDoubleBook { get; set; }

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// appointment on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public AppointmentState Clone() => new()
    {
        AppointmentId = AppointmentId,
        PatientId = PatientId,
        ClinicId = ClinicId,
        ClinicName = ClinicName,
        AppointmentDateTime = AppointmentDateTime,
        AppointmentType = AppointmentType,
        Status = Status,
        CheckInDateTime = CheckInDateTime,
        CheckOutDateTime = CheckOutDateTime,
        DurationMinutes = DurationMinutes,
        ProviderId = ProviderId,
        ProviderName = ProviderName,
        Purpose = Purpose,
        Notes = Notes,
        CancellationReason = CancellationReason,
        CancellationDateTime = CancellationDateTime,
        IsNoShow = IsNoShow,
        ReminderSent = ReminderSent,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        CreatedBy = CreatedBy,
        ModifiedBy = ModifiedBy,
        IsDoubleBook = IsDoubleBook
    };
}
