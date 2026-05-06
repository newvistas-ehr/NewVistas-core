// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Per-patient diabetes registry — chart-facing surface (read of snapshot
/// and pre-visit plan). Mutating operations (enrollment, HbA1c/exam recording)
/// are intentionally NOT exposed here; the workflow grain enforces
/// <c>CanManageDiabetesRegistry</c> for those, and the Blazor / WpfDelphiUI
/// chart panel only needs reads.
///
/// Reads are open to any authenticated clinician (matches the workflow
/// grain's read-side permissions per <see cref="IPatientWorkflowGrain"/>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/diabetesregistry")]
public class DiabetesRegistryController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DiabetesRegistryController> _logger;

    public DiabetesRegistryController(IGrainFactory grainFactory, ILogger<DiabetesRegistryController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>GET api/diabetesregistry/{patientId}/snapshot — Returns the computed registry snapshot.</summary>
    [HttpGet("{patientId}/snapshot")]
    public async Task<IActionResult> GetSnapshot(string patientId)
    {
        try
        {
            DiabetesRegistrySnapshot snap = await GetWorkflow(patientId).GetDiabetesRegistrySnapshotAsync();
            return Ok(snap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving diabetes registry snapshot for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>
    /// GET api/diabetesregistry/{patientId}/previsit-plan?visitDate=2026-05-02
    /// Returns the pre-visit plan items due/overdue/up-to-date for a visit on the given date.
    /// </summary>
    [HttpGet("{patientId}/previsit-plan")]
    public async Task<IActionResult> GetPreVisitPlan(string patientId, [FromQuery] DateTime? visitDate = null)
    {
        try
        {
            DateTime asOf = visitDate ?? DateTime.UtcNow;
            DiabetesPreVisitPlan plan = await GetWorkflow(patientId).GetDiabetesPreVisitPlanAsync(asOf);
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving diabetes pre-visit plan for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }
}
