// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>
/// Volunteer enrollment status.
/// VistA VOLUNTARY SERVICE file (#8810), field STATUS.
/// </summary>
public enum VolunteerStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2,
    Deceased = 3,
    Withdrawn = 4
}

/// <summary>
/// Type of voluntary service performed at the VA facility.
/// VistA VOLUNTARY SERVICE file (#8810), field TYPE OF SERVICE.
/// </summary>
public enum VolunteerServiceType
{
    PatientEscort = 0,
    GiftShop = 1,
    Reading = 2,
    ClericalSupport = 3,
    PetTherapy = 4,
    RespiteCaregiver = 5,
    Chaplaincy = 6,
    FoodService = 7,
    Recreation = 8,
    Transportation = 9,
    Administrative = 10,
    Other = 11
}

/// <summary>
/// Background / suitability check status for the volunteer.
/// VistA VOLUNTARY SERVICE file (#8810), field BACKGROUND CHECK.
/// </summary>
public enum BackgroundCheckStatus
{
    NotRequired = 0,
    Pending = 1,
    Cleared = 2,
    Failed = 3,
    Expired = 4
}

/// <summary>
/// Type of recognition or award for volunteer service milestones.
/// VistA VOLUNTARY SERVICE file (#8810), sub-file RECOGNITION (#8810.02).
/// </summary>
public enum VolunteerRecognitionType
{
    OneHundredHours = 0,
    FiveHundredHours = 1,
    OneThousandHours = 2,
    FiveThousandHours = 3,
    AnnualAward = 4,
    SpecialRecognition = 5
}

// ─── Nested Record Types ───────────────────────────────────────────────────────

/// <summary>
/// A single entry in the volunteer's hours log.
/// VistA VOLUNTARY SERVICE file (#8810), sub-file HOURS (#8810.01).
/// </summary>
[GenerateSerializer]
public class VolunteerHoursRecord
{
    /// <summary>Unique identifier for this hours log entry.</summary>
    [Id(0)]
    public string HoursId { get; set; } = string.Empty;

    /// <summary>Date the volunteer hours were performed.</summary>
    [Id(1)]
    public DateTime LoggedDate { get; set; }

    /// <summary>Number of hours volunteered on this date.</summary>
    [Id(2)]
    public decimal Hours { get; set; }

    /// <summary>Type of service performed during these hours.</summary>
    [Id(3)]
    public VolunteerServiceType ServiceType { get; set; } = VolunteerServiceType.Other;

    /// <summary>Assignment ID associated with these hours (if applicable).</summary>
    [Id(4)]
    public string? AssignmentId { get; set; }

    /// <summary>Optional notes about the hours logged.</summary>
    [Id(5)]
    public string? Notes { get; set; }

