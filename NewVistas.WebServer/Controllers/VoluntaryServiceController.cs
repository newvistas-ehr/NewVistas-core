// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API controller for the VA Voluntary Service program.
/// VistA VOLUNTARY SERVICE file (#8810).
/// </summary>
[Authorize]
[ApiController]
[Route("api/volunteers")]
public class VoluntaryServiceController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<VoluntaryServiceController> _logger;

    private const string IndexKey = "VS-INDEX";

    public VoluntaryServiceController(IGrainFactory grainFactory, ILogger<VoluntaryServiceController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IVolunteerGrain GetVolunteer(string volunteerId)
        => _grainFactory.GetGrain<IVolunteerGrain>($"VS-VOLUNTEER:{volunteerId}");

    private IVolunteerIndexGrain GetIndex()
        => _grainFactory.GetGrain<IVolunteerIndexGrain>(IndexKey);

    // ── Enrollment ────────────────────────────────────────────────────────────

    /// <summary>Enrolls a new volunteer in the Voluntary Service program.</summary>
    [HttpPost]
    public async Task<IActionResult> Enroll([FromBody] EnrollVolunteerRequest req)
    {
        try
        {
            string volunteerId = Guid.NewGuid().ToString();
            IVolunteerGrain grain = GetVolunteer(volunteerId);

            await grain.EnrollAsync(
                volunteerId,
                req.FirstName,
                req.LastName,
                req.MiddleName,
                req.DateOfBirth,
                req.PhoneNumber,
                req.Email,
                req.Address,
                req.EmergencyContactName,
                req.EmergencyContactPhone,
                req.EnrollmentDate,
                req.BackgroundCheckStatus,
                req.Skills,
                req.Interests,
                req.Notes);

            VolunteerState state = await grain.GetAsync();

            await GetIndex().UpsertEntryAsync(BuildIndexEntry(state));

            return Created($"api/volunteers/{volunteerId}", new { volunteerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling volunteer {LastName}", req.LastName);
            return StatusCode(500, "Error enrolling volunteer");
        }
    }

    // ── List / Search ─────────────────────────────────────────────────────────

    /// <summary>Returns all volunteers in the index.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            List<VolunteerIndexEntry> entries = await GetIndex().GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving volunteer list");
            return StatusCode(500, "Error retrieving volunteers");
        }
    }

    /// <summary>Returns volunteers filtered by enrollment status.</summary>
    [HttpGet("by-status/{status}")]
    public async Task<IActionResult> GetByStatus(VolunteerStatus status)
    {
        try
        {
            List<VolunteerIndexEntry> entries = await GetIndex().GetByStatusAsync(status);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving volunteers by status {Status}", status);
            return StatusCode(500, "Error retrieving volunteers");
        }
    }

    /// <summary>Returns volunteers filtered by primary service type.</summary>
    [HttpGet("by-service/{serviceType}")]
    public async Task<IActionResult> GetByServiceType(VolunteerServiceType serviceType)
    {
        try
        {
            List<VolunteerIndexEntry> entries = await GetIndex().GetByServiceTypeAsync(serviceType);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving volunteers by service type {ServiceType}", serviceType);
            return StatusCode(500, "Error retrieving volunteers");
        }
    }

    /// <summary>Searches volunteers by name fragment.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string name)
    {
        try
        {
            List<VolunteerIndexEntry> entries = await GetIndex().SearchAsync(name ?? string.Empty);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching volunteers by name {Name}", name);
            return StatusCode(500, "Error searching volunteers");
        }
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    /// <summary>Returns the full volunteer record for the given volunteer ID.</summary>
    [HttpGet("{volunteerId}")]
    public async Task<IActionResult> GetVolunteerDetail(string volunteerId)
    {
        try
        {
            VolunteerState state = await GetVolunteer(volunteerId).GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error retrieving volunteer");
        }
    }

    // ── Profile Update ────────────────────────────────────────────────────────

    /// <summary>Updates the volunteer's personal profile and contact information.</summary>
    [HttpPut("{volunteerId}/profile")]
    public async Task<IActionResult> UpdateProfile(string volunteerId, [FromBody] UpdateProfileRequest req)
    {
        try
        {
            IVolunteerGrain grain = GetVolunteer(volunteerId);
            await grain.UpdateProfileAsync(
                req.FirstName,
                req.LastName,
                req.MiddleName,
                req.PhoneNumber,
                req.Email,
                req.Address,
                req.EmergencyContactName,
                req.EmergencyContactPhone,
                req.Notes);

            VolunteerState state = await grain.GetAsync();
            await GetIndex().UpsertEntryAsync(BuildIndexEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error updating volunteer profile");
        }
    }

    // ── Status Update ─────────────────────────────────────────────────────────

    /// <summary>Updates the volunteer's enrollment status.</summary>
    [HttpPut("{volunteerId}/status")]
    public async Task<IActionResult> UpdateStatus(string volunteerId, [FromBody] UpdateStatusRequest req)
    {
        try
        {
            IVolunteerGrain grain = GetVolunteer(volunteerId);
            await grain.UpdateStatusAsync(req.Status, req.Notes);

            VolunteerState state = await grain.GetAsync();
            await GetIndex().UpsertEntryAsync(BuildIndexEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error updating volunteer status");
        }
    }

    // ── Hours Logging ─────────────────────────────────────────────────────────

    /// <summary>Logs volunteer hours for the given volunteer.</summary>
    [HttpPost("{volunteerId}/hours")]
    public async Task<IActionResult> LogHours(string volunteerId, [FromBody] LogHoursRequest req)
    {
        try
        {
            IVolunteerGrain grain = GetVolunteer(volunteerId);
            string hoursId = await grain.LogHoursAsync(
                req.LoggedDate,
                req.Hours,
                req.ServiceType,
                req.AssignmentId,
                req.Notes);

            VolunteerState state = await grain.GetAsync();
            await GetIndex().UpsertEntryAsync(BuildIndexEntry(state));
            return Created($"api/volunteers/{volunteerId}/hours/{hoursId}", new { hoursId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging hours for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error logging volunteer hours");
        }
    }

    /// <summary>Returns the complete hours log for the given volunteer.</summary>
    [HttpGet("{volunteerId}/hours")]
    public async Task<IActionResult> GetHoursLog(string volunteerId)
    {
        try
        {
            List<VolunteerHoursRecord> log = await GetVolunteer(volunteerId).GetHoursLogAsync();
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hours log for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error retrieving hours log");
        }
    }

    // ── Assignments ───────────────────────────────────────────────────────────

    /// <summary>Adds a new service assignment for the given volunteer.</summary>
    [HttpPost("{volunteerId}/assignments")]
    public async Task<IActionResult> AddAssignment(string volunteerId, [FromBody] AddAssignmentRequest req)
    {
        try
        {
            string assignmentId = await GetVolunteer(volunteerId).AddAssignmentAsync(
                req.ServiceType,
                req.ServiceArea,
                req.Role,
                req.StartDate,
                req.IsPrimary,
                req.SupervisorId,
                req.SupervisorName,
                req.Notes);

            return Created($"api/volunteers/{volunteerId}/assignments/{assignmentId}", new { assignmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding assignment for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error adding volunteer assignment");
        }
    }

    /// <summary>Ends an active service assignment.</summary>
    [HttpPut("{volunteerId}/assignments/{assignmentId}/end")]
    public async Task<IActionResult> EndAssignment(string volunteerId, string assignmentId, [FromBody] EndAssignmentRequest req)
    {
        try
        {
            await GetVolunteer(volunteerId).EndAssignmentAsync(assignmentId, req.EndDate, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending assignment {AssignmentId} for volunteer {VolunteerId}", assignmentId, volunteerId);
            return StatusCode(500, "Error ending volunteer assignment");
        }
    }

    /// <summary>Returns all service assignments for the given volunteer.</summary>
    [HttpGet("{volunteerId}/assignments")]
    public async Task<IActionResult> GetAssignments(string volunteerId)
    {
        try
        {
            List<VolunteerAssignmentRecord> assignments = await GetVolunteer(volunteerId).GetAssignmentsAsync();
            return Ok(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assignments for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error retrieving assignments");
        }
    }

    // ── Recognition ───────────────────────────────────────────────────────────

    /// <summary>Records a recognition or award for the given volunteer.</summary>
    [HttpPost("{volunteerId}/recognitions")]
    public async Task<IActionResult> AddRecognition(string volunteerId, [FromBody] AddRecognitionRequest req)
    {
        try
        {
            await GetVolunteer(volunteerId).AddRecognitionAsync(
                req.RecognitionType,
                req.AwardDate,
                req.AwardedBy,
                req.Description,
                req.CertificateNumber);

            return Created($"api/volunteers/{volunteerId}/recognitions", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding recognition for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error adding volunteer recognition");
        }
    }

    /// <summary>Returns all recognition and award records for the given volunteer.</summary>
    [HttpGet("{volunteerId}/recognitions")]
    public async Task<IActionResult> GetRecognitions(string volunteerId)
    {
        try
        {
            List<VolunteerRecognitionRecord> recognitions = await GetVolunteer(volunteerId).GetRecognitionsAsync();
            return Ok(recognitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recognitions for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error retrieving recognitions");
        }
    }

    // ── Background Check ──────────────────────────────────────────────────────

    /// <summary>Updates the volunteer's background check status and completion date.</summary>
    [HttpPut("{volunteerId}/background-check")]
    public async Task<IActionResult> UpdateBackgroundCheck(string volunteerId, [FromBody] UpdateBackgroundCheckRequest req)
    {
        try
        {
            await GetVolunteer(volunteerId).UpdateBackgroundCheckAsync(req.Status, req.CheckDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating background check for volunteer {VolunteerId}", volunteerId);
            return StatusCode(500, "Error updating background check");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static VolunteerIndexEntry BuildIndexEntry(VolunteerState state)
    {
        VolunteerAssignmentRecord? primary = state.Assignments.FirstOrDefault(a => a.IsPrimary && a.IsActive)
            ?? state.Assignments.FirstOrDefault(a => a.IsActive);

        return new VolunteerIndexEntry
        {
            VolunteerId = state.VolunteerId,
            FirstName = state.FirstName,
            LastName = state.LastName,
            Status = state.Status,
            TotalHours = state.TotalHours,
            PrimaryServiceType = primary?.ServiceType,
            EnrollmentDate = state.EnrollmentDate
        };
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record EnrollVolunteerRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateTime? DateOfBirth,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateTime EnrollmentDate,
    BackgroundCheckStatus BackgroundCheckStatus,
    List<string>? Skills,
    List<string>? Interests,
    string? Notes);

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Notes);

public record UpdateStatusRequest(
    VolunteerStatus Status,
    string? Notes);

public record LogHoursRequest(
    DateTime LoggedDate,
    decimal Hours,
    VolunteerServiceType ServiceType,
    string? AssignmentId,
    string? Notes);

public record AddAssignmentRequest(
    VolunteerServiceType ServiceType,
    string ServiceArea,
    string Role,
    DateTime StartDate,
    bool IsPrimary,
    string? SupervisorId,
    string? SupervisorName,
    string? Notes);

public record EndAssignmentRequest(
    DateTime EndDate,
    string? Notes);

public record AddRecognitionRequest(
    VolunteerRecognitionType RecognitionType,
    DateTime AwardDate,
    string? AwardedBy,
    string? Description,
    string? CertificateNumber);

public record UpdateBackgroundCheckRequest(
    BackgroundCheckStatus Status,
    DateTime? CheckDate);
