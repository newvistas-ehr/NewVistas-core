// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Automatic Eligibility Determination — DGENELA.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/auto-eligibility")]
[Produces("application/json")]
public class AutoEligibilityController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AutoEligibilityController> _logger;

    public AutoEligibilityController(IGrainFactory grainFactory, ILogger<AutoEligibilityController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}/determine")]
    public async Task<IActionResult> RunDetermination(string patientId, [FromBody] DetermineRequest? req)
    {
        try
        {
            AutoEligibilityDeterminationState result = await W(patientId)
                .RunAutoEligibilityDeterminationAsync(req?.DeterminedByUserId, req?.DeterminedByUserName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running auto eligibility for {PatientId}", patientId);
            return StatusCode(500, "Failed to run eligibility determination.");
        }
    }

    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetDetermination(string patientId)
    {
        try { return Ok(await W(patientId).GetAutoEligibilityDeterminationAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eligibility determination for {PatientId}", patientId);
            return StatusCode(500, "Failed to get eligibility determination.");
        }
    }

    public record DetermineRequest
    {
        public string? DeterminedByUserId { get; init; }
        public string? DeterminedByUserName { get; init; }
    }
}
