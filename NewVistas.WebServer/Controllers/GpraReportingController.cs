// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for GPRA Population Health Reporting.
/// Aggregates CQM evaluations with fiscal year trending and baseline comparison.
/// RPMS CIMGAGP / BQIGPRA.
/// </summary>
[Authorize]
[ApiController]
[Route("api/gpra")]
public class GpraReportingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<GpraReportingController> _logger;

    public GpraReportingController(IGrainFactory grainFactory, ILogger<GpraReportingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IGpraReportGrain GetReportGrain(string reportId) =>
        _grainFactory.GetGrain<IGpraReportGrain>(reportId);

    private IGpraReportIndexGrain GetIndex() =>
        _grainFactory.GetGrain<IGpraReportIndexGrain>("GPRA-REPORT-IDX");

    // ── Reports ──────────────────────────────────────────────────────────────

    /// <summary>GET api/gpra/reports — List all GPRA reports.</summary>
    [HttpGet("reports")]
    public async Task<IActionResult> GetAllReports()
    {
        try
        {
            List<GpraReportIndexEntry> entries = await GetIndex().GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all GPRA reports");
            return StatusCode(500, "Error retrieving GPRA reports.");
        }
    }

    /// <summary>GET api/gpra/reports/fy/{fiscalYear} — Filter reports by fiscal year.</summary>
    [HttpGet("reports/fy/{fiscalYear:int}")]
    public async Task<IActionResult> GetReportsByFiscalYear(int fiscalYear)
    {
        try
        {
            List<GpraReportIndexEntry> entries = await GetIndex().GetByFiscalYearAsync(fiscalYear);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GPRA reports for FY {FiscalYear}", fiscalYear);
            return StatusCode(500, "Error retrieving GPRA reports.");
        }
    }

    /// <summary>GET api/gpra/reports/{reportId} — Get full report detail.</summary>
    [HttpGet("reports/{reportId}")]
    public async Task<IActionResult> GetReport(string reportId)
    {
        try
        {
            string id = Uri.UnescapeDataString(reportId);
            GpraReportState report = await GetReportGrain(id).GetAsync();
            if (string.IsNullOrEmpty(report.FacilityId))
                return NotFound($"Report {reportId} not found.");
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GPRA report {ReportId}", reportId);
            return StatusCode(500, "Error retrieving GPRA report.");
        }
    }

    /// <summary>POST api/gpra/reports — Create a new GPRA report.</summary>
    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport([FromBody] CreateGpraReportRequest request)
    {
        try
        {
            string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
            await GetReportGrain(reportId).CreateAsync(
                request.FiscalYear,
                request.ReportingPeriod,
                request.CurrentPeriodStart,
                request.CurrentPeriodEnd,
                request.BaselinePeriodStart,
                request.BaselinePeriodEnd,
                request.FacilityId,
                request.FacilityName,
                request.CommunityTaxonomy,
                request.ActiveUserPopulation,
                request.GeneratedById,
                request.GeneratedByName);

            await GetIndex().AddEntryAsync(new GpraReportIndexEntry
            {
                ReportId = reportId,
                FiscalYear = request.FiscalYear,
                ReportingPeriod = request.ReportingPeriod,
                Status = GpraReportStatus.Draft,
                FacilityName = request.FacilityName,
                ActiveUserPopulation = request.ActiveUserPopulation,
                IndicatorCount = 0,
                CreatedDate = DateTime.UtcNow
            });

            return Created($"api/gpra/reports/{Uri.EscapeDataString(reportId)}",
                new GpraResponse { Id = reportId, Message = "GPRA report created." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GPRA report for FY {FiscalYear}", request.FiscalYear);
            return StatusCode(500, "Error creating GPRA report.");
        }
    }

    /// <summary>POST api/gpra/reports/{reportId}/indicators — Add an indicator result.</summary>
    [HttpPost("reports/{reportId}/indicators")]
    public async Task<IActionResult> AddIndicator(string reportId, [FromBody] AddGpraIndicatorRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(reportId);
            GpraIndicatorResult result = new()
            {
                MeasureId = request.MeasureId,
                Title = request.Title,
                Category = request.Category,
                CurrentDenominator = request.CurrentDenominator,
                CurrentNumerator = request.CurrentNumerator,
                CurrentPerformanceRate = request.CurrentPerformanceRate,
                BaselineDenominator = request.BaselineDenominator,
                BaselineNumerator = request.BaselineNumerator,
                BaselinePerformanceRate = request.BaselinePerformanceRate,
                PercentagePointChange = request.PercentagePointChange,
                IsImproved = request.IsImproved,
                TargetRate = request.TargetRate,
                TargetMet = request.TargetMet
            };
            await GetReportGrain(id).AddIndicatorResultAsync(result);
            return Ok(new GpraResponse { Id = id, Message = "Indicator added." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding indicator to GPRA report {ReportId}", reportId);
            return StatusCode(500, "Error adding indicator.");
        }
    }

    /// <summary>POST api/gpra/reports/{reportId}/complete — Mark report as completed.</summary>
    [HttpPost("reports/{reportId}/complete")]
    public async Task<IActionResult> CompleteReport(string reportId)
    {
        try
        {
            string id = Uri.UnescapeDataString(reportId);
            await GetReportGrain(id).CompleteAsync();
            await GetIndex().UpdateStatusAsync(id, GpraReportStatus.Completed);
            return Ok(new GpraResponse { Id = id, Message = "Report completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing GPRA report {ReportId}", reportId);
            return StatusCode(500, "Error completing GPRA report.");
        }
    }

    /// <summary>POST api/gpra/reports/{reportId}/error — Mark report as errored.</summary>
    [HttpPost("reports/{reportId}/error")]
    public async Task<IActionResult> MarkError(string reportId, [FromBody] GpraErrorRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(reportId);
            await GetReportGrain(id).MarkErrorAsync(request.ErrorMessage);
            await GetIndex().UpdateStatusAsync(id, GpraReportStatus.Error);
            return Ok(new GpraResponse { Id = id, Message = "Report marked as errored." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking GPRA report {ReportId} as errored", reportId);
            return StatusCode(500, "Error marking GPRA report as errored.");
        }
    }

    /// <summary>POST api/gpra/reports/{reportId}/cqm-link — Link a CQM report.</summary>
    [HttpPost("reports/{reportId}/cqm-link")]
    public async Task<IActionResult> LinkCqmReport(string reportId, [FromBody] LinkCqmReportRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(reportId);
            await GetReportGrain(id).AddCqmReportLinkAsync(request.CqmReportId);
            return Ok(new GpraResponse { Id = id, Message = "CQM report linked." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking CQM report to GPRA report {ReportId}", reportId);
            return StatusCode(500, "Error linking CQM report.");
        }
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────

public record CreateGpraReportRequest(
    int FiscalYear,
    GpraReportingPeriod ReportingPeriod,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime BaselinePeriodStart,
    DateTime BaselinePeriodEnd,
    string FacilityId,
    string FacilityName,
    string? CommunityTaxonomy,
    int ActiveUserPopulation,
    string? GeneratedById,
    string? GeneratedByName);

public record AddGpraIndicatorRequest(
    string MeasureId,
    string Title,
    GpraClinicalCategory Category,
    int CurrentDenominator,
    int CurrentNumerator,
    decimal CurrentPerformanceRate,
    int BaselineDenominator,
    int BaselineNumerator,
    decimal BaselinePerformanceRate,
    decimal PercentagePointChange,
    bool IsImproved,
    decimal? TargetRate,
    bool TargetMet);

public record GpraErrorRequest(string ErrorMessage);

public record LinkCqmReportRequest(string CqmReportId);

/// <summary>Standard response for GPRA operations.</summary>
public record GpraResponse
{
    public string Id { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
