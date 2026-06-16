// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Provider Schedule Index state — a provider's appointment schedule.
/// Provides "Today's Schedule" and "Upcoming" views without requiring patient-scoped lookups.
///
/// Key pattern: "PROV-SCHED:{providerId}"
/// </summary>
[GenerateSerializer]
public class ProviderScheduleIndexState
{
    /// <summary>
    /// Provider ID this schedule belongs to.
    /// </summary>
    [Id(0)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// All schedule entries for this provider.
    /// </summary>
    [Id(1)]
    public List<ProviderScheduleEntry> Entries { get; set; } = new();
}

/// <summary>
/// A single appointment entry in a provider's schedule.
/// Contains denormalized patient and clinic info for fast display.
/// </summary>
[GenerateSerializer]
public class ProviderScheduleEntry
{
    /// <summary>
    /// Appointment ID (matches IAppointmentGrain key).
    /// </summary>
    [Id(0)]
    public string AppointmentId { get; set; } = string.Empty;

    /// <summary>
    /// Patient ID for this appointment.
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient display name (denormalized).
    /// </summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Scheduled date and time.
    /// </summary>
    [Id(3)]
    public DateTime AppointmentDateTime { get; set; }

    /// <summary>
    /// Clinic ID where the appointment takes place.
    /// </summary>
    [Id(4)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>
    /// Clinic display name (denormalized).
    /// </summary>
    [Id(5)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>
    /// Duration in minutes.
    /// </summary>
    [Id(6)]
    public int DurationMinutes { get; set; } = 30;

    /// <summary>
    /// Appointment status — Scheduled, Checked In, Checked Out, Completed, Cancelled, No-Show.
    /// </summary>
    [Id(7)]
    public string Status { get; set; } = "Scheduled";

    /// <summary>
    /// Purpose/reason for the appointment.
    /// </summary>
    [Id(8)]
    public string? Purpose { get; set; }

    /// <summary>
    /// Appointment type — REGULAR, FOLLOW-UP, URGENT, CONSULT.
    /// </summary>
    [Id(9)]
    public string? AppointmentType { get; set; }
}
