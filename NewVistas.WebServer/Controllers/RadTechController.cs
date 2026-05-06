// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Radiology Technologist workflows — Exam tracking, protocols, worklist, image linking.
/// VistA RA package — RARTE.m, RAORD*.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/rad-tech")]
[Produces("application/json")]
public class RadTechController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<RadTechController> _logger;
    public RadTechController(IGrainFactory grainFactory, ILogger<RadTechController> logger) { _grainFactory = grainFactory; _logger = logger; }
    private IPatientWorkflowGrain W(string pid) => _grainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpPost("{patientId}/exams/{radiologyId}/init")]
    public async Task<IActionResult> InitExam(string patientId, string radiologyId, [FromBody] InitExamRequest? req)
    {
        try { await W(patientId).InitializeRadExamTrackingAsync(radiologyId, req?.ScheduledDateTime, req?.Room); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("{patientId}/exams/{radiologyId}")]
    public async Task<IActionResult> GetExam(string patientId, string radiologyId)
    {
        try { return Ok(await W(patientId).GetRadExamTrackingAsync(radiologyId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/exams/{radiologyId}/protocol")]
    public async Task<IActionResult> AssignProtocol(string patientId, string radiologyId, [FromBody] AssignProtocolRequest req)
    {
        try { await W(patientId).AssignRadProtocolAsync(radiologyId, req.ProtocolId, req.ProtocolName, req.Parameters); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/exams/{radiologyId}/prep")]
    public async Task<IActionResult> MarkPrepped(string patientId, string radiologyId, [FromBody] PrepRequest? req)
    {
        try { await W(patientId).MarkRadPatientPreppedAsync(radiologyId, req?.PrepNotes); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/exams/{radiologyId}/start")]
    public async Task<IActionResult> StartExam(string patientId, string radiologyId)
    {
        try { await W(patientId).StartRadExamAsync(radiologyId); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/exams/{radiologyId}/complete")]
    public async Task<IActionResult> CompleteExam(string patientId, string radiologyId, [FromBody] CompleteExamRequest? req)
    {
        try { await W(patientId).CompleteRadExamAsync(radiologyId, req?.ImageCount, req?.TechNotes); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/exams/{radiologyId}/pacs")]
    public async Task<IActionResult> SendToPacs(string patientId, string radiologyId)
    {
        try { await W(patientId).SendRadImagesToPacsAsync(radiologyId); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/exams/{radiologyId}/link-image")]
    public async Task<IActionResult> LinkImage(string patientId, string radiologyId, [FromBody] LinkImageRequest req)
    {
        try { await W(patientId).LinkImageToRadExamAsync(radiologyId, req.ImageId); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("protocols")]
    public async Task<IActionResult> GetProtocols()
    {
        try { return Ok(await _grainFactory.GetGrain<IRadProtocolIndexGrain>("RAD-PROTOCOL-INDEX").GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("protocols/type/{imagingType}")]
    public async Task<IActionResult> GetProtocolsByType(string imagingType)
    {
        try { return Ok(await _grainFactory.GetGrain<IRadProtocolIndexGrain>("RAD-PROTOCOL-INDEX").GetByImagingTypeAsync(imagingType)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpPost("{patientId}/worklist/{locationId}/refresh")]
    public async Task<IActionResult> RefreshWorklist(string patientId, string locationId)
    {
        try { return Ok(await W(patientId).RefreshRadWorklistAsync(locationId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("worklist/{locationId}")]
    public async Task<IActionResult> GetWorklist(string locationId)
    {
        try { return Ok(await _grainFactory.GetGrain<IRadWorklistGrain>($"RAD-WORKLIST:{locationId}").GetAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    public record InitExamRequest { public DateTime? ScheduledDateTime { get; init; } public string? Room { get; init; } }
    public record AssignProtocolRequest { public string ProtocolId { get; init; } = string.Empty; public string ProtocolName { get; init; } = string.Empty; public string? Parameters { get; init; } }
    public record PrepRequest { public string? PrepNotes { get; init; } }
    public record CompleteExamRequest { public int? ImageCount { get; init; } public string? TechNotes { get; init; } }
    public record LinkImageRequest { public string ImageId { get; init; } = string.Empty; }
}
