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
public class ExternalReferralController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ExternalReferralController> _logger;

    public ExternalReferralController(IGrainFactory grainFactory, ILogger<ExternalReferralController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("EXTERNAL_REFERRAL_TRACKING");
            return Ok(new { Feature = "EXTERNAL_REFERRAL_TRACKING", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking feature"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/referrals")]
    public async Task<IActionResult> CreateReferral(string patientId, [FromBody] CreateExternalReferralRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).CreateExternalReferralAsync(
                req.ReferralType, req.ExternalFacilityName, req.ExternalFacilityId,
                req.ExternalProviderName, req.ExternalProviderId,
                req.Purpose, req.Diagnosis, req.Urgency,
                req.ReferredByProviderId, req.ReferredByProviderName,
                req.ConsultId, req.AuthorizationNumber,
                req.AppointmentDateTime, req.SpecialInstructions);
            return Created($"api/externalreferral/{patientId}/referrals/{result.ReferralId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error creating referral"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/referrals")]
    public async Task<IActionResult> GetReferrals(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetExternalReferralsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting referrals"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/referrals/{referralId}")]
    public async Task<IActionResult> GetReferral(string patientId, string referralId)
    {
        try { return Ok(await GetWorkflow(patientId).GetExternalReferralAsync(referralId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting referral"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/referrals/{referralId}/status")]
    public async Task<IActionResult> UpdateStatus(string patientId, string referralId, [FromBody] UpdateExternalReferralStatusRequest req)
    {
        try
        {
            await GetWorkflow(patientId).UpdateExternalReferralStatusAsync(referralId, req.Status, req.StatusReason, req.UpdatedById, req.UpdatedByName);
            return Ok(new { Message = "Status updated." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error updating status"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/referrals/{referralId}/complete")]
    public async Task<IActionResult> CompleteReferral(string patientId, string referralId, [FromBody] CompleteReferralRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CompleteExternalReferralAsync(referralId, req.CompletionDate, req.OutcomeNotes, req.ClinicalFindings);
            return Ok(new { Message = "Referral completed." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error completing referral"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? status, [FromQuery] string? facility, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IExternalReferralIndexGrain>("EXT-REF-IDX");
            var results = await index.SearchAsync(null, status, facility, maxResults);
            return Ok(results);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting dashboard"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("pending-followups")]
    public async Task<IActionResult> GetPendingFollowUps([FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IExternalReferralIndexGrain>("EXT-REF-IDX");
            return Ok(await index.GetPendingFollowUpsAsync(maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting pending follow-ups"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    // ── Contract Health Services (CHS / PRC) — IHS-specific (25 CFR Part 136) ──
    // Workflow grain enforces CanAuthorizeChs on each of these via
    // [RequiresSecurityKey]; the controller is just the HTTP transport.

    [HttpPost("{patientId}/referrals/{referralId}/chs/request")]
    public async Task<IActionResult> RequestChsAuthorization(string patientId, string referralId, [FromBody] RequestChsAuthorizationRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RequestChsAuthorizationAsync(
                referralId, req.EstimatedCost, req.MedicalPriorityClass,
                req.AlternateResourcesChecked, req.AlternateResourcesNote,
                req.RequestedById, req.RequestedByName);
            return Ok(new { Message = "CHS authorization requested." });
        }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error requesting CHS authorization"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/referrals/{referralId}/chs/approve")]
    public async Task<IActionResult> ApproveChsAuthorization(string patientId, string referralId, [FromBody] ApproveChsAuthorizationRequest req)
    {
        try
        {
            await GetWorkflow(patientId).ApproveChsAuthorizationAsync(
                referralId, req.AuthorizedAmount, req.AuthorizationNumber,
                req.ApprovedById, req.ApprovedByName);
            return Ok(new { Message = "CHS authorization approved." });
        }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error approving CHS authorization"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/referrals/{referralId}/chs/deny")]
    public async Task<IActionResult> DenyChsAuthorization(string patientId, string referralId, [FromBody] DenyChsAuthorizationRequest req)
    {
        try
        {
            await GetWorkflow(patientId).DenyChsAuthorizationAsync(
                referralId, req.DenialReason, req.DeniedById, req.DeniedByName);
            return Ok(new { Message = "CHS authorization denied." });
        }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error denying CHS authorization"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record RequestChsAuthorizationRequest(
    decimal EstimatedCost,
    string MedicalPriorityClass,
    bool AlternateResourcesChecked,
    string? AlternateResourcesNote,
    string RequestedById,
    string RequestedByName);

public record ApproveChsAuthorizationRequest(
    decimal AuthorizedAmount,
    string? AuthorizationNumber,
    string ApprovedById,
    string ApprovedByName);

public record DenyChsAuthorizationRequest(
    string DenialReason,
    string DeniedById,
    string DeniedByName);

public record CreateExternalReferralRequest(
    string ReferralType, string ExternalFacilityName, string? ExternalFacilityId,
    string? ExternalProviderName, string? ExternalProviderId,
    string Purpose, string? Diagnosis, string Urgency,
    string ReferredByProviderId, string ReferredByProviderName,
    string? ConsultId, string? AuthorizationNumber,
    DateTime? AppointmentDateTime, string? SpecialInstructions);

public record UpdateExternalReferralStatusRequest(string Status, string? StatusReason, string UpdatedById, string UpdatedByName);
public record CompleteReferralRequest(DateTime CompletionDate, string? OutcomeNotes, string? ClinicalFindings);
