// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentWaitListController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AppointmentWaitListController> _logger;

    public AppointmentWaitListController(IGrainFactory grainFactory, ILogger<AppointmentWaitListController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("APPOINTMENT_WAITLIST");
            return Ok(new { Feature = "APPOINTMENT_WAITLIST", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking feature"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/waitlist")]
    public async Task<IActionResult> AddToWaitList(string patientId, [FromBody] AddToWaitListRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).AddToWaitListAsync(
                req.ClinicId, req.ClinicName,
                req.DesiredAppointmentType,
                req.PreferredProviderId, req.PreferredProviderName,
                req.Priority,
                req.DesiredDateRangeStart, req.DesiredDateRangeEnd,
                req.Comments,
                req.CreatedByProviderId, req.CreatedByProviderName);
            return Created($"api/appointmentwaitlist/{patientId}/waitlist/{result.EntryId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error adding to wait list"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/waitlist")]
    public async Task<IActionResult> GetWaitListEntries(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetWaitListEntriesAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting wait list entries"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/waitlist/{entryId}")]
    public async Task<IActionResult> GetWaitListEntry(string patientId, string entryId)
    {
        try { return Ok(await GetWorkflow(patientId).GetWaitListEntryAsync(entryId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting wait list entry"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/waitlist/{entryId}/offer")]
    public async Task<IActionResult> OfferSlot(string patientId, string entryId, [FromBody] OfferWaitListSlotRequest req)
    {
        try
        {
            await GetWorkflow(patientId).OfferWaitListSlotAsync(entryId, req.AppointmentId, req.OfferedDateTime, req.OfferedByName);
            return Ok(new { Message = "Slot offered." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error offering slot"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/waitlist/{entryId}/accept")]
    public async Task<IActionResult> AcceptOffer(string patientId, string entryId, [FromBody] AcceptWaitListOfferRequest req)
    {
        try
        {
            await GetWorkflow(patientId).AcceptWaitListOfferAsync(entryId, req.AcceptedByName);
            return Ok(new { Message = "Offer accepted, appointment booked." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error accepting offer"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/waitlist/{entryId}/decline")]
    public async Task<IActionResult> DeclineOffer(string patientId, string entryId, [FromBody] DeclineWaitListOfferRequest req)
    {
        try
        {
            await GetWorkflow(patientId).DeclineWaitListOfferAsync(entryId, req.Reason, req.DeclinedByName);
            return Ok(new { Message = "Offer declined, patient returned to wait list." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error declining offer"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/waitlist/{entryId}/cancel")]
    public async Task<IActionResult> CancelEntry(string patientId, string entryId, [FromBody] CancelWaitListEntryRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CancelWaitListEntryAsync(entryId, req.Reason, req.CancelledByName);
            return Ok(new { Message = "Wait list entry cancelled." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error cancelling entry"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("clinic/{clinicId}/pending")]
    public async Task<IActionResult> GetPendingByClinic(string clinicId, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");
            return Ok(await index.GetPendingByClinicAsync(clinicId, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting pending wait list"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? clinicId, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");
            return Ok(await index.SearchAsync(clinicId, status, priority, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting dashboard"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record AddToWaitListRequest(
    string ClinicId, string ClinicName,
    string DesiredAppointmentType,
    string? PreferredProviderId, string? PreferredProviderName,
    string Priority,
    DateTime? DesiredDateRangeStart, DateTime? DesiredDateRangeEnd,
    string? Comments,
    string CreatedByProviderId, string CreatedByProviderName);

public record OfferWaitListSlotRequest(string AppointmentId, DateTime OfferedDateTime, string OfferedByName);
public record AcceptWaitListOfferRequest(string AcceptedByName);
public record DeclineWaitListOfferRequest(string Reason, string DeclinedByName);
public record CancelWaitListEntryRequest(string Reason, string CancelledByName);
