// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Financial Reporting and AR Aging — aging analysis, revenue cycle metrics,
/// and AR dashboard queries (VistA PRCA — RCRP*.m).
/// </summary>
[Authorize]
[ApiController]
[Route("api/ar-aging")]
[Produces("application/json")]
public class ARAgingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ARAgingController> _logger;

    public ARAgingController(IGrainFactory grainFactory, ILogger<ARAgingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── AR Aging Report ────────────────────────────────────────────────────

    [HttpPost("{patientId}/generate")]
    public async Task<IActionResult> GenerateReport(string patientId, [FromBody] GenerateReportRequest? req)
    {
        try
        {
            ARAgingReportState report = await W(patientId).GenerateARAgingReportAsync(
                req?.GeneratedByUserId, req?.GeneratedByUserName);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AR aging report for {PatientId}", patientId);
            return StatusCode(500, "Failed to generate AR aging report.");
        }
    }

    [HttpGet("{patientId}/report")]
    public async Task<IActionResult> GetReport(string patientId)
    {
        try { return Ok(await W(patientId).GetARAgingReportAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AR aging report for {PatientId}", patientId);
            return StatusCode(500, "Failed to get AR aging report.");
        }
    }

    [HttpGet("{patientId}/buckets")]
    public async Task<IActionResult> GetBuckets(string patientId)
    {
        try { return Ok(await W(patientId).GetARAgingBucketsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting aging buckets for {PatientId}", patientId);
            return StatusCode(500, "Failed to get aging buckets.");
        }
    }

    [HttpGet("{patientId}/metrics")]
    public async Task<IActionResult> GetMetrics(string patientId)
    {
        try
        {
            RevenueCycleMetrics? metrics = await W(patientId).GetRevenueCycleMetricsAsync();
            return metrics is not null ? Ok(metrics) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue cycle metrics for {PatientId}", patientId);
            return StatusCode(500, "Failed to get revenue cycle metrics.");
        }
    }

    [HttpGet("{patientId}/buckets/{bucket}")]
    public async Task<IActionResult> GetAccountsByBucket(string patientId, AgingBucket bucket)
    {
        try { return Ok(await W(patientId).GetAccountsByAgingBucketAsync(bucket)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accounts for bucket {Bucket}, patient {PatientId}", bucket, patientId);
            return StatusCode(500, "Failed to get accounts by aging bucket.");
        }
    }

    // ─── Demo Data ──────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemoData([FromQuery] string patientId)
    {
        try
        {
            IPatientWorkflowGrain wf = W(patientId);

            // Create demo AR accounts across different aging periods
            await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(15));
            await wf.CreateARAccountAsync(null, "CopayPharmacy", 24m, DateTime.UtcNow.AddDays(-15));
            await wf.CreateARAccountAsync(null, "CopayInpatient", 200m, DateTime.UtcNow.AddDays(-45));
            await wf.CreateARAccountAsync(null, "CopayOutpatient", 35m, DateTime.UtcNow.AddDays(-80));
            await wf.CreateARAccountAsync(null, "MedicareDeductible", 150m, DateTime.UtcNow.AddDays(-120));

            // Generate the aging report
            await wf.GenerateARAgingReportAsync("DEMO-USER", "Demo User");

            return Ok(new { Message = "Demo AR aging data loaded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo AR aging data for {PatientId}", patientId);
            return StatusCode(500, "Failed to load demo data.");
        }
    }

    public record GenerateReportRequest
    {
        public string? GeneratedByUserId { get; init; }
        public string? GeneratedByUserName { get; init; }
    }
}
