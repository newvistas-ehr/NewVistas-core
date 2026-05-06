// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Volunteer Grain — manages a single volunteer's record in the VA Voluntary Service program.
/// VistA VOLUNTARY SERVICE file (#8810).
/// </summary>
public class VolunteerGrain : Grain, IVolunteerGrain
{
    private readonly IPersistentState<VolunteerState> _state;

    public VolunteerGrain(
        [PersistentState("volunteerState", "vsVolunteerStore")] IPersistentState<VolunteerState> state)
    {
        _state = state;
    }

    public Task<VolunteerState> GetAsync() => Task.FromResult(_state.State);

    public async Task EnrollAsync(
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
        string? notes)
    {
        _state.State.VolunteerId = volunteerId;
        _state.State.FirstName = firstName;
        _state.State.LastName = lastName;
        _state.State.MiddleName = middleName;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.PhoneNumber = phoneNumber;
        _state.State.Email = email;
        _state.State.Address = address;
        _state.State.EmergencyContactName = emergencyContactName;
        _state.State.EmergencyContactPhone = emergencyContactPhone;
        _state.State.EnrollmentDate = enrollmentDate;
        _state.State.Status = VolunteerStatus.Active;
        _state.State.BackgroundCheckStatus = backgroundCheckStatus;
        _state.State.Skills = skills ?? new();
        _state.State.Interests = interests ?? new();
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateProfileAsync(
        string firstName,
        string lastName,
        string? middleName,
        string? phoneNumber,
        string? email,
        string? address,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? notes)
    {
        _state.State.FirstName = firstName;
        _state.State.LastName = lastName;
        _state.State.MiddleName = middleName;
        _state.State.PhoneNumber = phoneNumber;
        _state.State.Email = email;
        _state.State.Address = address;
        _state.State.EmergencyContactName = emergencyContactName;
        _state.State.EmergencyContactPhone = emergencyContactPhone;
        if (notes is not null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(VolunteerStatus status, string? notes)
    {
        _state.State.Status = status;
        if (notes is not null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<string> LogHoursAsync(
        DateTime loggedDate,
        decimal hours,
        VolunteerServiceType serviceType,
        string? assignmentId,
        string? notes)
    {
        string hoursId = $"VS-HOURS:{Guid.NewGuid()}";

        VolunteerHoursRecord entry = new VolunteerHoursRecord
        {
            HoursId = hoursId,
            LoggedDate = loggedDate,
            Hours = hours,
            ServiceType = serviceType,
            AssignmentId = assignmentId,
            Notes = notes,
            CreatedDate = DateTime.UtcNow
        };

        _state.State.HoursLog.Add(entry);
        _state.State.TotalHours += hours;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return hoursId;
    }

    public async Task<string> AddAssignmentAsync(
        VolunteerServiceType serviceType,
        string serviceArea,
        string role,
        DateTime startDate,
        bool isPrimary,
        string? supervisorId,
        string? supervisorName,
        string? notes)
    {
        string assignmentId = $"VS-ASSIGN:{Guid.NewGuid()}";

        // If this is the new primary, demote any existing primary
        if (isPrimary)
        {
            foreach (VolunteerAssignmentRecord existing in _state.State.Assignments)
                existing.IsPrimary = false;
        }

        VolunteerAssignmentRecord assignment = new VolunteerAssignmentRecord
        {
            AssignmentId = assignmentId,
            ServiceType = serviceType,
            ServiceArea = serviceArea,
            Role = role,
            StartDate = startDate,
            IsPrimary = isPrimary,
            IsActive = true,
            SupervisorId = supervisorId,
            SupervisorName = supervisorName,
            Notes = notes,
            CreatedDate = DateTime.UtcNow
        };

        _state.State.Assignments.Add(assignment);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return assignmentId;
    }

    public async Task EndAssignmentAsync(string assignmentId, DateTime endDate, string? notes)
    {
        VolunteerAssignmentRecord? assignment = _state.State.Assignments
            .FirstOrDefault(a => a.AssignmentId == assignmentId);

        if (assignment is null)
            return;

        assignment.EndDate = endDate;
        assignment.IsActive = false;
        if (notes is not null)
            assignment.Notes = notes;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddRecognitionAsync(
        VolunteerRecognitionType recognitionType,
        DateTime awardDate,
        string? awardedBy,
        string? description,
        string? certificateNumber)
    {
        VolunteerRecognitionRecord recognition = new VolunteerRecognitionRecord
        {
            RecognitionId = $"VS-RECOG:{Guid.NewGuid()}",
            RecognitionType = recognitionType,
            AwardDate = awardDate,
            AwardedBy = awardedBy,
            Description = description,
            CertificateNumber = certificateNumber,
            CreatedDate = DateTime.UtcNow
        };

        _state.State.Recognitions.Add(recognition);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateBackgroundCheckAsync(BackgroundCheckStatus status, DateTime? checkDate)
    {
        _state.State.BackgroundCheckStatus = status;
        _state.State.BackgroundCheckDate = checkDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<VolunteerHoursRecord>> GetHoursLogAsync()
        => Task.FromResult(_state.State.HoursLog);

    public Task<List<VolunteerAssignmentRecord>> GetAssignmentsAsync()
        => Task.FromResult(_state.State.Assignments);

    public Task<List<VolunteerRecognitionRecord>> GetRecognitionsAsync()
        => Task.FromResult(_state.State.Recognitions);
}
