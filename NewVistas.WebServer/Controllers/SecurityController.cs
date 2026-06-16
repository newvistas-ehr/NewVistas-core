// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Security API — Patient Access Control, Sensitivity Flags, Break-the-Glass.
///
/// Mirrors VistA DG SENSITIVITY routines and DG SECURITY LOG (File #38.1).
/// </summary>
[Authorize]
[ApiController]
[Route("api/security")]
[Produces("application/json")]
public class SecurityController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(IGrainFactory grainFactory, ILogger<SecurityController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// Get the patient access control state (sensitivity flags, authorized providers).
    /// </summary>
    [HttpGet("{patientId}/access")]
    [ProducesResponseType(typeof(PatientAccessControlState), StatusCodes.Status200OK)]
    public async Task<ActionResult<PatientAccessControlState>> GetAccessControl(string patientId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");
            var state = await grain.GetAccessControlAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving access control for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving access control");
        }
    }

    /// <summary>
    /// Set patient sensitivity flags and categories.
    /// </summary>
    [HttpPost("{patientId}/sensitivity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetSensitivity(string patientId, [FromBody] SetSensitivityRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetPatientSensitivityAsync(
                request.IsSensitive, request.SensitivityLevel, request.Categories);
            return Ok(new { Message = "Sensitivity updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting sensitivity for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while setting sensitivity");
        }
    }

    /// <summary>
    /// Check if a user has access to the patient record.
    /// </summary>
    [HttpPost("{patientId}/access/check")]
    [ProducesResponseType(typeof(CheckAccessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckAccessResponse>> CheckAccess(string patientId, [FromBody] CheckAccessRequest request)
    {
        try
        {
            bool hasAccess = await GetWorkflow(patientId).CheckPatientAccessAsync(request.UserId);
            return Ok(new CheckAccessResponse { HasAccess = hasAccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking access for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while checking access");
        }
    }

    /// <summary>
    /// Break-the-glass — record override access to a sensitive patient record.
    /// </summary>
    [HttpPost("{patientId}/access/btg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BreakTheGlass(string patientId, [FromBody] BtgRequest request)
    {
        try
        {
            await GetWorkflow(patientId).RecordPatientAccessAsync(
                request.UserId, request.UserName, "BREAK_THE_GLASS", true, request.JustificationText);
            _logger.LogWarning(
                "Break-the-glass access recorded for patient {PatientId} by user {UserId}",
                patientId, request.UserId);
            return Ok(new { Message = "Break-the-glass access recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording BTG access for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording break-the-glass access");
        }
    }

    /// <summary>
    /// Add a provider to the authorized access list.
    /// </summary>
    [HttpPost("{patientId}/providers/{providerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAuthorizedProvider(string patientId, string providerId)
    {
        try
        {
            await GetWorkflow(patientId).AddAuthorizedProviderAsync(providerId);
            return Ok(new { Message = $"Provider {providerId} added to authorized list" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding authorized provider for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding authorized provider");
        }
    }

    /// <summary>
    /// Remove a provider from the authorized access list.
    /// </summary>
    [HttpDelete("{patientId}/providers/{providerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAuthorizedProvider(string patientId, string providerId)
    {
        try
        {
            await GetWorkflow(patientId).RemoveAuthorizedProviderAsync(providerId);
            return Ok(new { Message = $"Provider {providerId} removed from authorized list" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing authorized provider for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while removing authorized provider");
        }
    }

    /// <summary>
    /// Get the patient access audit log.
    /// </summary>
    [HttpGet("{patientId}/access/log")]
    [ProducesResponseType(typeof(List<PatientAccessLog>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PatientAccessLog>>> GetAccessLog(string patientId)
    {
        try
        {
            var log = await GetWorkflow(patientId).GetPatientAccessLogAsync();
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving access log for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving access log");
        }
    }

    /// <summary>
    /// Load demo data for patient access control.
    /// </summary>
    [HttpPost("demo/load")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadDemo([FromQuery] string patientId)
    {
        try
        {
            var workflow = GetWorkflow(patientId);

            // Mark as sensitive with multiple categories
            await workflow.SetPatientSensitivityAsync(
                true, "ELEVATED",
                new List<string> { "BEHAVIORAL", "EMPLOYEE" });

            // Add authorized providers
            await workflow.AddAuthorizedProviderAsync("PROV-001");
            await workflow.AddAuthorizedProviderAsync("PROV-002");

            // Record some access events
            await workflow.RecordPatientAccessAsync(
                "PROV-001", "Dr. Adams", "TREATING_PROVIDER", false, null);
            await workflow.RecordPatientAccessAsync(
                "PROV-003", "Dr. Chen", "BREAK_THE_GLASS", true,
                "Emergency consult — patient presenting with acute symptoms");
            await workflow.RecordPatientAccessAsync(
                "ADMIN-001", "J. Smith", "ADMINISTRATIVE", false, null);

            return Ok(new { Message = "Security demo data loaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading security demo data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while loading demo data");
        }
    }
}

public record SetSensitivityRequest
{
    public bool IsSensitive { get; init; }
    public string SensitivityLevel { get; init; } = string.Empty;
    public List<string> Categories { get; init; } = new();
}

public record CheckAccessRequest
{
    public string UserId { get; init; } = string.Empty;
}

public record CheckAccessResponse
{
    public bool HasAccess { get; init; }
}

public record BtgRequest
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string JustificationText { get; init; } = string.Empty;
}
