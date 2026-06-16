// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single appointment slot registered in the clinic schedule index.
/// Used for conflict detection and daily capacity tracking.
/// VistA File #44.4 — clinic appointment cross-reference.
/// </summary>
[GenerateSerializer]
public class ClinicScheduleEntry
{
    /// <summary>Appointment IEN</summary>
    [Id(0)]
    public string AppointmentId { get; set; } = string.Empty;

    /// <summary>Patient IEN</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Appointment date and time</summary>
    [Id(2)]
    public DateTime AppointmentDateTime { get; set; }

    /// <summary>Duration in minutes</summary>
    [Id(3)]
    public int DurationMinutes { get; set; } = 30;

    /// <summary>Status: Scheduled, Checked In, Completed, Cancelled, No-Show</summary>
    [Id(4)]
    public string Status { get; set; } = "Scheduled";

    /// <summary>True when booked with the double-book override active</summary>
    [Id(5)]
    public bool IsDoubleBook { get; set; }
}

/// <summary>
/// Clinic schedule index grain state.
/// Keyed as "CLINIC-SCHED:{clinicId}".
/// VistA File #44.4 — clinic appointment slot cross-reference.
/// </summary>
[GenerateSerializer]
public class ScheduleIndexState
{
    /// <summary>Clinic IEN this schedule belongs to</summary>
    [Id(0)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>All appointment slots registered for this clinic</summary>
    [Id(1)]
    public List<ClinicScheduleEntry> Entries { get; set; } = new();
}

/// <summary>
/// An available appointment time slot in a clinic.
/// Returned by IScheduleIndexGrain.GetAvailableSlotsAsync().
/// VistA reference: SDBUILD.m availability grid.
/// </summary>
[GenerateSerializer]
public class AvailableSlot
{
    /// <summary>Start time of the slot.</summary>
    [Id(0)]
    public DateTime StartTime { get; set; }

    /// <summary>End time of the slot.</summary>
    [Id(1)]
    public DateTime EndTime { get; set; }

    /// <summary>Duration of the slot in minutes.</summary>
    [Id(2)]
    public int DurationMinutes { get; set; }

    /// <summary>Whether the slot is available (no conflicting appointments).</summary>
    [Id(3)]
    public bool IsAvailable { get; set; }

    /// <summary>Number of appointments already booked in this slot (for overbooking display).</summary>
    [Id(4)]
    public int BookedCount { get; set; }

    /// <summary>
    /// Scheduling tier: PATIENT (patient self-schedulable), STAFF (staff-only),
    /// or BLOCKED (provider unavailable). Default STAFF for backward compatibility.
    /// VistA File #44.003 (Appointment Type) equivalent.
    /// </summary>
    [Id(5)]
    public string SchedulingTier { get; set; } = "STAFF";

    /// <summary>
    /// Provider ID associated with this slot (null if clinic-level, no specific provider).
    /// </summary>
    [Id(6)]
    public string? ProviderId { get; set; }

