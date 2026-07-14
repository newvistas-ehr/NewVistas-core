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
/// Home-Health Agency Directory API (<c>api/homehealthagencies</c>) — the facility-wide catalog of
/// home-health agencies an externally-delivered home-care episode points at, and the picker a
/// coordinator chooses from when referring a patient out. Facility-wide (not patient-scoped); calls the
/// singleton <c>HHA-DIRECTORY</c> grain directly. Reads are open; writes are [Authorize]-gated.
/// </summary>
[Authorize]
[ApiController]
[Route("api/homehealthagencies")]
[Produces("application/json")]
public class HomeHealthAgencyDirectoryController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HomeHealthAgencyDirectoryController> _logger;

    public HomeHealthAgencyDirectoryController(IGrainFactory grainFactory, ILogger<HomeHealthAgencyDirectoryController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IHomeHealthAgencyDirectoryGrain Directory() =>
        _grainFactory.GetGrain<IHomeHealthAgencyDirectoryGrain>("HHA-DIRECTORY");

    /// <summary>Lists active agencies, or searches by name/id. <c>externalOnly=true</c> hides the in-house agency.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<HomeHealthAgencyEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeHealthAgencyEntry>>> List([FromQuery] string? q, [FromQuery] bool externalOnly = false)
    {
        try
        {
            List<HomeHealthAgencyEntry> results = string.IsNullOrWhiteSpace(q)
                ? await Directory().GetAllAsync(externalOnly)
                : await Directory().SearchAsync(q, externalOnly);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing home-health agencies");
            return StatusCode(500, "An error occurred while listing home-health agencies");
        }
    }

    /// <summary>Returns a single agency by id.</summary>
    [HttpGet("{agencyId}")]
    [ProducesResponseType(typeof(HomeHealthAgencyEntry), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeHealthAgencyEntry>> Get(string agencyId)
    {
        try
        {
            HomeHealthAgencyEntry? entry = await Directory().GetAsync(agencyId);
            return entry is null ? NotFound() : Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-health agency {AgencyId}", agencyId);
            return StatusCode(500, "An error occurred while retrieving the agency");
        }
    }

    /// <summary>Adds or updates an agency entry.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddOrUpdate([FromBody] HomeHealthAgencyEntry entry)
    {
        try
        {
            await Directory().AddOrUpdateAsync(entry);
            return Created($"/api/homehealthagencies/{entry.AgencyId}", new { entry.AgencyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving home-health agency {AgencyId}", entry.AgencyId);
            return StatusCode(500, "An error occurred while saving the agency");
        }
    }

    /// <summary>Activates or deactivates an agency.</summary>
    [HttpPut("{agencyId}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(string agencyId, [FromBody] SetAgencyActiveRequest request)
    {
        try
        {
            await Directory().SetActiveAsync(agencyId, request.IsActive);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active flag for home-health agency {AgencyId}", agencyId);
            return StatusCode(500, "An error occurred while updating the agency");
        }
    }
}

public record SetAgencyActiveRequest
{
    public bool IsActive { get; init; }
}
