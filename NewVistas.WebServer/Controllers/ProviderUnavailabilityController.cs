// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Provider Unavailability API — manages sudden provider unavailability events
/// (illness, injury, emergency) with batch cancellation or reassignment.
/// </summary>
[Authorize]
[ApiController]
[Route("api/provider-unavailability")]
[Produces("application/json")]
public class ProviderUnavailabilityController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ProviderUnavailabilityController> _logger;

    public ProviderUnavailabilityController(IGrainFactory grainFactory, ILogger<ProviderUnavailabilityController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IProviderUnavailabilityGrain GetEvent(string eventId)
        => _grainFactory.GetGrain<IProviderUnavailabilityGrain>(eventId);

    /// <summary>Check whether batch provider unavailability is enabled for this site.</summary>
    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        bool enabled = await siteParams.IsFeatureEnabledAsync("PROVIDER_UNAVAILABILITY_BATCH");
        return Ok(new { Feature = "PROVIDER_UNAVAILABILITY_BATCH", Enabled = enabled });
    }

    /// <summary>Create an unavailability event (identifies affected appointments).</summary>
    [HttpPost]
    public async Task<ActionResult<ProviderUnavailabilityState>> CreateEvent([FromBody] CreateUnavailabilityRequest request)
    {
        try
        {
            string eventId = $"PROV-UNAVAIL:{Guid.NewGuid()}";
            IProviderUnavailabilityGrain grain = GetEvent(eventId);
            ProviderUnavailabilityState state = await grain.CreateEventAsync(
                request.ProviderId, request.ProviderName,
                request.UnavailableFrom, request.UnavailableTo,
                request.Reason, request.Notes,
                request.InitiatedByUserId, request.InitiatedByUserName);
            return Created($"api/provider-unavailability/{eventId}", state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating unavailability event for provider {ProviderId}", request.ProviderId);
            return StatusCode(500, "An error occurred while creating unavailability event");
        }
    }

    /// <summary>Get an unavailability event status including affected appointments.</summary>
    [HttpGet("{eventId}")]
    public async Task<ActionResult<ProviderUnavailabilityState>> GetEvent(string eventId, int _unused = 0)
    {
        try
        {
            ProviderUnavailabilityState state = await GetEvent(eventId).GetEventAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unavailability event {EventId}", eventId);
            return StatusCode(500, "An error occurred while retrieving event");
        }
    }

    /// <summary>Preview affected appointments without taking action.</summary>
    [HttpGet("{eventId}/preview")]
    public async Task<ActionResult<List<AffectedAppointmentRecord>>> PreviewAffected(string eventId)
    {
        try
        {
            ProviderUnavailabilityState state = await GetEvent(eventId).GetEventAsync();
            return Ok(state.AffectedAppointments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing affected appointments for event {EventId}", eventId);
            return StatusCode(500, "An error occurred while previewing affected appointments");
        }
    }

    /// <summary>Execute batch cancellation of all affected appointments.</summary>
    [HttpPost("{eventId}/cancel-all")]
    public async Task<ActionResult<ProviderUnavailabilityResult>> CancelAll(string eventId)
    {
        try
        {
            ProviderUnavailabilityResult result = await GetEvent(eventId).ExecuteBatchCancellationAsync();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing batch cancellation for event {EventId}", eventId);
            return StatusCode(500, "An error occurred while executing batch cancellation");
        }
    }

    /// <summary>Execute batch reassignment to a replacement provider.</summary>
    [HttpPost("{eventId}/reassign")]
    public async Task<ActionResult<ProviderUnavailabilityResult>> Reassign(
        string eventId, [FromBody] ReassignRequest request)
    {
        try
        {
            ProviderUnavailabilityResult result = await GetEvent(eventId)
                .ExecuteBatchReassignmentAsync(request.ReplacementProviderId, request.ReplacementProviderName);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing batch reassignment for event {EventId}", eventId);
            return StatusCode(500, "An error occurred while executing batch reassignment");
        }
    }

    public record CreateUnavailabilityRequest(
        string ProviderId,
        string ProviderName,
        DateTime UnavailableFrom,
        DateTime UnavailableTo,
        string Reason,
        string? Notes,
        string InitiatedByUserId,
        string InitiatedByUserName);

    public record ReassignRequest(
        string ReplacementProviderId,
        string ReplacementProviderName);
}
