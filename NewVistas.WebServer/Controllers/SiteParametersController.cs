// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// System-level site parameters — VistA PARAMETER file (#8989.5).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SiteParametersController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SiteParametersController> _logger;
    private const string DefaultSiteKey = "SITE:DEFAULT";

    public SiteParametersController(IGrainFactory grainFactory, ILogger<SiteParametersController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private ISiteParametersGrain GetSiteParams() =>
        _grainFactory.GetGrain<ISiteParametersGrain>(DefaultSiteKey);

    [HttpGet]
    public async Task<ActionResult<SiteParametersState>> GetParameters()
    {
        try
        {
            SiteParametersState state = await GetSiteParams().GetParametersAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving site parameters");
            return StatusCode(500, "An error occurred while retrieving site parameters");
        }
    }

    [HttpGet("vitals-display-count")]
    public async Task<ActionResult<int>> GetVitalsDisplayCount()
    {
        try
        {
            int count = await GetSiteParams().GetVitalsDisplayCountAsync();
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vitals display count");
            return StatusCode(500, "An error occurred while retrieving vitals display count");
        }
    }

    [HttpPut("vitals-display-count")]
    public async Task<IActionResult> SetVitalsDisplayCount([FromBody] SetVitalsDisplayCountRequest request)
    {
        try
        {
            await GetSiteParams().SetVitalsDisplayCountAsync(request.Count);
            return Ok(new { Message = $"Vitals display count set to {request.Count}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting vitals display count to {Count}", request.Count);
            return StatusCode(500, "An error occurred while setting vitals display count");
        }
    }

    [HttpGet("orders-display-count")]
    public async Task<ActionResult<int>> GetOrdersDisplayCount()
    {
        try
        {
            int count = await GetSiteParams().GetOrdersDisplayCountAsync();
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders display count");
            return StatusCode(500, "An error occurred while retrieving orders display count");
        }
    }

    [HttpPut("orders-display-count")]
    public async Task<IActionResult> SetOrdersDisplayCount([FromBody] SetOrdersDisplayCountRequest request)
    {
        try
        {
            await GetSiteParams().SetOrdersDisplayCountAsync(request.Count);
            return Ok(new { Message = $"Orders display count set to {request.Count}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting orders display count to {Count}", request.Count);
            return StatusCode(500, "An error occurred while setting orders display count");
        }
    }

    [HttpGet("notes-display-count")]
    public async Task<ActionResult<int>> GetNotesDisplayCount()
    {
        try
        {
            int count = await GetSiteParams().GetNotesDisplayCountAsync();
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notes display count");
            return StatusCode(500, "An error occurred while retrieving notes display count");
        }
    }

    [HttpPut("notes-display-count")]
    public async Task<IActionResult> SetNotesDisplayCount([FromBody] SetNotesDisplayCountRequest request)
    {
        try
        {
            await GetSiteParams().SetNotesDisplayCountAsync(request.Count);
            return Ok(new { Message = $"Notes display count set to {request.Count}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting notes display count to {Count}", request.Count);
            return StatusCode(500, "An error occurred while setting notes display count");
        }
    }

    [HttpGet("parameter/{name}")]
    public async Task<ActionResult<string>> GetParameter(string name)
    {
        try
        {
            string? value = await GetSiteParams().GetParameterAsync(name);
            if (value == null) return NotFound();
            return Ok(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parameter {Name}", name);
            return StatusCode(500, "An error occurred while retrieving parameter");
        }
    }

    [HttpPut("parameter/{name}")]
    public async Task<IActionResult> SetParameter(string name, [FromBody] SetParameterRequest request)
    {
        try
        {
            await GetSiteParams().SetParameterAsync(name, request.Value);
            return Ok(new { Message = $"Parameter '{name}' set" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting parameter {Name}", name);
            return StatusCode(500, "An error occurred while setting parameter");
        }
    }

    // ── Feature Flags (Site Flavor Architecture) ──────────────────────────

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
    {
        try
        {
            HashSet<string> features = await GetSiteParams().GetFeaturesAsync();
            return Ok(features);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting features"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("features/{featureName}")]
    public async Task<IActionResult> EnableFeature(string featureName)
    {
        try
        {
            await GetSiteParams().EnableFeatureAsync(featureName);
            return Ok(new { Message = $"Feature '{featureName}' enabled." });
        }
        catch (InvalidOperationException ex)
        {
            // The one-way latch. The explanation is the whole point — a generic 500 would
            // leave an administrator believing this is a transient failure worth retrying.
            return Conflict(new { Error = ex.Message });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error enabling feature {Feature}", featureName); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpDelete("features/{featureName}")]
    public async Task<IActionResult> DisableFeature(string featureName)
    {
        try
        {
            // The attributed overload: for a one-way feature the disable is permanent, so
            // who did it must be recorded permanently with it. The XUMGR key requirement is
            // enforced by the grain call filter against the caller in RequestContext.
            await GetSiteParams().DisableFeatureAsync(
                featureName,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue("display_name") ?? User.FindFirstValue(ClaimTypes.Name),
                "Disabled via site-parameters API");
            return Ok(new { Message = $"Feature '{featureName}' disabled." });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "Disabling a feature requires the XUMGR (system manager) security key." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error disabling feature {Feature}", featureName); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("features/{featureName}")]
    public async Task<IActionResult> IsFeatureEnabled(string featureName)
    {
        try
        {
            bool enabled = await GetSiteParams().IsFeatureEnabledAsync(featureName);
            return Ok(new { Feature = featureName, Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking feature {Feature}", featureName); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record SetVitalsDisplayCountRequest(int Count);
public record SetOrdersDisplayCountRequest(int Count);
public record SetNotesDisplayCountRequest(int Count);
public record SetParameterRequest(string Value);
