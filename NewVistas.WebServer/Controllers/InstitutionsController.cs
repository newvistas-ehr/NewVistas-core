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
/// Institutions (VistA File #4) — the facility directory that anchors multi-site
/// bed management and the Transfer Center. Admin writes are XUMGR-gated at the grain.
/// </summary>
[ApiController]
[Route("api/institutions")]
[Authorize]
public class InstitutionsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InstitutionsController> _logger;

    public InstitutionsController(IGrainFactory grainFactory, ILogger<InstitutionsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IInstitutionIndexGrain Index()
        => _grainFactory.GetGrain<IInstitutionIndexGrain>("INSTITUTION-INDEX");

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        try { return Ok(await Index().GetAllAsync(activeOnly)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing institutions");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("{institutionId}")]
    public async Task<ActionResult> Get(string institutionId)
    {
        try
        {
            InstitutionState state = await _grainFactory
                .GetGrain<IInstitutionGrain>($"INST:{institutionId}").GetAsync();
            if (string.IsNullOrEmpty(state.Name))
                return NotFound($"Institution '{institutionId}' not found.");
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading institution {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("health-system/{healthSystemId}")]
    public async Task<ActionResult> GetByHealthSystem(string healthSystemId)
    {
        try { return Ok(await Index().GetByHealthSystemAsync(healthSystemId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing institutions for health system {HealthSystemId}", healthSystemId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>System-wide capacity snapshot (fan-out over institutions' capacity rollups).</summary>
    [HttpGet("system-capacity")]
    public async Task<ActionResult> SystemCapacity([FromQuery] string? healthSystemId = null)
    {
        try
        {
            ISystemCapacityGrain capacity = _grainFactory.GetGrain<ISystemCapacityGrain>("SYSTEM-CAPACITY");
            return Ok(await capacity.GetSystemCapacityAsync(healthSystemId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading system capacity");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Transfer-target search: institutions with a placeable bed of the requested kind.</summary>
    [HttpGet("placement-targets")]
    public async Task<ActionResult> PlacementTargets([FromQuery] BedType? bedType = null, [FromQuery] string? capability = null)
    {
        try
        {
            ISystemCapacityGrain capacity = _grainFactory.GetGrain<ISystemCapacityGrain>("SYSTEM-CAPACITY");
            return Ok(await capacity.FindPlacementTargetsAsync(bedType, capability));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching placement targets");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost]
    public async Task<ActionResult> Register([FromBody] RegisterInstitutionRequest r)
    {
        try
        {
            await _grainFactory.GetGrain<IInstitutionGrain>($"INST:{r.InstitutionId}")
                .RegisterAsync(r.Name, r.Type, r.StationNumber,
                    r.HealthSystemId, r.HealthSystemName,
                    r.StreetAddress, r.City, r.State, r.Zip, r.Phone,
                    r.Capabilities, r.LegacyAliases);
            return Created($"api/institutions/{r.InstitutionId}", new { r.InstitutionId });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering institution {InstitutionId}", r.InstitutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPut("{institutionId}")]
    public async Task<ActionResult> Update(string institutionId, [FromBody] UpdateInstitutionRequest r)
    {
        try
        {
            IInstitutionGrain grain = _grainFactory.GetGrain<IInstitutionGrain>($"INST:{institutionId}");
            await grain.UpdateAsync(r.Name, r.Type, r.StationNumber,
                r.HealthSystemId, r.HealthSystemName,
                r.StreetAddress, r.City, r.State, r.Zip, r.Phone);
            if (r.Capabilities is not null)
                await grain.SetCapabilitiesAsync(new HashSet<string>(r.Capabilities));
            if (r.IsActive is not null)
                await grain.SetActiveAsync(r.IsActive.Value);
            if (r.AcceptsInboundTransfers is not null)
                await grain.SetAcceptsInboundTransfersAsync(r.AcceptsInboundTransfers.Value);
            return Ok(new { Message = "Institution updated." });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating institution {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record RegisterInstitutionRequest
{
    public required string InstitutionId { get; init; }
    public required string Name { get; init; }
    public InstitutionType Type { get; init; } = InstitutionType.Hospital;
    public string? StationNumber { get; init; }
    public string? HealthSystemId { get; init; }
    public string? HealthSystemName { get; init; }
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
    public string? Phone { get; init; }
    public List<string>? Capabilities { get; init; }
    public List<string>? LegacyAliases { get; init; }
}

public record UpdateInstitutionRequest
{
    public string? Name { get; init; }
    public InstitutionType? Type { get; init; }
    public string? StationNumber { get; init; }
    public string? HealthSystemId { get; init; }
    public string? HealthSystemName { get; init; }
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
    public string? Phone { get; init; }
    public List<string>? Capabilities { get; init; }
    public bool? IsActive { get; init; }
    public bool? AcceptsInboundTransfers { get; init; }
}
