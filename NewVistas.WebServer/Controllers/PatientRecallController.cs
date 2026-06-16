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
public class PatientRecallController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PatientRecallController> _logger;

    public PatientRecallController(IGrainFactory grainFactory, ILogger<PatientRecallController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("PATIENT_RECALL");
            return Ok(new { Feature = "PATIENT_RECALL", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking feature"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/recalls")]
    public async Task<IActionResult> CreateRecall(string patientId, [FromBody] CreateRecallRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).CreateRecallEntryAsync(
                req.ClinicId, req.ClinicName,
                req.RecallType, req.RecallDate,
                req.ProviderId, req.ProviderName,
                req.Diagnosis, req.Instructions,
                req.CreatedByProviderId, req.CreatedByProviderName);
            return Created($"api/patientrecall/{patientId}/recalls/{result.EntryId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error creating recall"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/recalls")]
    public async Task<IActionResult> GetRecalls(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetRecallEntriesAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting recalls"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/recalls/{entryId}")]
    public async Task<IActionResult> GetRecall(string patientId, string entryId)
    {
        try { return Ok(await GetWorkflow(patientId).GetRecallEntryAsync(entryId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting recall"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/recalls/{entryId}/letter")]
    public async Task<IActionResult> GenerateLetter(string patientId, string entryId, [FromBody] GenerateRecallLetterRequest req)
    {
        try
        {
            await GetWorkflow(patientId).GenerateRecallLetterAsync(entryId, req.LetterType, req.GeneratedByName);
            return Ok(new { Message = "Letter generated." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error generating letter"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/recalls/{entryId}/contact")]
    public async Task<IActionResult> RecordContact(string patientId, string entryId, [FromBody] RecordRecallContactRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordRecallContactAttemptAsync(entryId, req.ContactMethod, req.Result, req.ContactedByName, req.Notes);
            return Ok(new { Message = "Contact attempt recorded." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error recording contact"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/recalls/{entryId}/schedule")]
    public async Task<IActionResult> ScheduleAppointment(string patientId, string entryId, [FromBody] ScheduleRecallAppointmentRequest req)
    {
        try
        {
            await GetWorkflow(patientId).ScheduleRecallAppointmentAsync(entryId, req.AppointmentId, req.AppointmentDateTime, req.ScheduledByName);
            return Ok(new { Message = "Appointment scheduled from recall." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error scheduling"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/recalls/{entryId}/complete")]
    public async Task<IActionResult> Complete(string patientId, string entryId, [FromBody] CompleteRecallRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CompleteRecallEntryAsync(entryId, req.CompletedByName, req.Notes);
            return Ok(new { Message = "Recall completed." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error completing recall"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/recalls/{entryId}/cancel")]
    public async Task<IActionResult> Cancel(string patientId, string entryId, [FromBody] CancelRecallRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CancelRecallEntryAsync(entryId, req.Reason, req.CancelledByName);
            return Ok(new { Message = "Recall cancelled." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error cancelling recall"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue([FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IPatientRecallIndexGrain>("SD-RECALL-IDX");
            return Ok(await index.GetOverdueAsync(maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting overdue"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("due-in-range")]
    public async Task<IActionResult> GetDueInRange([FromQuery] DateTime rangeStart, [FromQuery] DateTime rangeEnd, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IPatientRecallIndexGrain>("SD-RECALL-IDX");
            return Ok(await index.GetDueInRangeAsync(rangeStart, rangeEnd, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting due recalls"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? clinicId, [FromQuery] string? status, [FromQuery] string? recallType, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IPatientRecallIndexGrain>("SD-RECALL-IDX");
            return Ok(await index.SearchAsync(clinicId, status, recallType, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting dashboard"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record CreateRecallRequest(
    string ClinicId, string ClinicName,
    string RecallType, DateTime RecallDate,
    string? ProviderId, string? ProviderName,
    string? Diagnosis, string? Instructions,
    string CreatedByProviderId, string CreatedByProviderName);

public record GenerateRecallLetterRequest(string LetterType, string GeneratedByName);
public record RecordRecallContactRequest(string ContactMethod, string Result, string ContactedByName, string? Notes);
public record ScheduleRecallAppointmentRequest(string AppointmentId, DateTime AppointmentDateTime, string ScheduledByName);
public record CompleteRecallRequest(string CompletedByName, string? Notes);
public record CancelRecallRequest(string Reason, string CancelledByName);
