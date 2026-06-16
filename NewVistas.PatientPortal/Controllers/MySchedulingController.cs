// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.PatientPortal.Controllers;

/// <summary>
/// Patient Portal Scheduling API — patients can view, schedule, cancel, reschedule
/// appointments and manage waitlist entries.
/// §170.315(e)(1) — View, Download, and Transmit to 3rd Party.
/// </summary>
[ApiController]
[Route("api/my/scheduling")]
[Authorize]
public class MySchedulingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MySchedulingController> _logger;

    public MySchedulingController(IGrainFactory grainFactory, ILogger<MySchedulingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private string GetPatientId()
        => User.FindFirstValue("patient_id")
            ?? throw new InvalidOperationException("patient_id claim not found.");

    private IPatientWorkflowGrain GetWorkflow()
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(GetPatientId());

    // ─── Feature Status ────────────────────────────────────────────────────

    /// <summary>Check whether patient self-scheduling is enabled for this site.</summary>
    [HttpGet("feature-status")]
    public async Task<ActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool selfScheduling = await siteParams.IsFeatureEnabledAsync("PATIENT_SELF_SCHEDULING");
            bool providerAvailability = await siteParams.IsFeatureEnabledAsync("PROVIDER_AVAILABILITY");
            return Ok(new
            {
                PatientSelfScheduling = selfScheduling,
                ProviderAvailability = providerAvailability
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature status");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Appointment Views ��──────────────────────────────────────────────

    /// <summary>Get all appointments with full details.</summary>
    [HttpGet("appointments")]
    public async Task<ActionResult<List<PatientAppointmentDto>>> GetAppointments()
    {
        try
        {
            List<AppointmentState> appointments = await GetWorkflow().GetAppointmentsWithDetailsAsync();
            List<PatientAppointmentDto> dtos = appointments.Select(a => new PatientAppointmentDto(
                a.AppointmentId, a.ClinicId, a.ClinicName, a.AppointmentDateTime,
                a.DurationMinutes, a.Status, a.ProviderName, a.Purpose,
                a.AppointmentType, a.Notes)).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appointments");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get upcoming appointments only.</summary>
    [HttpGet("appointments/upcoming")]
    public async Task<ActionResult> GetUpcomingAppointments()
    {
        try
        {
            List<AppointmentEntry> entries = await GetWorkflow().GetAllAppointmentsAsync();
            List<AppointmentEntry> upcoming = entries
                .Where(e => e.AppointmentDateTime > DateTime.UtcNow
                    && (e.Status == "Scheduled" || e.Status == "Checked In"))
                .OrderBy(e => e.AppointmentDateTime)
                .ToList();
            return Ok(upcoming);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming appointments");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get a single appointment with full details.</summary>
    [HttpGet("appointments/{appointmentId}")]
    public async Task<ActionResult<AppointmentState>> GetAppointment(string appointmentId)
    {
        try
        {
            AppointmentState state = await GetWorkflow().GetAppointmentAsync(appointmentId);
            if (state.PatientId != GetPatientId())
                return Forbid();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Scheduling Eligibility & Clinics ────────────────────────────────

    /// <summary>Check patient scheduling eligibility.</summary>
    [HttpGet("eligibility")]
    public async Task<ActionResult<PatientEligibilityResult>> CheckEligibility()
    {
        try
        {
            PatientEligibilityResult result = await GetWorkflow().CheckPatientEligibilityForSchedulingAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking eligibility");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get clinics that accept patient self-scheduling.</summary>
    [HttpGet("clinics")]
    public async Task<ActionResult<List<PatientClinicDto>>> GetBookableClinics()
    {
        try
        {
            List<ClinicEntry> clinics = await GetWorkflow().GetPatientBookableClinicsAsync();
            List<PatientClinicDto> dtos = clinics.Select(c => new PatientClinicDto(
                c.ClinicId, c.Name, null, null, c.AppointmentLength, c.AcceptsPatientSelfSchedule)).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookable clinics");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get patient-bookable slots for a clinic on a date.</summary>
    [HttpGet("clinics/{clinicId}/slots")]
    public async Task<ActionResult<List<PatientSlotDto>>> GetAvailableSlots(
        string clinicId, [FromQuery] DateTime date)
    {
        try
        {
            List<AvailableSlot> slots = await GetWorkflow().GetPatientBookableSlotsAsync(clinicId, date);
            List<PatientSlotDto> dtos = slots.Select(s => new PatientSlotDto(
                s.StartTime, s.EndTime, s.DurationMinutes, s.IsAvailable)).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available slots for clinic {ClinicId}", clinicId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Self-Scheduling ─────────────────────────────────────────────────

    /// <summary>Self-schedule an appointment.</summary>
    [HttpPost("appointments")]
    public async Task<ActionResult> SelfSchedule([FromBody] PatientScheduleRequest request)
    {
        try
        {
            string appointmentId = await GetWorkflow().PatientSelfScheduleAppointmentAsync(
                request.ClinicId, request.AppointmentDateTime, request.Purpose, request.AppointmentType);
            return Created($"api/my/scheduling/appointments/{appointmentId}", new { appointmentId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Patient scheduling rejected: {Message}", ex.Message);
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during patient self-schedule");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Cancel own appointment.</summary>
    [HttpPut("appointments/{appointmentId}/cancel")]
    public async Task<ActionResult<CancellationPolicyResult>> CancelAppointment(
        string appointmentId, [FromBody] PatientCancelRequest request)
    {
        try
        {
            CancellationPolicyResult result = await GetWorkflow().PatientCancelAppointmentAsync(
                appointmentId, request.Reason);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Reschedule own appointment.</summary>
    [HttpPut("appointments/{appointmentId}/reschedule")]
    public async Task<ActionResult> RescheduleAppointment(
        string appointmentId, [FromBody] PatientRescheduleRequest request)
    {
        try
        {
            await GetWorkflow().PatientRescheduleAppointmentAsync(
                appointmentId, request.NewDateTime, request.Reason);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Waitlist ────────────────────────────────────────────────────────

    /// <summary>Join waitlist for a clinic.</summary>
    [HttpPost("waitlist")]
    public async Task<ActionResult> JoinWaitList([FromBody] PatientWaitlistRequest request)
    {
        try
        {
            AppointmentWaitListState entry = await GetWorkflow().PatientJoinWaitListAsync(
                request.ClinicId, request.DesiredAppointmentType, request.PreferredProviderId,
                request.DesiredDateRangeStart, request.DesiredDateRangeEnd, request.Comments);
            return Created($"api/my/scheduling/waitlist/{entry.EntryId}", entry);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining waitlist");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my waitlist entries.</summary>
    [HttpGet("waitlist")]
    public async Task<ActionResult> GetWaitListEntries()
    {
        try
        {
            List<AppointmentWaitListIndexEntry> entries = await GetWorkflow().GetWaitListEntriesAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting waitlist entries");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get a specific waitlist entry.</summary>
    [HttpGet("waitlist/{entryId}")]
    public async Task<ActionResult> GetWaitListEntry(string entryId)
    {
        try
        {
            AppointmentWaitListState entry = await GetWorkflow().GetWaitListEntryAsync(entryId);
            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting waitlist entry {EntryId}", entryId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Accept an offered waitlist slot.</summary>
    [HttpPut("waitlist/{entryId}/accept")]
    public async Task<ActionResult> AcceptOffer(string entryId)
    {
        try
        {
            string patientId = GetPatientId();
            await GetWorkflow().AcceptWaitListOfferAsync(entryId, $"PATIENT:{patientId}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting waitlist offer {EntryId}", entryId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Decline an offered waitlist slot.</summary>
    [HttpPut("waitlist/{entryId}/decline")]
    public async Task<ActionResult> DeclineOffer(string entryId, [FromBody] PatientDeclineOfferRequest request)
    {
        try
        {
            string patientId = GetPatientId();
            await GetWorkflow().DeclineWaitListOfferAsync(entryId, request.Reason, $"PATIENT:{patientId}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining waitlist offer {EntryId}", entryId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Cancel a waitlist entry.</summary>
    [HttpPut("waitlist/{entryId}/cancel")]
    public async Task<ActionResult> CancelWaitListEntry(string entryId)
    {
        try
        {
            string patientId = GetPatientId();
            await GetWorkflow().CancelWaitListEntryAsync(entryId, "Patient cancelled", $"PATIENT:{patientId}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling waitlist entry {EntryId}", entryId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Request/Response DTOs ───────────────────────────────────────────

    public record PatientScheduleRequest(
        string ClinicId,
        DateTime AppointmentDateTime,
        string? Purpose,
        string? AppointmentType);

    public record PatientCancelRequest(string? Reason);

    public record PatientRescheduleRequest(DateTime NewDateTime, string? Reason);

    public record PatientWaitlistRequest(
        string ClinicId,
        string DesiredAppointmentType,
        string? PreferredProviderId,
        DateTime? DesiredDateRangeStart,
        DateTime? DesiredDateRangeEnd,
        string? Comments);

    public record PatientDeclineOfferRequest(string Reason);

    public record PatientAppointmentDto(
        string AppointmentId,
        string ClinicId,
        string ClinicName,
        DateTime AppointmentDateTime,
        int DurationMinutes,
        string Status,
        string? ProviderName,
        string? Purpose,
        string? AppointmentType,
        string? Notes);

    public record PatientClinicDto(
        string ClinicId,
        string Name,
        string? PhysicalLocation,
        string? PhoneNumber,
        int AppointmentLength,
        bool AcceptsPatientSelfSchedule);

    public record PatientSlotDto(
        DateTime StartTime,
        DateTime EndTime,
        int DurationMinutes,
        bool IsAvailable);
}
