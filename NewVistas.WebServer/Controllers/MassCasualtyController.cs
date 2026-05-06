// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MassCasualtyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MassCasualtyController> _logger;

    public MassCasualtyController(IGrainFactory grainFactory, ILogger<MassCasualtyController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var sp = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await sp.IsFeatureEnabledAsync("MASS_CASUALTY");
            return Ok(new { Feature = "MASS_CASUALTY", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    // ── Incidents (system-level) ────────────────────────────────

    [HttpPost("incidents")]
    public async Task<IActionResult> ActivateIncident([FromBody] ActivateMciRequest req)
    {
        try
        {
            string id = $"MCI:{Guid.NewGuid()}";
            var grain = _grainFactory.GetGrain<IMassCasualtyIncidentGrain>(id);
            var result = await grain.ActivateAsync(req.IncidentName, req.IncidentType, req.Severity, req.ActivatedByName, req.Description, req.EstimatedCasualties);
            return Created($"api/masscasualty/incidents/{id}", result);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error activating"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("incidents/{incidentId}")]
    public async Task<IActionResult> GetIncident(string incidentId)
    {
        try { return Ok(await _grainFactory.GetGrain<IMassCasualtyIncidentGrain>(incidentId).GetIncidentAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("incidents/{incidentId}/deactivate")]
    public async Task<IActionResult> DeactivateIncident(string incidentId, [FromBody] DeactivateMciRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IMassCasualtyIncidentGrain>(incidentId).DeactivateAsync(req.DeactivatedByName, req.AfterActionNotes);
            return Ok(new { Message = "MCI deactivated." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("incidents/{incidentId}/status-update")]
    public async Task<IActionResult> AddStatusUpdate(string incidentId, [FromBody] MciStatusUpdateRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IMassCasualtyIncidentGrain>(incidentId).AddStatusUpdateAsync(req.Message, req.AuthorName);
            return Ok(new { Message = "Status update added." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("incidents")]
    public async Task<IActionResult> GetIncidents([FromQuery] string? status, [FromQuery] int maxResults = 50)
    {
        try
        {
            var idx = _grainFactory.GetGrain<IMassCasualtyIncidentIndexGrain>("MCI-IDX");
            if (!string.IsNullOrEmpty(status)) return Ok(await idx.GetByStatusAsync(status, maxResults));
            return Ok(await idx.GetAllAsync(maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("incidents/active")]
    public async Task<IActionResult> GetActiveIncidents()
    {
        try { return Ok(await _grainFactory.GetGrain<IMassCasualtyIncidentIndexGrain>("MCI-IDX").GetActiveAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    // ── Casualties ──────────────────────────────────────────────

    [HttpPost("incidents/{incidentId}/casualties")]
    public async Task<IActionResult> RegisterCasualty(string incidentId, [FromBody] RegisterMciCasualtyRequest req)
    {
        try
        {
            string id = $"MCI-CASUALTY:{Guid.NewGuid()}";
            var grain = _grainFactory.GetGrain<IMassCasualtyCasualtyGrain>(id);
            var result = await grain.RegisterCasualtyAsync(incidentId, req.TriageTag, req.TriageCategory, req.PatientId, req.PatientName, req.ChiefInjury, req.ArrivalMode, req.RegisteredByName);
            return Created($"api/masscasualty/casualties/{id}", result);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("incidents/{incidentId}/casualties")]
    public async Task<IActionResult> GetCasualties(string incidentId)
    {
        try { return Ok(await _grainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX").GetByIncidentAsync(incidentId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("casualties/{casualtyId}")]
    public async Task<IActionResult> GetCasualty(string casualtyId)
    {
        try { return Ok(await _grainFactory.GetGrain<IMassCasualtyCasualtyGrain>(casualtyId).GetCasualtyAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("casualties/{casualtyId}/triage")]
    public async Task<IActionResult> UpdateTriage(string casualtyId, [FromBody] UpdateMciTriageRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IMassCasualtyCasualtyGrain>(casualtyId).UpdateTriageCategoryAsync(req.TriageCategory, req.UpdatedByName);
            return Ok(new { Message = "Triage updated." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("casualties/{casualtyId}/assign-area")]
    public async Task<IActionResult> AssignArea(string casualtyId, [FromBody] AssignMciAreaRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IMassCasualtyCasualtyGrain>(casualtyId).AssignToAreaAsync(req.TreatmentArea, req.AssignedByName);
            return Ok(new { Message = "Area assigned." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("casualties/{casualtyId}/disposition")]
    public async Task<IActionResult> UpdateDisposition(string casualtyId, [FromBody] UpdateMciDispositionRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IMassCasualtyCasualtyGrain>(casualtyId).UpdateDispositionAsync(req.Disposition, req.UpdatedByName);
            return Ok(new { Message = "Disposition updated." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("casualties/search")]
    public async Task<IActionResult> SearchCasualties([FromQuery] string? incidentId, [FromQuery] string? triageCategory, [FromQuery] string? disposition, [FromQuery] int maxResults = 100)
    {
        try { return Ok(await _grainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX").SearchAsync(incidentId, triageCategory, disposition, maxResults)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record ActivateMciRequest(string IncidentName, string IncidentType, string Severity, string ActivatedByName, string? Description, int? EstimatedCasualties);
public record DeactivateMciRequest(string DeactivatedByName, string? AfterActionNotes);
public record MciStatusUpdateRequest(string Message, string AuthorName);
public record RegisterMciCasualtyRequest(string TriageTag, string TriageCategory, string? PatientId, string? PatientName, string? ChiefInjury, string? ArrivalMode, string RegisteredByName);
public record UpdateMciTriageRequest(string TriageCategory, string UpdatedByName);
public record AssignMciAreaRequest(string TreatmentArea, string AssignedByName);
public record UpdateMciDispositionRequest(string Disposition, string UpdatedByName);
