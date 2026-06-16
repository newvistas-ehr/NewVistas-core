// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Cancer Registry Controller — NAACCR abstract generation and submission tracking.
/// §170.315(f)(4) — Transmission to cancer registries.
///
/// Provides endpoints for generating NAACCR cancer case abstracts from oncology
/// tumor/treatment data and tracking submission to state/central cancer registries.
/// </summary>
[ApiController]
[Route("api/cancerregistry")]
public class CancerRegistryController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CancerRegistryController> _logger;

    public CancerRegistryController(IGrainFactory grainFactory, ILogger<CancerRegistryController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private ICancerRegistryReportIndexGrain GetIndex()
        => _grainFactory.GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

    // ─── Report Generation ───────────────────────────────────────────────

    /// <summary>
    /// Generate a NAACCR cancer registry abstract for a specific tumor record.
    /// Extracts demographics, tumor, staging, and treatment data into a structured abstract.
    /// </summary>
    [HttpPost("generate/{patientId}")]
    public async Task<IActionResult> GenerateReport(string patientId, [FromBody] GenerateCancerReportRequest request)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string reportId = await GetWorkflow(decodedId).GenerateCancerRegistryReportAsync(
                request.TumorId, request.ReportingFacility, request.RegistrarId, request.RegistrarName);
            return Created($"api/cancerregistry/report/{Uri.EscapeDataString(decodedId)}/{Uri.EscapeDataString(reportId)}",
                new { ReportId = reportId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cancer registry report for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while generating the cancer registry report.");
        }
    }

    /// <summary>Get a cancer registry report by ID.</summary>
    [HttpGet("report/{patientId}/{reportId}")]
    public async Task<IActionResult> GetReport(string patientId, string reportId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string decodedReportId = Uri.UnescapeDataString(reportId);
            CancerRegistryReportState state = await GetWorkflow(decodedId)
                .GetCancerRegistryReportAsync(decodedReportId);
            return Ok(new CancerRegistryReportResponse(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cancer registry report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while retrieving the cancer registry report.");
        }
    }

    /// <summary>Get the NAACCR abstract content (pipe-delimited flat file).</summary>
    [HttpGet("naaccr/{patientId}/{reportId}")]
    public async Task<IActionResult> GetNaaccrAbstract(string patientId, string reportId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string decodedReportId = Uri.UnescapeDataString(reportId);
            string content = await GetWorkflow(decodedId)
                .GetCancerRegistryNaaccrAbstractAsync(decodedReportId);
            return Ok(new { ReportId = decodedReportId, NaaccrContent = content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving NAACCR abstract for report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while retrieving the NAACCR abstract.");
        }
    }

    // ─── Report Submission ───────────────────────────────────────────────

    /// <summary>Submit a cancer registry report to a named registry.</summary>
    [HttpPost("submit/{patientId}/{reportId}")]
    public async Task<IActionResult> SubmitReport(string patientId, string reportId,
        [FromBody] SubmitCancerReportRequest request)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string decodedReportId = Uri.UnescapeDataString(reportId);
            await GetWorkflow(decodedId).SubmitCancerRegistryReportAsync(
                decodedReportId, request.RegistryName, request.ConfirmationNumber);
            return Ok(new { ReportId = decodedReportId, Status = "Submitted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting cancer registry report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while submitting the cancer registry report.");
        }
    }

    /// <summary>Record acceptance of a cancer registry report.</summary>
    [HttpPost("accept/{patientId}/{reportId}")]
    public async Task<IActionResult> AcceptReport(string patientId, string reportId,
        [FromBody] AcceptCancerReportRequest request)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string decodedReportId = Uri.UnescapeDataString(reportId);
            await GetWorkflow(decodedId).AcceptCancerRegistryReportAsync(
                decodedReportId, request.RegistryResponse);
            return Ok(new { ReportId = decodedReportId, Status = "Accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting cancer registry report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while recording the report acceptance.");
        }
    }

    /// <summary>Record rejection of a cancer registry report.</summary>
    [HttpPost("reject/{patientId}/{reportId}")]
    public async Task<IActionResult> RejectReport(string patientId, string reportId,
        [FromBody] RejectCancerReportRequest request)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string decodedReportId = Uri.UnescapeDataString(reportId);
            await GetWorkflow(decodedId).RejectCancerRegistryReportAsync(
                decodedReportId, request.RejectionReason);
            return Ok(new { ReportId = decodedReportId, Status = "Rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting cancer registry report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while recording the report rejection.");
        }
    }

    // ─── Report Index / Dashboard ────────────────────────────────────────

    /// <summary>Get all cancer registry reports (system-wide).</summary>
    [HttpGet("reports")]
    public async Task<IActionResult> GetAllReports()
    {
        try
        {
            List<CancerRegistryReportIndexEntry> reports = await GetIndex().GetAllReportsAsync();
            return Ok(reports.Select(r => new CancerRegistryReportSummaryResponse(r)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing cancer registry reports");
            return StatusCode(500, "An error occurred while listing cancer registry reports.");
        }
    }

    /// <summary>Get cancer registry reports by patient.</summary>
    [HttpGet("reports/patient/{patientId}")]
    public async Task<IActionResult> GetReportsByPatient(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            List<CancerRegistryReportIndexEntry> reports = await GetIndex().GetReportsByPatientAsync(decodedId);
            return Ok(reports.Select(r => new CancerRegistryReportSummaryResponse(r)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing cancer registry reports for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while listing cancer registry reports.");
        }
    }

    /// <summary>Get cancer registry reports by status.</summary>
    [HttpGet("reports/status/{status}")]
    public async Task<IActionResult> GetReportsByStatus(string status)
    {
        try
        {
            List<CancerRegistryReportIndexEntry> reports = await GetIndex().GetReportsByStatusAsync(status);
            return Ok(reports.Select(r => new CancerRegistryReportSummaryResponse(r)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing cancer registry reports by status {Status}", status);
            return StatusCode(500, "An error occurred while listing cancer registry reports.");
        }
    }

    /// <summary>Get pending (generated but not yet submitted) cancer registry reports.</summary>
    [HttpGet("reports/pending")]
    public async Task<IActionResult> GetPendingReports()
    {
        try
        {
            List<CancerRegistryReportIndexEntry> reports = await GetIndex().GetPendingReportsAsync();
            return Ok(reports.Select(r => new CancerRegistryReportSummaryResponse(r)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pending cancer registry reports");
            return StatusCode(500, "An error occurred while listing pending cancer registry reports.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public record GenerateCancerReportRequest(
    string TumorId,
    string ReportingFacility,
    string RegistrarId,
    string RegistrarName);

public record SubmitCancerReportRequest(
    string RegistryName,
    string? ConfirmationNumber);

public record AcceptCancerReportRequest(
    string? RegistryResponse);

public record RejectCancerReportRequest(
    string RejectionReason);

// ─── Response DTOs ───────────────────────────────────────────────────────────

public record CancerRegistryReportResponse(
    string ReportId,
    string PatientId,
    string TumorId,
    string PatientName,
    string PrimarySite,
    string PrimarySiteText,
    string Histology,
    string HistologyText,
    DateTime DateOfDiagnosis,
    string? StageGroup,
    string? SeerSummaryStage,
    string TreatmentSummary,
    string Status,
    string ReportingFacility,
    string RegistrarName,
    string? RegistryName,
    string? ConfirmationNumber,
    string? RejectionReason,
    DateTime CreatedDate,
    DateTime? SubmittedDate)
{
    public CancerRegistryReportResponse(CancerRegistryReportState s) : this(
        s.ReportId, s.PatientId, s.TumorId, s.PatientName,
        s.PrimarySite, s.PrimarySiteText, s.Histology, s.HistologyText,
        s.DateOfDiagnosis, s.StageGroup, s.SeerSummaryStage,
        s.TreatmentSummary, s.Status.ToString(), s.ReportingFacility,
        s.RegistrarName, s.RegistryName, s.ConfirmationNumber,
        s.RejectionReason, s.CreatedDate, s.SubmittedDate)
    { }
}

public record CancerRegistryReportSummaryResponse(
    string ReportId,
    string PatientId,
    string PatientName,
    string TumorId,
    string PrimarySiteText,
    DateTime DateOfDiagnosis,
    string Status,
    string ReportingFacility,
    DateTime CreatedDate,
    string? RegistryName)
{
    public CancerRegistryReportSummaryResponse(CancerRegistryReportIndexEntry e) : this(
        e.ReportId, e.PatientId, e.PatientName, e.TumorId,
        e.PrimarySiteText, e.DateOfDiagnosis, e.Status.ToString(),
        e.ReportingFacility, e.CreatedDate, e.RegistryName)
    { }
}
