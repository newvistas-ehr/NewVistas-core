// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an appointment wait list entry.
/// Maps to IHS RPMS SD Wait List (File #409.3) for tracking patients
/// waiting for clinic appointment slots with auto-rebooking capability.
/// </summary>
[GenerateSerializer]
public class AppointmentWaitListState
{
    /// <summary>Unique wait list entry ID (grain key, e.g., "SD-WL:{guid}").</summary>
    [Id(0)]
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Patient waiting for an appointment.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Clinic the patient wants an appointment at.</summary>
    [Id(3)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name for display.</summary>
    [Id(4)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Desired appointment type (e.g., FOLLOW-UP, NEW PATIENT, PROCEDURE).</summary>
    [Id(5)]
    public string DesiredAppointmentType { get; set; } = string.Empty;

    /// <summary>Preferred provider ID (optional).</summary>
    [Id(6)]
    public string? PreferredProviderId { get; set; }

    /// <summary>Preferred provider name (optional).</summary>
    [Id(7)]
    public string? PreferredProviderName { get; set; }

    /// <summary>Wait list priority: ROUTINE, URGENT, STAT.</summary>
    [Id(8)]
    public string Priority { get; set; } = "ROUTINE";

    /// <summary>Status: WAITING, OFFERED, BOOKED, DECLINED, CANCELLED, EXPIRED.</summary>
    [Id(9)]
    public string Status { get; set; } = "WAITING";

    /// <summary>Earliest acceptable appointment date.</summary>
    [Id(10)]
    public DateTime? DesiredDateRangeStart { get; set; }

    /// <summary>Latest acceptable appointment date.</summary>
    [Id(11)]
    public DateTime? DesiredDateRangeEnd { get; set; }

    /// <summary>Comments or special requirements.</summary>
    [Id(12)]
    public string? Comments { get; set; }

    /// <summary>Provider who placed the patient on the wait list.</summary>
    [Id(13)]
    public string CreatedByProviderId { get; set; } = string.Empty;

    /// <summary>Name of provider who placed the patient on the wait list.</summary>
    [Id(14)]
    public string CreatedByProviderName { get; set; } = string.Empty;

    /// <summary>When the patient was placed on the wait list.</summary>
    [Id(15)]
    public DateTime WaitListDate { get; set; }

    /// <summary>Appointment ID offered to the patient (auto-rebook).</summary>
    [Id(16)]
    public string? OfferedAppointmentId { get; set; }

    /// <summary>Date/time of the offered appointment slot.</summary>
    [Id(17)]
    public DateTime? OfferedDateTime { get; set; }

    /// <summary>When the slot was offered.</summary>
    [Id(18)]
    public DateTime? OfferDate { get; set; }

    /// <summary>Who offered the slot.</summary>
    [Id(19)]
    public string? OfferedByName { get; set; }

    /// <summary>Appointment ID that was booked from the wait list.</summary>
    [Id(20)]
    public string? BookedAppointmentId { get; set; }

    /// <summary>Date/time the appointment was booked.</summary>
    [Id(21)]
    public DateTime? BookedDateTime { get; set; }

    /// <summary>When the booking was confirmed.</summary>
    [Id(22)]
    public DateTime? BookedDate { get; set; }

    /// <summary>Who confirmed the booking.</summary>
    [Id(23)]
    public string? BookedByName { get; set; }

    /// <summary>Reason for declining an offered slot.</summary>
    [Id(24)]
    public string? DeclineReason { get; set; }

    /// <summary>Reason for cancellation.</summary>
    [Id(25)]
    public string? CancellationReason { get; set; }

    /// <summary>Number of times a slot has been offered to this patient.</summary>
    [Id(26)]
    public int OfferCount { get; set; }

    /// <summary>History of status changes and actions.</summary>
    [Id(27)]
    public List<WaitListAuditEntry> AuditTrail { get; set; } = new();

    [Id(28)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(29)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit trail entry for wait list status changes.
/// </summary>
[GenerateSerializer]
public class WaitListAuditEntry
{
    [Id(0)]
    public DateTime Timestamp { get; set; }

    [Id(1)]
    public string Action { get; set; } = string.Empty;

    [Id(2)]
    public string PerformedByName { get; set; } = string.Empty;

    [Id(3)]
    public string? Details { get; set; }
}

/// <summary>
/// Index entry for the system-level appointment wait list index.
/// </summary>
[GenerateSerializer]
public class AppointmentWaitListIndexEntry
{
    [Id(0)]
    public string EntryId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string ClinicId { get; set; } = string.Empty;

    [Id(4)]
    public string ClinicName { get; set; } = string.Empty;

    [Id(5)]
    public string DesiredAppointmentType { get; set; } = string.Empty;

    [Id(6)]
    public string Priority { get; set; } = string.Empty;

    [Id(7)]
    public string Status { get; set; } = string.Empty;

    [Id(8)]
    public DateTime WaitListDate { get; set; }

    [Id(9)]
    public DateTime? DesiredDateRangeStart { get; set; }

    [Id(10)]
    public DateTime? DesiredDateRangeEnd { get; set; }

    [Id(11)]
    public string? PreferredProviderName { get; set; }

    [Id(12)]
    public int OfferCount { get; set; }
}

/// <summary>
/// Persistent state for the appointment wait list index singleton.
/// </summary>
[GenerateSerializer]
public class AppointmentWaitListIndexState
{
    [Id(0)]
    public Dictionary<string, AppointmentWaitListIndexEntry> Entries { get; set; } = new();
}
