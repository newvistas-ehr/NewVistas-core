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
public class iCareDashboardController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<iCareDashboardController> _logger;

    public iCareDashboardController(IGrainFactory grainFactory, ILogger<iCareDashboardController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IiCareDashboardGrain GetDashboard(string providerId)
        => _grainFactory.GetGrain<IiCareDashboardGrain>($"ICARE:{providerId}");

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("ICARE_DASHBOARD");
            return Ok(new { Feature = "ICARE_DASHBOARD", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{providerId}/panel")]
    public async Task<IActionResult> GetPanel(string providerId)
    {
        try { return Ok(await GetDashboard(providerId).GetPanelAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{providerId}/panel")]
    public async Task<IActionResult> AddToPanel(string providerId, [FromBody] AddToPanelRequest req)
    {
        try
        {
            await GetDashboard(providerId).AddPatientToPanelAsync(req.PatientId, req.PatientName);
            return Ok(new { Message = "Patient added to panel." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpDelete("{providerId}/panel/{patientId}")]
    public async Task<IActionResult> RemoveFromPanel(string providerId, string patientId)
    {
        try
        {
            await GetDashboard(providerId).RemovePatientFromPanelAsync(patientId);
            return Ok(new { Message = "Patient removed from panel." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{providerId}/generate")]
    public async Task<IActionResult> GenerateDashboard(string providerId)
    {
        try
        {
            var result = await GetDashboard(providerId).GenerateDashboardAsync();
            return Ok(result);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{providerId}/dashboard")]
    public async Task<IActionResult> GetDashboardState(string providerId)
    {
        try { return Ok(await GetDashboard(providerId).GetDashboardStateAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{providerId}/patients/{patientId}")]
    public async Task<IActionResult> GetPatientSummary(string providerId, string patientId)
    {
        try { return Ok(await GetDashboard(providerId).GetPatientSummaryAsync(patientId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record AddToPanelRequest(string PatientId, string PatientName);
