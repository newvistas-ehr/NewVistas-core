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
/// Pharmacogenomics (PGx) API — a patient's pharmacogenomic profile and the
/// gene/drug recommendations derived from it.
///
/// All operations are patient-scoped and route through <see cref="IPatientWorkflowGrain"/>
/// keyed by the <c>{patientId}</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PharmacogenomicsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PharmacogenomicsController> _logger;

    public PharmacogenomicsController(IGrainFactory grainFactory, ILogger<PharmacogenomicsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Profile / Recommendations Reads ─────────────────────────────────────

    /// <summary>Returns the patient's pharmacogenomic profile (all recorded gene results).</summary>
    [HttpGet("{patientId}/profile")]
    [ProducesResponseType(typeof(PharmacogenomicsState), StatusCodes.Status200OK)]
    public async Task<ActionResult<PharmacogenomicsState>> GetProfile(string patientId)
    {
        try
        {
            PharmacogenomicsState profile = await GetWorkflow(patientId).GetPharmacogenomicProfileAsync();
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pharmacogenomic profile for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the pharmacogenomic profile");
        }
    }

    /// <summary>Returns all pharmacogenomic recommendations derived from the patient's profile.</summary>
    [HttpGet("{patientId}/recommendations")]
    [ProducesResponseType(typeof(List<Abstractions.Clinical.PgxRecommendation>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Abstractions.Clinical.PgxRecommendation>>> GetRecommendations(string patientId)
    {
        try
        {
            List<Abstractions.Clinical.PgxRecommendation> recommendations =
                await GetWorkflow(patientId).GetPharmacogenomicRecommendationsAsync();
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pharmacogenomic recommendations for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the pharmacogenomic recommendations");
        }
    }

    /// <summary>Returns pharmacogenomic recommendations for a specific drug.</summary>
    [HttpGet("{patientId}/check")]
    [ProducesResponseType(typeof(List<Abstractions.Clinical.PgxRecommendation>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Abstractions.Clinical.PgxRecommendation>>> CheckDrug(
        string patientId, [FromQuery] string drug)
    {
        try
        {
            List<Abstractions.Clinical.PgxRecommendation> recommendations =
                await GetWorkflow(patientId).CheckDrugPharmacogenomicsAsync(drug);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking drug pharmacogenomics for drug {Drug} for patient {PatientId}",
                drug, patientId);
            return StatusCode(500, "An error occurred while checking the drug pharmacogenomics");
        }
    }

    // ─── Result Record / Remove ──────────────────────────────────────────────

    /// <summary>Records a pharmacogenomic gene result for the patient. Returns the result id.</summary>
    [HttpPost("{patientId}/results")]
    [ProducesResponseType(typeof(RecordPgxResultResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecordPgxResultResponse>> RecordResult(
        string patientId, [FromBody] RecordPgxResultRequest request)
    {
        try
        {
            string resultId = await GetWorkflow(patientId).RecordPharmacogenomicResultAsync(
                request.Gene,
                request.Diplotype,
                request.Phenotype,
                request.ActivityScore,
                request.TestDate,
                request.Lab ?? string.Empty,
                request.Method ?? string.Empty,
                request.Notes ?? string.Empty,
                request.RecordedBy ?? string.Empty);
            return Created($"/api/pharmacogenomics/{patientId}/results",
                new RecordPgxResultResponse { ResultId = resultId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording pharmacogenomic result for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording the pharmacogenomic result");
        }
    }

    /// <summary>Removes the pharmacogenomic result for a given gene.</summary>
    [HttpDelete("{patientId}/results/{gene}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveResult(string patientId, string gene)
    {
        try
        {
            await GetWorkflow(patientId).RemovePharmacogenomicResultAsync(gene);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing pharmacogenomic result for gene {Gene} for patient {PatientId}",
                gene, patientId);
            return StatusCode(500, "An error occurred while removing the pharmacogenomic result");
        }
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────────────

public record RecordPgxResultRequest
{
    public string Gene { get; init; } = string.Empty;
    public string Diplotype { get; init; } = string.Empty;
    public PgxPhenotype Phenotype { get; init; }
    public decimal? ActivityScore { get; init; }
    public DateTime? TestDate { get; init; }
    public string? Lab { get; init; }
    public string? Method { get; init; }
    public string? Notes { get; init; }
    public string? RecordedBy { get; init; }
}

public record RecordPgxResultResponse
{
    public string ResultId { get; init; } = string.Empty;
}
