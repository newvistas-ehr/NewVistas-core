// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Provider Availability state — weekly recurring patterns, time blocks, and scheduling status.
/// Defines when a provider works at which clinic and manages blocked time periods.
///
/// Key pattern: "PROV-AVAIL:{providerId}"
/// VistA File #44.005 (SD Clinic Availability), File #44.002 (Provider).
/// MUMPS references: SDCOU.m, SDBUILD.m
/// </summary>
[GenerateSerializer]
public class ProviderAvailabilityState
{
    /// <summary>
    /// Provider IEN — unique identifier.
    /// </summary>
    [Id(0)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Provider scheduling status: ACTIVE, ON_LEAVE, UNAVAILABLE.
    /// VistA File #200 field 53.1 (PROVIDER CLASS).
    /// </summary>
    [Id(1)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Reason for current status (e.g., "Medical leave", "Sabbatical").
    /// </summary>
    [Id(2)]
    public string? StatusReason { get; set; }

    /// <summary>
    /// When status was last changed.
    /// </summary>
    [Id(3)]
    public DateTime? StatusChangedDate { get; set; }

    /// <summary>
    /// Who last changed the status.
    /// </summary>
    [Id(4)]
    public string? StatusChangedBy { get; set; }

    /// <summary>
    /// Recurring weekly availability patterns — defines which clinics this provider
    /// works at, on which days, during which hours.
    /// VistA File #44.005 clinic availability sub-file equivalent.
    /// </summary>
    [Id(5)]
    public List<WeeklyAvailabilityPattern> WeeklyPatterns { get; set; } = new();

    /// <summary>
    /// One-off time blocks — vacation, sick leave, lunch, admin time, meetings, training.
    /// Overrides weekly patterns for the blocked period.
    /// VistA File #44.5 (Non-Count Clinic / Clinic Cancel) equivalent.
    /// </summary>
    [Id(6)]
    public List<ProviderTimeBlock> TimeBlocks { get; set; } = new();

    /// <summary>
    /// Per-clinic scheduling tier configuration — controls which slots
    /// are patient-self-schedulable vs staff-only.
    /// Key: clinicId. VistA File #44.003 (Appointment Type) equivalent.
    /// </summary>
    [Id(7)]
    public Dictionary<string, ClinicSchedulingTierConfig> SchedulingTiers { get; set; } = new();

    /// <summary>
    /// Date record created.
    /// </summary>
    [Id(8)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date record last modified.
    /// </summary>
    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A recurring weekly availability pattern for a provider at a specific clinic.
/// E.g., "Dr. Smith works at PRIMARY CARE Mon/Wed/Fri 8:00-12:00".
/// VistA File #44.005 sub-file — clinic availability grid.
/// </summary>
[GenerateSerializer]
public class WeeklyAvailabilityPattern
{
    /// <summary>Unique pattern ID for update/delete operations.</summary>
    [Id(0)]
    public string PatternId { get; set; } = string.Empty;

    /// <summary>Clinic ID where this pattern applies.</summary>
    [Id(1)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name (denormalized for display).</summary>
    [Id(2)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>
    /// Days of week this pattern applies. DayOfWeek enum values (0=Sunday..6=Saturday).
    /// E.g., [Monday, Wednesday, Friday].
    /// </summary>
    [Id(3)]
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    /// <summary>Start hour (0-23). E.g., 8 for 8:00 AM.</summary>
    [Id(4)]
    public int StartHour { get; set; }

    /// <summary>Start minute (0-59). E.g., 30 for 8:30 AM.</summary>
    [Id(5)]
    public int StartMinute { get; set; }

    /// <summary>End hour (0-23). E.g., 17 for 5:00 PM.</summary>
    [Id(6)]
    public int EndHour { get; set; }

    /// <summary>End minute (0-59).</summary>
    [Id(7)]
    public int EndMinute { get; set; }

    /// <summary>Effective start date (null = no start bound / always).</summary>
    [Id(8)]
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>Effective end date (null = no end bound / indefinite).</summary>
    [Id(9)]
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Override appointment length for this pattern (null = use clinic default).</summary>
    [Id(10)]
    public int? AppointmentLengthOverride { get; set; }

    /// <summary>Maximum patients during this window (null = use clinic default).</summary>
    [Id(11)]
    public int? MaxPatientsOverride { get; set; }

    /// <summary>Whether this pattern is currently active.</summary>
    [Id(12)]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A one-off time block that prevents scheduling during a specific period.
/// Used for: vacation, sick leave, lunch, admin time, meetings, training,
/// conference, personal, jury duty, bereavement.
/// VistA File #44.5 — Clinic Cancel / Non-Count equivalent.
/// </summary>
[GenerateSerializer]
public class ProviderTimeBlock
{
    /// <summary>Unique block ID.</summary>
    [Id(0)]
    public string BlockId { get; set; } = string.Empty;

    /// <summary>
    /// Block type: VACATION, SICK_LEAVE, LUNCH, ADMIN_TIME, MEETING,
    /// TRAINING, CONFERENCE, PERSONAL, JURY_DUTY, BEREAVEMENT, OTHER.
    /// </summary>
    [Id(1)]
    public string BlockType { get; set; } = string.Empty;

    /// <summary>Block start date/time.</summary>
    [Id(2)]
    public DateTime StartDateTime { get; set; }

    /// <summary>Block end date/time.</summary>
    [Id(3)]
    public DateTime EndDateTime { get; set; }

    /// <summary>
    /// Which clinic this block applies to. Null = all clinics (provider-wide block).
    /// E.g., a lunch block is null (applies everywhere), while an admin block
    /// might be specific to one clinic.
    /// </summary>
    [Id(4)]
    public string? ClinicId { get; set; }

    /// <summary>Reason or notes for this block.</summary>
    [Id(5)]
    public string? Reason { get; set; }

    /// <summary>Whether the block recurs daily within the date range (e.g., daily lunch).</summary>
    [Id(6)]
    public bool IsRecurringDaily { get; set; }

    /// <summary>For recurring daily blocks: start time of day (hour).</summary>
    [Id(7)]
    public int? RecurringStartHour { get; set; }

    /// <summary>For recurring daily blocks: start minute.</summary>
    [Id(8)]
    public int? RecurringStartMinute { get; set; }

    /// <summary>For recurring daily blocks: end time of day (hour).</summary>
    [Id(9)]
    public int? RecurringEndHour { get; set; }

    /// <summary>For recurring daily blocks: end minute.</summary>
    [Id(10)]
    public int? RecurringEndMinute { get; set; }

    /// <summary>Who created this block.</summary>
    [Id(11)]
    public string? CreatedBy { get; set; }

    /// <summary>Date record created.</summary>
    [Id(12)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A computed availability window — the result of applying weekly patterns minus time blocks.
/// Returned by IProviderAvailabilityGrain.GetEffectiveAvailabilityAsync().
/// </summary>
[GenerateSerializer]
public class AvailabilityWindow
{
    /// <summary>Window start time.</summary>
    [Id(0)]
    public DateTime StartTime { get; set; }

    /// <summary>Window end time.</summary>
    [Id(1)]
    public DateTime EndTime { get; set; }

    /// <summary>Clinic ID.</summary>
    [Id(2)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name (denormalized).</summary>
    [Id(3)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Override appointment length (null = use clinic default).</summary>
    [Id(4)]
    public int? AppointmentLengthOverride { get; set; }

    /// <summary>Override max patients (null = use clinic default).</summary>
    [Id(5)]
    public int? MaxPatientsOverride { get; set; }
}

/// <summary>
/// Summary of a provider's availability at a specific clinic on a date.
/// Used by "find available providers" searches.
/// </summary>
[GenerateSerializer]
public class ProviderClinicAvailabilitySummary
{
    /// <summary>Clinic ID.</summary>
    [Id(0)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name.</summary>
    [Id(1)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Availability windows at this clinic.</summary>
    [Id(2)]
    public List<AvailabilityWindow> Windows { get; set; } = new();

    /// <summary>Total available minutes across all windows.</summary>
    [Id(3)]
    public int TotalAvailableMinutes { get; set; }
}

/// <summary>
/// Scheduling tier configuration for a provider at a specific clinic.
/// Controls which time slots are open for patient self-scheduling vs staff-only.
/// VistA File #44.003 (Appointment Type) — maps access types to slot availability.
/// </summary>
[GenerateSerializer]
public class ClinicSchedulingTierConfig
{
    /// <summary>
    /// Number of slots from the start of each availability window that are
    /// open for patient self-scheduling. E.g., 4 means the first 4 slots
    /// of the morning are patient-schedulable.
    /// </summary>
    [Id(0)]
    public int PatientSchedulableSlotCount { get; set; }

    /// <summary>
    /// Whether patient self-scheduling is enabled at all for this provider/clinic.
    /// </summary>
    [Id(1)]
    public bool PatientSelfSchedulingEnabled { get; set; }

    /// <summary>
    /// Minimum days in advance a patient can self-schedule (e.g., 1 = next day at earliest).
    /// </summary>
    [Id(2)]
    public int MinDaysAheadForPatient { get; set; } = 1;

    /// <summary>
    /// Maximum days in advance a patient can self-schedule (e.g., 90).
    /// </summary>
    [Id(3)]
    public int MaxDaysAheadForPatient { get; set; } = 90;

    /// <summary>
    /// Appointment types patients can self-schedule (e.g., ["REGULAR", "FOLLOW-UP"]).
    /// Null or empty = all types allowed.
    /// </summary>
    [Id(4)]
    public List<string>? AllowedPatientAppointmentTypes { get; set; }
}
