// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Clinical Quality Measures (CQM) Controller — eCQM evaluation and QRDA reporting.
///
/// §170.315(c)(1) — Record and export CQM data (QRDA Category I)
/// §170.315(c)(2) — Import and calculate quality measures
/// §170.315(c)(3) — Report quality measures (QRDA Category III)
/// §170.315(c)(4) — Filter by demographics (age, sex, race, ethnicity, payer)
///
/// Maps to VistA: Clinical Reminders (PXRM) + EPRP reporting.
/// </summary>
[ApiController]
[Route("api/cqm")]
[Authorize]
public class CqmController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CqmController> _logger;

    public CqmController(IGrainFactory grainFactory, ILogger<CqmController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── Measure Management ──────────────────────────────────────────────────

    /// <summary>GET api/cqm/measures — List all registered quality measures.</summary>
    [HttpGet("measures")]
    public async Task<IActionResult> GetAllMeasures()
    {
        try
        {
            ICqmMeasureIndexGrain index = _grainFactory.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
            List<CqmMeasureSummary> measures = await index.GetAllMeasuresAsync();
            return Ok(measures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing CQM measures");
            return StatusCode(500, "An error occurred listing quality measures.");
        }
    }

    /// <summary>GET api/cqm/measures/active — List only active measures.</summary>
    [HttpGet("measures/active")]
    public async Task<IActionResult> GetActiveMeasures()
    {
        try
        {
            ICqmMeasureIndexGrain index = _grainFactory.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
            List<CqmMeasureSummary> measures = await index.GetActiveMeasuresAsync();
            return Ok(measures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active CQM measures");
            return StatusCode(500, "An error occurred listing active quality measures.");
        }
    }

    /// <summary>GET api/cqm/measures/{measureId} — Get a full measure definition.</summary>
    [HttpGet("measures/{measureId}")]
    public async Task<IActionResult> GetMeasure(string measureId)
    {
        try
        {
            ICqmMeasureGrain grain = _grainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
            CqmMeasureState measure = await grain.GetMeasureAsync();
            if (string.IsNullOrEmpty(measure.Title))
                return NotFound($"Measure {measureId} not found.");
            return Ok(measure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting measure {MeasureId}", measureId);
            return StatusCode(500, "An error occurred getting the measure.");
        }
    }

    /// <summary>POST api/cqm/measures — Create or update a quality measure definition.</summary>
    [HttpPost("measures")]
    public async Task<IActionResult> SaveMeasure([FromBody] CqmMeasureState measure)
    {
        try
        {
            ICqmMeasureGrain grain = _grainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measure.MeasureId}");
            await grain.SaveMeasureAsync(measure);

            // Update index
            ICqmMeasureIndexGrain index = _grainFactory.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
            await index.AddMeasureAsync(new CqmMeasureSummary
            {
                MeasureId = measure.MeasureId,
                Title = measure.Title,
                ClinicalDomain = measure.ClinicalDomain,
                MeasureType = measure.MeasureType,
                IsActive = measure.IsActive,
                NqfNumber = measure.NqfNumber
            });

            return Created($"api/cqm/measures/{measure.MeasureId}", measure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving measure {MeasureId}", measure.MeasureId);
            return StatusCode(500, "An error occurred saving the measure.");
        }
    }

    /// <summary>PUT api/cqm/measures/{measureId}/active — Activate or deactivate a measure.</summary>
    [HttpPut("measures/{measureId}/active")]
    public async Task<IActionResult> SetMeasureActive(string measureId, [FromBody] bool isActive)
    {
        try
        {
            ICqmMeasureGrain grain = _grainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
            await grain.SetActiveAsync(isActive);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating measure {MeasureId} active status", measureId);
            return StatusCode(500, "An error occurred updating the measure.");
        }
    }

    /// <summary>DELETE api/cqm/measures/{measureId} — Remove a measure from the index.</summary>
    [HttpDelete("measures/{measureId}")]
    public async Task<IActionResult> RemoveMeasure(string measureId)
    {
        try
        {
            ICqmMeasureIndexGrain index = _grainFactory.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
            await index.RemoveMeasureAsync(measureId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing measure {MeasureId}", measureId);
            return StatusCode(500, "An error occurred removing the measure.");
        }
    }

    // ─── Evaluation & Reporting ──────────────────────────────────────────────

    /// <summary>
    /// POST api/cqm/evaluate — Evaluate a measure across a set of patients.
    /// §170.315(c)(2) — Import and calculate.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateMeasure([FromBody] CqmEvaluationRequest request)
    {
        try
        {
            string reportId = $"CQM-REPORT:{Guid.NewGuid():N}";
            ICqmReportGrain grain = _grainFactory.GetGrain<ICqmReportGrain>(reportId);
            await grain.EvaluateAsync(
                request.MeasureId,
                request.PatientIds,
                request.PeriodStart,
                request.PeriodEnd,
                User.Identity?.Name);

            CqmReportState report = await grain.GetReportAsync();
            return Created($"api/cqm/reports/{reportId}", report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating measure {MeasureId}", request.MeasureId);
            return StatusCode(500, "An error occurred evaluating the measure.");
        }
    }

    /// <summary>GET api/cqm/reports/{reportId} — Get evaluation report.</summary>
    [HttpGet("reports/{reportId}")]
    public async Task<IActionResult> GetReport(string reportId)
    {
        try
        {
            ICqmReportGrain grain = _grainFactory.GetGrain<ICqmReportGrain>(reportId);
            CqmReportState report = await grain.GetReportAsync();
            if (report.Status == "pending" && string.IsNullOrEmpty(report.MeasureId))
                return NotFound($"Report {reportId} not found.");
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting report {ReportId}", reportId);
            return StatusCode(500, "An error occurred getting the report.");
        }
    }

    /// <summary>
    /// POST api/cqm/reports/{reportId}/filter — Get filtered report results.
    /// §170.315(c)(4) — Filter by demographics.
    /// </summary>
    [HttpPost("reports/{reportId}/filter")]
    public async Task<IActionResult> GetFilteredReport(string reportId, [FromBody] CqmFilterCriteria filter)
    {
        try
        {
            ICqmReportGrain grain = _grainFactory.GetGrain<ICqmReportGrain>(reportId);
            CqmReportState report = await grain.GetFilteredReportAsync(filter);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering report {ReportId}", reportId);
            return StatusCode(500, "An error occurred filtering the report.");
        }
    }

    // ─── QRDA Exports ────────────────────────────────────────────────────────

    /// <summary>
    /// GET api/cqm/reports/{reportId}/qrda-i/{patientId} — Export QRDA Category I XML.
    /// §170.315(c)(1) — Patient-level quality reporting.
    /// </summary>
    [HttpGet("reports/{reportId}/qrda-i/{patientId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> ExportQrdaCategoryI(string reportId, string patientId)
    {
        try
        {
            ICqmReportGrain grain = _grainFactory.GetGrain<ICqmReportGrain>(reportId);
            string xml = await grain.ExportQrdaCategoryIAsync(patientId);
            return Content(xml, "application/xml");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting QRDA I for {PatientId} in {ReportId}", patientId, reportId);
            return StatusCode(500, "An error occurred exporting QRDA Category I.");
        }
    }

    /// <summary>
    /// GET api/cqm/reports/{reportId}/qrda-iii — Export QRDA Category III XML.
    /// §170.315(c)(3) — Population-level quality reporting.
    /// </summary>
    [HttpGet("reports/{reportId}/qrda-iii")]
    [Produces("application/xml")]
    public async Task<IActionResult> ExportQrdaCategoryIII(string reportId)
    {
        try
        {
            ICqmReportGrain grain = _grainFactory.GetGrain<ICqmReportGrain>(reportId);
            string xml = await grain.ExportQrdaCategoryIIIAsync();
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting QRDA III for {ReportId}", reportId);
            return StatusCode(500, "An error occurred exporting QRDA Category III.");
        }
    }

    /// <summary>
    /// POST api/cqm/reports/{reportId}/qrda-iii/filtered — Export filtered QRDA Category III.
    /// §170.315(c)(4) — Filtered aggregate reporting.
    /// </summary>
    [HttpPost("reports/{reportId}/qrda-iii/filtered")]
    [Produces("application/xml")]
    public async Task<IActionResult> ExportFilteredQrdaCategoryIII(string reportId, [FromBody] CqmFilterCriteria filter)
    {
        try
        {
            ICqmReportGrain grain = _grainFactory.GetGrain<ICqmReportGrain>(reportId);
            string xml = await grain.ExportFilteredQrdaCategoryIIIAsync(filter);
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting filtered QRDA III for {ReportId}", reportId);
            return StatusCode(500, "An error occurred exporting filtered QRDA Category III.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

/// <summary>Request to evaluate a CQM across patients for a reporting period.</summary>
public record CqmEvaluationRequest(
    string MeasureId,
    List<string> PatientIds,
    DateTime PeriodStart,
    DateTime PeriodEnd);
