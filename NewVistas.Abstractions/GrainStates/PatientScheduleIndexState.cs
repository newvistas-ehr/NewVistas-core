// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight projection of an appointment for schedule index display.
/// VistA File #44.4 (SCHEDULING) appointment sub-record.
/// </summary>
[GenerateSerializer]
public class AppointmentEntry
{
    /// <summary>Appointment IEN</summary>
    [Id(0)]
    public string AppointmentId { get; set; } = string.Empty;

    /// <summary>Clinic IEN — Reference to HOSPITAL LOCATION file (#44)</summary>
    [Id(1)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name (denormalized)</summary>
    [Id(2)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Appointment date and time</summary>
    [Id(3)]
    public DateTime AppointmentDateTime { get; set; }

    /// <summary>Duration in minutes</summary>
    [Id(4)]
    public int DurationMinutes { get; set; } = 30;

    /// <summary>Status: Scheduled, Checked In, Completed, Cancelled, No-Show</summary>
    [Id(5)]
    public string Status { get; set; } = "Scheduled";

    /// <summary>Provider IEN</summary>
    [Id(6)]
    public string? ProviderId { get; set; }

    /// <summary>Provider name (denormalized)</summary>
    [Id(7)]
    public string? ProviderName { get; set; }

    /// <summary>Appointment purpose / reason for visit</summary>
    [Id(8)]
    public string? Purpose { get; set; }

    /// <summary>Appointment type: REGULAR, FOLLOW-UP, URGENT, etc.</summary>
    [Id(9)]
    public string? AppointmentType { get; set; }

    /// <summary>Date the appointment was created</summary>
    [Id(10)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Patient schedule index grain state.
/// Keyed as "SD-SCHED:{patientId}".
/// VistA File #44.4 — patient appointment list cross-reference.
/// </summary>
[GenerateSerializer]
public class PatientScheduleIndexState
{
    /// <summary>Patient IEN</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>All appointment entries for this patient</summary>
    [Id(1)]
    public List<AppointmentEntry> Appointments { get; set; } = new();
}
