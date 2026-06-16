// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnesthesiaController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AnesthesiaController> _logger;

    public AnesthesiaController(IGrainFactory grainFactory, ILogger<AnesthesiaController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try { return Ok(new { Feature = "ANESTHESIA_TRACKING", Enabled = await _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT").IsFeatureEnabledAsync("ANESTHESIA_TRACKING") }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records")]
    public async Task<IActionResult> CreateRecord(string patientId, [FromBody] CreateAnesthesiaRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).CreateAnesthesiaRecordAsync(
                req.SurgeryId, req.ProcedureName, req.AnesthesiaType,
                req.AnesthesiologistId, req.AnesthesiologistName,
                req.AsaClassification, req.AirwayClass, req.PreOpNotes);
            return Created($"api/anesthesia/{patientId}/records/{result.RecordId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/records")]
    public async Task<IActionResult> GetRecords(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetAnesthesiaRecordsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/records/{recordId}")]
    public async Task<IActionResult> GetRecord(string patientId, string recordId)
    {
        try { return Ok(await GetWorkflow(patientId).GetAnesthesiaRecordAsync(recordId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/agents")]
    public async Task<IActionResult> AddAgent(string patientId, string recordId, [FromBody] AnesthesiaAgent agent)
    {
        try { await GetWorkflow(patientId).AddAnesthesiaAgentAsync(recordId, agent); return Ok(new { Message = "Agent added." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/vitals")]
    public async Task<IActionResult> RecordVitals(string patientId, string recordId, [FromBody] AnesthesiaVitalEntry vitals)
    {
        try { await _grainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId).RecordVitalsAsync(vitals); return Ok(new { Message = "Vitals recorded." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/events")]
    public async Task<IActionResult> RecordEvent(string patientId, string recordId, [FromBody] RecordAnesthesiaEventRequest req)
    {
        try { await _grainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId).RecordEventAsync(req.EventType, req.Description, req.RecordedByName); return Ok(new { Message = "Event recorded." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/induction")]
    public async Task<IActionResult> RecordInduction(string patientId, string recordId, [FromBody] RecordInductionRequest req)
    {
        try { await _grainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId).RecordInductionAsync(req.InductionTime, req.InductionMethod, req.PerformedByName); return Ok(new { Message = "Induction recorded." }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/emergence")]
    public async Task<IActionResult> RecordEmergence(string patientId, string recordId, [FromBody] RecordEmergenceRequest req)
    {
        try { await _grainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId).RecordEmergenceAsync(req.EmergenceTime, req.EmergenceNotes, req.PerformedByName); return Ok(new { Message = "Emergence recorded." }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/pacu-handoff")]
    public async Task<IActionResult> PacuHandoff(string patientId, string recordId, [FromBody] PacuHandoffRequest req)
    {
        try { await _grainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId).RecordPacuHandoffAsync(req.PacuNurse, req.AldretScore, req.HandoffNotes); return Ok(new { Message = "PACU handoff recorded." }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/finalize")]
    public async Task<IActionResult> Finalize(string patientId, string recordId, [FromBody] AnesthesiaActionRequest req)
    {
        try { await GetWorkflow(patientId).FinalizeAnesthesiaRecordAsync(recordId, req.PerformedByName); return Ok(new { Message = "Record finalized." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/records/{recordId}/addend")]
    public async Task<IActionResult> Addend(string patientId, string recordId, [FromBody] AnesthesiaAddendRequest req)
    {
        try { await _grainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId).AddendRecordAsync(req.AddendumNote, req.AddendedByName); return Ok(new { Message = "Addendum recorded." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? patientId, [FromQuery] string? anesthesiologistId, [FromQuery] string? status, [FromQuery] string? anesthesiaType, [FromQuery] int maxResults = 50)
    {
        try { return Ok(await _grainFactory.GetGrain<IAnesthesiaRecordIndexGrain>("ANES-IDX").SearchAsync(patientId, anesthesiologistId, status, anesthesiaType, maxResults)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record CreateAnesthesiaRequest(string SurgeryId, string ProcedureName, string AnesthesiaType, string AnesthesiologistId, string AnesthesiologistName, string AsaClassification, string? AirwayClass, string? PreOpNotes);
public record RecordAnesthesiaEventRequest(string EventType, string Description, string RecordedByName);
public record RecordInductionRequest(DateTime InductionTime, string InductionMethod, string PerformedByName);
public record RecordEmergenceRequest(DateTime EmergenceTime, string? EmergenceNotes, string PerformedByName);
public record PacuHandoffRequest(string PacuNurse, int AldretScore, string? HandoffNotes);
public record AnesthesiaActionRequest(string PerformedByName);
public record AnesthesiaAddendRequest(string AddendumNote, string AddendedByName);
