// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Claim Status Inquiry (EDI 276/277) — IBCSC*.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/claim-status")]
[Produces("application/json")]
public class ClaimStatusController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ClaimStatusController> _logger;

    public ClaimStatusController(IGrainFactory grainFactory, ILogger<ClaimStatusController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}/inquiries")]
    public async Task<IActionResult> SubmitInquiry(string patientId, [FromBody] SubmitCsiRequest req)
    {
        try
        {
            string id = await W(patientId).SubmitClaimStatusInquiryAsync(
                req.ClaimId, req.PayerId, req.PayerName,
                req.InitiatedByUserId, req.InitiatedByUserName, req.Notes);
            return Created("", new { InquiryId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting claim status inquiry for {PatientId}", patientId);
            return StatusCode(500, "Failed to submit claim status inquiry.");
        }
    }

    [HttpGet("{patientId}/inquiries")]
    public async Task<IActionResult> GetInquiries(string patientId)
    {
        try { return Ok(await W(patientId).GetClaimStatusInquiriesAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting claim status inquiries for {PatientId}", patientId);
            return StatusCode(500, "Failed to get claim status inquiries.");
        }
    }

    [HttpGet("{patientId}/inquiries/{inquiryId}")]
    public async Task<IActionResult> GetInquiry(string patientId, string inquiryId)
    {
        try { return Ok(await W(patientId).GetClaimStatusInquiryAsync(inquiryId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inquiry {InquiryId}", inquiryId);
            return StatusCode(500, "Failed to get claim status inquiry.");
        }
    }

    [HttpGet("{patientId}/inquiries/claim/{claimId}")]
    public async Task<IActionResult> GetByClaimId(string patientId, string claimId)
    {
        try { return Ok(await W(patientId).GetClaimStatusInquiriesByClaimAsync(claimId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inquiries for claim {ClaimId}", claimId);
            return StatusCode(500, "Failed to get claim status inquiries.");
        }
    }

    public record SubmitCsiRequest
    {
        public string ClaimId { get; init; } = string.Empty;
        public string PayerId { get; init; } = string.Empty;
        public string PayerName { get; init; } = string.Empty;
        public string? InitiatedByUserId { get; init; }
        public string? InitiatedByUserName { get; init; }
        public string? Notes { get; init; }
    }
}
