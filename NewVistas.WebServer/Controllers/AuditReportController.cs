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
/// Audit Report Controller — §170.315(d)(3).
///
/// Implements:
///   - On-demand audit report generation with date range, domain, action, and user filters
///   - Aggregation statistics (by domain, action, user)
///   - Hash-chain integrity verification per §170.315(d)(2)
///   - Report storage and retrieval
///   - Report listing with filtering by patient or type
/// </summary>
[ApiController]
[Route("api/audit/reports")]
[Authorize(Policy = "CanViewAuditTrail")]
public class AuditReportController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AuditReportController> _logger;

    public AuditReportController(IGrainFactory grainFactory, ILogger<AuditReportController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <summary>
    /// POST api/audit/reports/generate — Generate a formal audit report for a patient.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateReport([FromBody] GenerateAuditReportRequest request)
    {
        try
        {
            // Generate the report
            IAuditReportGeneratorGrain generator = _grainFactory.GetGrain<IAuditReportGeneratorGrain>(
                $"AUDIT-REPORT-GEN:{request.PatientId}");
            AuditReportState report = await generator.GenerateReportAsync(
                request.PeriodStart, request.PeriodEnd,
                request.DomainFilter, request.ActionFilter, request.UserIdFilter,
                request.VerifyIntegrity, User.Identity?.Name);

            // Persist the report
            string reportGrainId = $"AUDIT-REPORT:{report.ReportId}";
            IAuditReportGrain reportGrain = _grainFactory.GetGrain<IAuditReportGrain>(reportGrainId);
            await reportGrain.SaveReportAsync(report);

            // Add to index
            IAuditReportIndexGrain index = _grainFactory.GetGrain<IAuditReportIndexGrain>("AUDIT-REPORT-INDEX");
            await index.AddReportAsync(new AuditReportSummary
            {
                ReportId = report.ReportId,
                Title = report.Title,
                ReportType = report.ReportType,
                PatientId = report.PatientId,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                GeneratedDate = report.GeneratedDate,
                TotalEvents = report.TotalEvents,
                IntegrityStatus = report.IntegrityStatus
            });

            return Created($"api/audit/reports/{report.ReportId}", report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audit report for patient {PatientId}", request.PatientId);
            return StatusCode(500, "An error occurred generating the audit report.");
        }
    }

    /// <summary>GET api/audit/reports — List all generated audit reports.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllReports()
    {
        try
        {
            IAuditReportIndexGrain index = _grainFactory.GetGrain<IAuditReportIndexGrain>("AUDIT-REPORT-INDEX");
            List<AuditReportSummary> reports = await index.GetAllReportsAsync();
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing audit reports");
            return StatusCode(500, "An error occurred listing audit reports.");
        }
    }

    /// <summary>GET api/audit/reports/patient/{patientId} — Reports for a specific patient.</summary>
    [HttpGet("patient/{patientId}")]
    public async Task<IActionResult> GetReportsByPatient(string patientId)
    {
        try
        {
            IAuditReportIndexGrain index = _grainFactory.GetGrain<IAuditReportIndexGrain>("AUDIT-REPORT-INDEX");
            List<AuditReportSummary> reports = await index.GetReportsByPatientAsync(patientId);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing reports for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing audit reports.");
        }
    }

    /// <summary>GET api/audit/reports/type/{reportType} — Reports by type.</summary>
    [HttpGet("type/{reportType}")]
    public async Task<IActionResult> GetReportsByType(string reportType)
    {
        try
        {
            IAuditReportIndexGrain index = _grainFactory.GetGrain<IAuditReportIndexGrain>("AUDIT-REPORT-INDEX");
            List<AuditReportSummary> reports = await index.GetReportsByTypeAsync(reportType);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing reports by type {ReportType}", reportType);
            return StatusCode(500, "An error occurred listing audit reports.");
        }
    }

    /// <summary>GET api/audit/reports/{reportId} — Get full report details.</summary>
    [HttpGet("{reportId}")]
    public async Task<IActionResult> GetReport(string reportId)
    {
        try
        {
            IAuditReportGrain grain = _grainFactory.GetGrain<IAuditReportGrain>($"AUDIT-REPORT:{reportId}");
            AuditReportState report = await grain.GetReportAsync();
            if (string.IsNullOrEmpty(report.Title))
                return NotFound($"Report {reportId} not found.");
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting report {ReportId}", reportId);
            return StatusCode(500, "An error occurred getting the audit report.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

/// <summary>Request to generate a formal audit report.</summary>
public record GenerateAuditReportRequest(
    string PatientId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string? DomainFilter = null,
    string? ActionFilter = null,
    string? UserIdFilter = null,
    bool VerifyIntegrity = false);
