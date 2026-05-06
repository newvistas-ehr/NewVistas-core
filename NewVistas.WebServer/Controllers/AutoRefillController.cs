// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AutoRefillController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AutoRefillController> _logger;

    public AutoRefillController(IGrainFactory grainFactory, ILogger<AutoRefillController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("AUTO_REFILL");
            return Ok(new { Feature = "AUTO_REFILL", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking feature"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/enrollments")]
    public async Task<IActionResult> Enroll(string patientId, [FromBody] EnrollAutoRefillRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).EnrollAutoRefillAsync(
                req.PrescriptionId, req.DrugName, req.DrugClass,
                req.DaysSupply, req.RefillsRemaining, req.LastFillDate,
                req.PharmacyId, req.PharmacyName,
                req.EnrolledByProviderId, req.EnrolledByProviderName);
            return Created($"api/autorefill/{patientId}/enrollments/{result.EnrollmentId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error enrolling"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/enrollments")]
    public async Task<IActionResult> GetEnrollments(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetAutoRefillEnrollmentsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting enrollments"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/enrollments/{enrollmentId}")]
    public async Task<IActionResult> GetEnrollment(string patientId, string enrollmentId)
    {
        try { return Ok(await GetWorkflow(patientId).GetAutoRefillEnrollmentAsync(enrollmentId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting enrollment"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/enrollments/{enrollmentId}/suspend")]
    public async Task<IActionResult> Suspend(string patientId, string enrollmentId, [FromBody] AutoRefillReasonRequest req)
    {
        try
        {
            await GetWorkflow(patientId).SuspendAutoRefillAsync(enrollmentId, req.Reason, req.PerformedByName);
            return Ok(new { Message = "Auto-refill suspended." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error suspending"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/enrollments/{enrollmentId}/resume")]
    public async Task<IActionResult> Resume(string patientId, string enrollmentId, [FromBody] AutoRefillActionRequest req)
    {
        try
        {
            await GetWorkflow(patientId).ResumeAutoRefillAsync(enrollmentId, req.PerformedByName);
            return Ok(new { Message = "Auto-refill resumed." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error resuming"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/enrollments/{enrollmentId}/disenroll")]
    public async Task<IActionResult> Disenroll(string patientId, string enrollmentId, [FromBody] AutoRefillReasonRequest req)
    {
        try
        {
            await GetWorkflow(patientId).DisenrollAutoRefillAsync(enrollmentId, req.Reason, req.PerformedByName);
            return Ok(new { Message = "Disenrolled from auto-refill." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error disenrolling"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("due")]
    public async Task<IActionResult> GetDueForRefill([FromQuery] DateTime? asOfDate, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IAutoRefillIndexGrain>("RX-AUTOREFILL-IDX");
            return Ok(await index.GetDueForRefillAsync(asOfDate ?? DateTime.UtcNow, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting due refills"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? patientId, [FromQuery] string? status, [FromQuery] string? pharmacyId, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IAutoRefillIndexGrain>("RX-AUTOREFILL-IDX");
            return Ok(await index.SearchAsync(patientId, status, pharmacyId, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting dashboard"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record EnrollAutoRefillRequest(
    string PrescriptionId, string DrugName, string DrugClass,
    int DaysSupply, int RefillsRemaining, DateTime LastFillDate,
    string PharmacyId, string PharmacyName,
    string EnrolledByProviderId, string EnrolledByProviderName);

public record AutoRefillActionRequest(string PerformedByName);
public record AutoRefillReasonRequest(string Reason, string PerformedByName);