    /// <summary>Timestamp when this log entry was created.</summary>
    [Id(6)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single volunteer service assignment to a department or role.
/// VistA VOLUNTARY SERVICE file (#8810), sub-file ASSIGNMENT (#8810.03).
/// </summary>
[GenerateSerializer]
public class VolunteerAssignmentRecord
{
    /// <summary>Unique identifier for this assignment.</summary>
    [Id(0)]
    public string AssignmentId { get; set; } = string.Empty;

    /// <summary>Type of service for this assignment.</summary>
    [Id(1)]
    public VolunteerServiceType ServiceType { get; set; } = VolunteerServiceType.Other;

    /// <summary>Specific service area or department (e.g., "3 West", "Canteen").</summary>
    [Id(2)]
    public string ServiceArea { get; set; } = string.Empty;

    /// <summary>Role title within the assignment (e.g., "Patient Escort Volunteer").</summary>
    [Id(3)]
    public string Role { get; set; } = string.Empty;

    /// <summary>Date the assignment began.</summary>
    [Id(4)]
    public DateTime StartDate { get; set; }

    /// <summary>Date the assignment ended (null if still active).</summary>
    [Id(5)]
    public DateTime? EndDate { get; set; }

    /// <summary>Whether this is the volunteer's primary assignment.</summary>
    [Id(6)]
    public bool IsPrimary { get; set; }

    /// <summary>Whether this assignment is currently active.</summary>
    [Id(7)]
    public bool IsActive { get; set; } = true;

    /// <summary>Supervisor / coordinator identifier for this assignment.</summary>
    [Id(8)]
    public string? SupervisorId { get; set; }

    /// <summary>Supervisor / coordinator display name.</summary>
    [Id(9)]
    public string? SupervisorName { get; set; }

    /// <summary>Optional notes about the assignment.</summary>
    [Id(10)]
    public string? Notes { get; set; }

    /// <summary>Timestamp when this assignment record was created.</summary>
    [Id(11)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A recognition or award for volunteer service milestones.
/// VistA VOLUNTARY SERVICE file (#8810), sub-file RECOGNITION (#8810.02).
/// </summary>
[GenerateSerializer]
public class VolunteerRecognitionRecord
{
    /// <summary>Unique identifier for this recognition record.</summary>
    [Id(0)]
    public string RecognitionId { get; set; } = string.Empty;

    /// <summary>Type of recognition or award.</summary>
    [Id(1)]
    public VolunteerRecognitionType RecognitionType { get; set; } = VolunteerRecognitionType.AnnualAward;

    /// <summary>Date the award was presented.</summary>
    [Id(2)]
    public DateTime AwardDate { get; set; }

    /// <summary>Name of the person or organization presenting the award.</summary>
    [Id(3)]
    public string? AwardedBy { get; set; }

    /// <summary>Description or citation text for the recognition.</summary>
    [Id(4)]
    public string? Description { get; set; }

    /// <summary>Certificate or plaque number (if applicable).</summary>
    [Id(5)]
    public string? CertificateNumber { get; set; }

    /// <summary>Timestamp when this recognition record was created.</summary>
    [Id(6)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

// ─── Index Entry ───────────────────────────────────────────────────────────────

/// <summary>
/// Summary entry for the Voluntary Service index grain.
/// Stored in the singleton <see cref="VolunteerIndexState"/>.
/// </summary>
[GenerateSerializer]
public class VolunteerIndexEntry
{
    /// <summary>Volunteer identifier — matches the grain key suffix.</summary>
    [Id(0)]
    public string VolunteerId { get; set; } = string.Empty;

    /// <summary>Volunteer first name.</summary>
    [Id(1)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Volunteer last name.</summary>
    [Id(2)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Current enrollment status.</summary>
    [Id(3)]
    public VolunteerStatus Status { get; set; } = VolunteerStatus.Active;

    /// <summary>Cumulative volunteer hours to date.</summary>
    [Id(4)]
    public decimal TotalHours { get; set; }

    /// <summary>Primary service type for quick filtering.</summary>
    [Id(5)]
    public VolunteerServiceType? PrimaryServiceType { get; set; }

    /// <summary>Date the volunteer enrolled in the program.</summary>
    [Id(6)]
    public DateTime EnrollmentDate { get; set; }
}

// ─── Main Grain States ─────────────────────────────────────────────────────────

/// <summary>
/// State for the Volunteer grain — the core voluntary service record.
/// VistA VOLUNTARY SERVICE file (#8810).
/// MUMPS routines: VSSCD.m (volunteer screening/create), VSHRPRT.m (hours report),
/// VSRPT.m (recognition print), VSMC.m (volunteer management/coordinator).
/// </summary>
[GenerateSerializer]
public class VolunteerState
{
    /// <summary>Volunteer identifier — the grain key suffix. (#8810 .01 NAME)</summary>
    [Id(0)]
    public string VolunteerId { get; set; } = string.Empty;

    /// <summary>Volunteer first name. (#8810 FIRST NAME)</summary>
    [Id(1)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Volunteer last name. (#8810 LAST NAME)</summary>
    [Id(2)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Volunteer middle name or initial. (#8810 MIDDLE NAME)</summary>
    [Id(3)]
    public string? MiddleName { get; set; }

    /// <summary>Volunteer date of birth. (#8810 DATE OF BIRTH)</summary>
    [Id(4)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Primary phone number. (#8810 PHONE NUMBER)</summary>
    [Id(5)]
    public string? PhoneNumber { get; set; }

    /// <summary>Email address. (#8810 EMAIL)</summary>
    [Id(6)]
    public string? Email { get; set; }

    /// <summary>Mailing or home address. (#8810 ADDRESS)</summary>
    [Id(7)]
    public string? Address { get; set; }

    /// <summary>Emergency contact name. (#8810 EMERGENCY CONTACT)</summary>
    [Id(8)]
    public string? EmergencyContactName { get; set; }

    /// <summary>Emergency contact phone. (#8810 EMERGENCY CONTACT PHONE)</summary>
    [Id(9)]
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Date the volunteer enrolled in the program. (#8810 DATE ENROLLED)</summary>
    [Id(10)]
    public DateTime EnrollmentDate { get; set; }

    /// <summary>Current enrollment status. (#8810 STATUS)</summary>
    [Id(11)]
    public VolunteerStatus Status { get; set; } = VolunteerStatus.Active;

    /// <summary>Background / suitability check status. (#8810 BACKGROUND CHECK)</summary>
    [Id(12)]
    public BackgroundCheckStatus BackgroundCheckStatus { get; set; } = BackgroundCheckStatus.NotRequired;

    /// <summary>Date the background check was completed. (#8810 BACKGROUND CHECK DATE)</summary>
    [Id(13)]
    public DateTime? BackgroundCheckDate { get; set; }

    /// <summary>Skills the volunteer brings to their service. (#8810 SKILLS)</summary>
    [Id(14)]
    public List<string> Skills { get; set; } = new();

    /// <summary>Areas of service interest. (#8810 INTERESTS)</summary>
    [Id(15)]
    public List<string> Interests { get; set; } = new();

    /// <summary>Cumulative hours volunteered to date (derived from HoursLog). (#8810 TOTAL HOURS)</summary>
    [Id(16)]
    public decimal TotalHours { get; set; }

    /// <summary>All hours log entries. (#8810 sub-file HOURS #8810.01)</summary>
    [Id(17)]
    public List<VolunteerHoursRecord> HoursLog { get; set; } = new();

    /// <summary>All service assignments. (#8810 sub-file ASSIGNMENT #8810.03)</summary>
    [Id(18)]
    public List<VolunteerAssignmentRecord> Assignments { get; set; } = new();

    /// <summary>All recognition and award records. (#8810 sub-file RECOGNITION #8810.02)</summary>
    [Id(19)]
    public List<VolunteerRecognitionRecord> Recognitions { get; set; } = new();

    /// <summary>Free-text notes for the volunteer record.</summary>
    [Id(20)]
    public string? Notes { get; set; }

    /// <summary>Date this volunteer record was created.</summary>
    [Id(21)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this volunteer record was last modified.</summary>
    [Id(22)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// State for the Volunteer index grain — singleton listing all enrolled volunteers.
/// </summary>
[GenerateSerializer]
public class VolunteerIndexState
{
    /// <summary>All volunteers in the voluntary service program.</summary>
    [Id(0)]
    public List<VolunteerIndexEntry> Entries { get; set; } = new();
}