    /// <summary>
    /// Provider name for display.
    /// </summary>
    [Id(7)]
    public string? ProviderName { get; set; }
}

/// <summary>
/// Daily capacity summary for a clinic.
/// Returned by IPatientWorkflowGrain.GetClinicDailyCapacityAsync().
/// </summary>
[GenerateSerializer]
public class ClinicDailyCapacity
{
    /// <summary>Clinic ID.</summary>
    [Id(0)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name.</summary>
    [Id(1)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Date for this capacity report.</summary>
    [Id(2)]
    public DateTime Date { get; set; }

    /// <summary>Maximum patients allowed per day.</summary>
    [Id(3)]
    public int MaxPatientsPerDay { get; set; }

    /// <summary>Number of active appointments (Scheduled + Checked In) for the date.</summary>
    [Id(4)]
    public int BookedCount { get; set; }

    /// <summary>Remaining open slots for the date.</summary>
    [Id(5)]
    public int RemainingSlots { get; set; }

    /// <summary>Whether overbooking is allowed for this clinic.</summary>
    [Id(6)]
    public bool AllowOverbooking { get; set; }

    /// <summary>Whether the clinic is at capacity.</summary>
    [Id(7)]
    public bool IsAtCapacity { get; set; }

    /// <summary>Default appointment length for this clinic.</summary>
    [Id(8)]
    public int AppointmentLength { get; set; }

    /// <summary>Available time slots for the date.</summary>
    [Id(9)]
    public List<AvailableSlot> AvailableSlots { get; set; } = new();
}

/// <summary>
/// Result of a patient eligibility check for scheduling.
/// Returned by IPatientWorkflowGrain.CheckPatientEligibilityForSchedulingAsync().
/// VistA reference: DG eligibility checks (DGENELA.m, DGENELB.m).
/// </summary>
[GenerateSerializer]
public class PatientEligibilityResult
{
    /// <summary>Whether the patient is eligible to schedule an appointment.</summary>
    [Id(0)]
    public bool IsEligible { get; set; }

    /// <summary>Reasons why the patient is not eligible (empty if eligible).</summary>
    [Id(1)]
    public List<string> Reasons { get; set; } = new();

    /// <summary>Current enrollment status.</summary>
    [Id(2)]
    public string EnrollmentStatus { get; set; } = string.Empty;

    /// <summary>Priority group (e.g., "1", "2", "5").</summary>
    [Id(3)]
    public string? PriorityGroup { get; set; }

    /// <summary>Whether the patient is copay exempt.</summary>
    [Id(4)]
    public bool CopayExempt { get; set; }

    /// <summary>Copay exemption reason if applicable (SC, MST, CAT, etc.).</summary>
    [Id(5)]
    public string? CopayExemptionReason { get; set; }

    /// <summary>Whether a means test is required.</summary>
    [Id(6)]
    public bool MeansTestRequired { get; set; }

    /// <summary>Whether means test is completed (when required).</summary>
    [Id(7)]
    public bool MeansTestCompleted { get; set; }

    /// <summary>Service connected percentage.</summary>
    [Id(8)]
    public int? ServiceConnectedPercentage { get; set; }

    /// <summary>Primary eligibility code.</summary>
    [Id(9)]
    public string? PrimaryEligibilityCode { get; set; }

    /// <summary>Enrollment effective date.</summary>
    [Id(10)]
    public DateTime? EnrollmentEffectiveDate { get; set; }

    /// <summary>Enrollment termination date (null if still active).</summary>
    [Id(11)]
    public DateTime? EnrollmentTerminationDate { get; set; }

    /// <summary>Whether the enrollment has been terminated.</summary>
    [Id(12)]
    public bool IsTerminated { get; set; }
}

/// <summary>
/// Appointment confirmation/reminder letter content.
/// Generated by IPatientWorkflowGrain.GenerateAppointmentLetterAsync().
/// VistA reference: SD appointment letters.
/// </summary>
[GenerateSerializer]
public class AppointmentLetterContent
{
    [Id(0)] public string AppointmentId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;
    [Id(2)] public string PatientId { get; set; } = string.Empty;
    [Id(3)] public string? StreetAddress1 { get; set; }
    [Id(4)] public string? StreetAddress2 { get; set; }
    [Id(5)] public string? City { get; set; }
    [Id(6)] public string? State { get; set; }
    [Id(7)] public string? ZipCode { get; set; }
    [Id(8)] public string? PhoneNumber { get; set; }
    [Id(9)] public string ClinicName { get; set; } = string.Empty;
    [Id(10)] public string? ClinicPhone { get; set; }
    [Id(11)] public string? ClinicLocation { get; set; }
    [Id(12)] public DateTime AppointmentDateTime { get; set; }
    [Id(13)] public int DurationMinutes { get; set; }
    [Id(14)] public string? ProviderName { get; set; }
    [Id(15)] public string? Purpose { get; set; }
    [Id(16)] public string? AppointmentType { get; set; }
    [Id(17)] public string LetterType { get; set; } = "CONFIRMATION";
    [Id(18)] public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    [Id(19)] public string? Instructions { get; set; }
}

/// <summary>
/// Result of a reminder batch processing run.
/// Returned by IPatientWorkflowGrain.ProcessReminderBatchAsync().
/// VistA reference: SD reminder processing.
/// </summary>
[GenerateSerializer]
public class ReminderBatchResult
{
    /// <summary>Total appointments evaluated.</summary>
    [Id(0)]
    public int TotalEvaluated { get; set; }

    /// <summary>Number of reminders successfully sent.</summary>
    [Id(1)]
    public int RemindersSent { get; set; }

    /// <summary>Number already sent (skipped).</summary>
    [Id(2)]
    public int AlreadySent { get; set; }

    /// <summary>Number skipped (cancelled, past, etc.).</summary>
    [Id(3)]
    public int Skipped { get; set; }

    /// <summary>Details of each reminder processed.</summary>
    [Id(4)]
    public List<ReminderBatchEntry> Entries { get; set; } = new();
}

/// <summary>
/// A single entry in a reminder batch result.
/// </summary>
[GenerateSerializer]
public class ReminderBatchEntry
{
    [Id(0)] public string AppointmentId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string ClinicName { get; set; } = string.Empty;
    [Id(3)] public DateTime AppointmentDateTime { get; set; }
    [Id(4)] public string Status { get; set; } = string.Empty;
    [Id(5)] public bool ReminderSent { get; set; }
    [Id(6)] public string? Reason { get; set; }
}

/// <summary>
/// Result of a cancellation policy check for patient-initiated cancellations.
/// Returned by IPatientWorkflowGrain.PatientCancelAppointmentAsync().
/// </summary>
[GenerateSerializer]
public class CancellationPolicyResult
{
    /// <summary>Whether the cancellation is allowed (always true — soft enforcement).</summary>
    [Id(0)]
    public bool IsAllowed { get; set; }

    /// <summary>Whether the cancellation is within the required notice window.</summary>
    [Id(1)]
    public bool IsWithinNoticeWindow { get; set; }

    /// <summary>Hours remaining until the appointment at time of cancellation.</summary>
    [Id(2)]
    public int HoursUntilAppointment { get; set; }

    /// <summary>Required notice hours from site parameters.</summary>
    [Id(3)]
    public int RequiredNoticeHours { get; set; }

    /// <summary>Policy message displayed to the patient.</summary>
    [Id(4)]
    public string? PolicyMessage { get; set; }

    /// <summary>Whether the cancellation was actually executed.</summary>
    [Id(5)]
    public bool WasCancelled { get; set; }
}
