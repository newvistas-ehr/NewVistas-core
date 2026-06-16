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
public class ImmunizationForecastController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ImmunizationForecastController> _logger;

    public ImmunizationForecastController(IGrainFactory grainFactory, ILogger<ImmunizationForecastController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Check if IMMUNIZATION_FORECAST feature is enabled.</summary>
    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("IMMUNIZATION_FORECAST");
            return Ok(new { Feature = "IMMUNIZATION_FORECAST", Enabled = enabled });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking forecast feature status");
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>Generate a new immunization forecast for the patient.</summary>
    [HttpPost("{patientId}/forecast")]
    public async Task<IActionResult> GenerateForecast(string patientId)
    {
        try
        {
            ImmunizationForecastResult result = await GetWorkflow(patientId).GenerateImmunizationForecastAsync();
            if (!result.Success)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating forecast for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>Get the most recent forecast without recalculating.</summary>
    [HttpGet("{patientId}/forecast")]
    public async Task<IActionResult> GetForecast(string patientId)
    {
        try
        {
            ImmunizationForecastResult result = await GetWorkflow(patientId).GetImmunizationForecastAsync();
            if (!result.Success)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting forecast for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>Get the ACIP schedule definitions for a patient's forecast grain.</summary>
    [HttpGet("{patientId}/schedule")]
    public async Task<IActionResult> GetSchedule(string patientId)
    {
        try
        {
            string forecastKey = $"IMM-FORECAST:{patientId}";
            var forecastGrain = _grainFactory.GetGrain<IImmunizationForecastGrain>(forecastKey);
            List<VaccineSeriesDefinition> schedule = await forecastGrain.GetScheduleAsync();
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }
}
