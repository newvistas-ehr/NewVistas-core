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
public class PeriodontalChartController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PeriodontalChartController> _logger;

    public PeriodontalChartController(IGrainFactory grainFactory, ILogger<PeriodontalChartController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var sp = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            return Ok(new { Feature = "PERIODONTAL_CHARTING", Enabled = await sp.IsFeatureEnabledAsync("PERIODONTAL_CHARTING") });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts")]
    public async Task<IActionResult> CreateChart(string patientId, [FromBody] CreatePerioChartRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).CreatePeriodontalChartAsync(req.ProviderId, req.ProviderName, req.Notes);
            return Created($"api/periodontalchart/{patientId}/charts/{result.ChartId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/charts")]
    public async Task<IActionResult> GetCharts(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetPeriodontalChartsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/charts/{chartId}")]
    public async Task<IActionResult> GetChart(string patientId, string chartId)
    {
        try { return Ok(await GetWorkflow(patientId).GetPeriodontalChartAsync(chartId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts/{chartId}/tooth/{toothNumber}")]
    public async Task<IActionResult> RecordToothData(string patientId, string chartId, int toothNumber, [FromBody] PeriodontalToothData data)
    {
        try
        {
            await GetWorkflow(patientId).RecordPeriodontalToothDataAsync(chartId, toothNumber, data);
            return Ok(new { Message = $"Tooth {toothNumber} data recorded." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts/{chartId}/teeth")]
    public async Task<IActionResult> RecordMultipleTeeth(string patientId, string chartId, [FromBody] List<PeriodontalToothEntry> entries)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
            await grain.RecordMultipleTeethAsync(entries);
            return Ok(new { Message = $"{entries.Count} teeth recorded." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts/{chartId}/tooth/{toothNumber}/missing")]
    public async Task<IActionResult> MarkMissing(string patientId, string chartId, int toothNumber, [FromBody] MarkToothMissingRequest req)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
            await grain.MarkToothMissingAsync(toothNumber, req.Reason);
            return Ok(new { Message = $"Tooth {toothNumber} marked missing." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts/{chartId}/assessment")]
    public async Task<IActionResult> SetAssessment(string patientId, string chartId, [FromBody] SetPerioAssessmentRequest req)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
            await grain.SetOverallAssessmentAsync(req.Classification, req.TreatmentPlan, req.AssessedByName);
            return Ok(new { Message = "Assessment recorded." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts/{chartId}/finalize")]
    public async Task<IActionResult> Finalize(string patientId, string chartId, [FromBody] PerioActionRequest req)
    {
        try
        {
            await GetWorkflow(patientId).FinalizePeriodontalChartAsync(chartId, req.PerformedByName);
            return Ok(new { Message = "Chart finalized." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/charts/{chartId}/addend")]
    public async Task<IActionResult> Addend(string patientId, string chartId, [FromBody] PerioAddendRequest req)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
            await grain.AddendChartAsync(req.AddendumNote, req.AddendedByName);
            return Ok(new { Message = "Addendum recorded." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? patientId, [FromQuery] string? providerId, [FromQuery] string? status, [FromQuery] int maxResults = 50)
    {
        try { return Ok(await _grainFactory.GetGrain<IPeriodontalChartIndexGrain>("PERIO-IDX").SearchAsync(patientId, providerId, status, maxResults)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record CreatePerioChartRequest(string ProviderId, string ProviderName, string? Notes);
public record MarkToothMissingRequest(string Reason);
public record SetPerioAssessmentRequest(string Classification, string? TreatmentPlan, string AssessedByName);
public record PerioActionRequest(string PerformedByName);
public record PerioAddendRequest(string AddendumNote, string AddendedByName);
