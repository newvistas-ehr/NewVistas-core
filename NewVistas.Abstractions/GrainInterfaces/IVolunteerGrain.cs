// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Volunteer Grain — manages a single volunteer's record in the VA Voluntary Service program.
///
/// VistA VOLUNTARY SERVICE file (#8810).
/// MUMPS routines: VSSCD.m (volunteer screening/creation), VSHRPRT.m (hours report),
/// VSRPT.m (recognition print), VSMC.m (volunteer management/coordinator).
///
/// Grain key: "VS-VOLUNTEER:{volunteerId}"
/// </summary>
public interface IVolunteerGrain : IGrainWithStringKey
{
    /// <summary>Returns the full volunteer record.</summary>
    Task<VolunteerState> GetAsync();

    /// <summary>
    /// Enrolls a new volunteer in the Voluntary Service program.
    /// Sets status to Active and initializes the profile.
    /// </summary>
    Task EnrollAsync(
        string volunteerId,
        string firstName,
        string lastName,
        string? middleName,
        DateTime? dateOfBirth,
        string? phoneNumber,
        string? email,
        string? address,
        string? emergencyContactName,
        string? emergencyContactPhone,
        DateTime enrollmentDate,
        BackgroundCheckStatus backgroundCheckStatus,
        List<string>? skills,
        List<string>? interests,
        string? notes);

    /// <summary>Updates the volunteer's personal profile and contact information.</summary>
    Task UpdateProfileAsync(
        string firstName,
        string lastName,
        string? middleName,
        string? phoneNumber,
        string? email,
        string? address,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? notes);

    /// <summary>Updates the volunteer's enrollment status (e.g., Active → Inactive or Withdrawn).</summary>
    Task UpdateStatusAsync(VolunteerStatus status, string? notes);

    /// <summary>
    /// Logs volunteer hours for a given date.
    /// Adds to the running TotalHours total.
    /// Returns the new hours log entry ID.
    /// </summary>
    Task<string> LogHoursAsync(
        DateTime loggedDate,
        decimal hours,
        VolunteerServiceType serviceType,
        string? assignmentId,
        string? notes);

    /// <summary>
    /// Adds a new service assignment for this volunteer.
    /// Returns the new assignment ID.
    /// </summary>
    Task<string> AddAssignmentAsync(
        VolunteerServiceType serviceType,
        string serviceArea,
        string role,
        DateTime startDate,
        bool isPrimary,
        string? supervisorId,
        string? supervisorName,
        string? notes);

    /// <summary>Ends an active service assignment by recording its end date.</summary>
    Task EndAssignmentAsync(string assignmentId, DateTime endDate, string? notes);

    /// <summary>
    /// Records a recognition or award for this volunteer.
    /// </summary>
    Task AddRecognitionAsync(
        VolunteerRecognitionType recognitionType,
        DateTime awardDate,
        string? awardedBy,
        string? description,
        string? certificateNumber);

    /// <summary>Updates the background check status and date for this volunteer.</summary>
    Task UpdateBackgroundCheckAsync(BackgroundCheckStatus status, DateTime? checkDate);

    /// <summary>Returns all hours log entries for this volunteer.</summary>
    Task<List<VolunteerHoursRecord>> GetHoursLogAsync();

    /// <summary>Returns all service assignments for this volunteer.</summary>
    Task<List<VolunteerAssignmentRecord>> GetAssignmentsAsync();

    /// <summary>Returns all recognition and award records for this volunteer.</summary>
    Task<List<VolunteerRecognitionRecord>> GetRecognitionsAsync();
}
