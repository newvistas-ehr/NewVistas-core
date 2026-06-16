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
/// Care Team API — manages patient care team membership (PCMM File #404.43).
/// </summary>
[Authorize]
[ApiController]
[Route("api/careteam")]
[Produces("application/json")]
public class CareTeamController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CareTeamController> _logger;

    public CareTeamController(IGrainFactory grainFactory, ILogger<CareTeamController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// Get all care team members for a patient (active and inactive).
    /// </summary>
    [HttpGet("{patientId}/team")]
    [ProducesResponseType(typeof(List<CareTeamMember>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CareTeamMember>>> GetCareTeam(string patientId)
    {
        try
        {
            List<CareTeamMember> members = await GetWorkflow(patientId).GetCareTeamAsync();
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving care team for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving care team");
        }
    }

    /// <summary>
    /// Get only active (non-expired) care team members.
    /// </summary>
    [HttpGet("{patientId}/team/active")]
    [ProducesResponseType(typeof(List<CareTeamMember>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CareTeamMember>>> GetActiveCareTeam(string patientId)
    {
        try
        {
            List<CareTeamMember> members = await GetWorkflow(patientId).GetActiveCareTeamAsync();
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active care team for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving active care team");
        }
    }

    /// <summary>
    /// Add a provider to the patient's care team.
    /// </summary>
    [HttpPost("{patientId}/team")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMember(string patientId, [FromBody] AddCareTeamMemberRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AddCareTeamMemberAsync(
                request.ProviderId, request.ProviderName, request.Role,
                request.Specialty, request.AssignmentSource, request.ExpirationDate);
            return Created();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding care team member for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding care team member");
        }
    }

    /// <summary>
    /// Remove a provider from the care team (deactivates, does not delete).
    /// </summary>
    [HttpDelete("{patientId}/team/{providerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveMember(string patientId, string providerId)
    {
        try
        {
            await GetWorkflow(patientId).RemoveCareTeamMemberAsync(providerId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing care team member {ProviderId} for patient {PatientId}", providerId, patientId);
            return StatusCode(500, "An error occurred while removing care team member");
        }
    }

    /// <summary>
    /// Set the Primary Care Provider for a patient.
    /// </summary>
    [HttpPut("{patientId}/team/pcp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPcp(string patientId, [FromBody] SetPcpRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetPcpAsync(request.ProviderId, request.ProviderName, request.Specialty);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting PCP for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while setting PCP");
        }
    }

    /// <summary>
    /// Get the current PCP for a patient.
    /// </summary>
    [HttpGet("{patientId}/team/pcp")]
    [ProducesResponseType(typeof(CareTeamMember), StatusCodes.Status200OK)]
    public async Task<ActionResult<CareTeamMember?>> GetPcp(string patientId)
    {
        try
        {
            CareTeamMember? pcp = await GetWorkflow(patientId).GetPcpAsync();
            return Ok(pcp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PCP for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving PCP");
        }
    }

    /// <summary>
    /// Check whether a provider is on the care team.
    /// </summary>
    [HttpGet("{patientId}/team/check/{providerId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckMembership(string patientId, string providerId)
    {
        try
        {
            bool isMember = await GetWorkflow(patientId).IsOnCareTeamAsync(providerId);
            return Ok(isMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking care team membership for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while checking care team membership");
        }
    }

    /// <summary>
    /// Get active care team members eligible for secure messaging.
    /// </summary>
    [HttpGet("{patientId}/team/messaging")]
    [ProducesResponseType(typeof(List<CareTeamMember>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CareTeamMember>>> GetMessagingTeam(string patientId)
    {
        try
        {
            List<CareTeamMember> members = await GetWorkflow(patientId).GetCareTeamForMessagingAsync();
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messaging team for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving messaging team");
        }
    }
}

public record AddCareTeamMemberRequest(
    string ProviderId,
    string ProviderName,
    string Role,
    string? Specialty,
    string AssignmentSource,
    DateTime? ExpirationDate);

public record SetPcpRequest(
    string ProviderId,
    string ProviderName,
    string? Specialty);
