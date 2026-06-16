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
/// Drug Interaction Blocking API — DRGINT.m integration in PSO fill workflow.
///
/// Screens prescriptions for drug-drug interactions against active medications.
/// Significant and Contraindicated interactions block fill/refill until a
/// pharmacist overrides each with a documented clinical reason.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/interactionscreening")]
[Produces("application/json")]
public class InteractionBlockingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InteractionBlockingController> _logger;

    public InteractionBlockingController(IGrainFactory grainFactory, ILogger<InteractionBlockingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── GET endpoints ───────────────────────────────────────────────────────

    /// <summary>Gets all interaction screenings for a patient.</summary>
    [HttpGet]
    public async Task<ActionResult<List<InteractionScreeningIndexEntry>>> GetScreenings(string patientId)
    {
        try
        {
            List<InteractionScreeningIndexEntry> results = await GetWorkflow(patientId).GetInteractionScreeningsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving interaction screenings for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving interaction screenings.");
        }
    }

    /// <summary>Gets prescriptions currently blocked by drug interactions.</summary>
    [HttpGet("blocked")]
    public async Task<ActionResult<List<InteractionScreeningIndexEntry>>> GetBlocked(string patientId)
    {
        try
        {
            List<InteractionScreeningIndexEntry> results = await GetWorkflow(patientId).GetBlockedPrescriptionsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blocked prescriptions for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving blocked prescriptions.");
        }
    }

    /// <summary>Gets a single interaction screening with all findings.</summary>
    [HttpGet("{screeningId}")]
    public async Task<ActionResult<InteractionScreeningState>> GetScreening(string patientId, string screeningId)
    {
        try
        {
            InteractionScreeningState state = await GetWorkflow(patientId).GetInteractionScreeningAsync(screeningId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Screening '{screeningId}' not found." });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screening {ScreeningId} for patient {PatientId}", screeningId, patientId);
            return StatusCode(500, "An error occurred while retrieving the screening.");
        }
    }

    /// <summary>Gets the interaction screening for a specific prescription.</summary>
    [HttpGet("prescription/{prescriptionId}")]
    public async Task<ActionResult<InteractionScreeningState>> GetForPrescription(string patientId, string prescriptionId)
    {
        try
        {
            InteractionScreeningState state = await GetWorkflow(patientId).GetInteractionScreeningForPrescriptionAsync(prescriptionId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"No screening found for prescription '{prescriptionId}'." });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screening for prescription {PrescriptionId} patient {PatientId}", prescriptionId, patientId);
            return StatusCode(500, "An error occurred while retrieving the screening.");
        }
    }

    /// <summary>Checks whether a prescription is cleared for fill.</summary>
    [HttpGet("prescription/{prescriptionId}/cleared")]
    public async Task<ActionResult<FillClearedResponse>> IsClearedForFill(string patientId, string prescriptionId)
    {
        try
        {
            bool cleared = await GetWorkflow(patientId).IsPrescriptionClearedForFillAsync(prescriptionId);
            return Ok(new FillClearedResponse { PrescriptionId = prescriptionId, Cleared = cleared });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking fill clearance for prescription {PrescriptionId} patient {PatientId}", prescriptionId, patientId);
            return StatusCode(500, "An error occurred while checking fill clearance.");
        }
    }

    // ── POST endpoints ──────────────────────────────────────────────────────

    /// <summary>Screens a prescription for drug-drug interactions.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScreeningResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> ScreenPrescription(
        string patientId,
        [FromBody] ScreenPrescriptionRequest request)
    {
        try
        {
            string screeningId = await GetWorkflow(patientId).ScreenPrescriptionForInteractionsAsync(
                request.PrescriptionId,
                request.DrugName,
                request.NewDrugIngredients,
                request.ExistingMedicationIngredients,
                request.ScreenedBy);

            _logger.LogInformation("Interaction screening {ScreeningId} completed for Rx {PrescriptionId} patient {PatientId}",
                screeningId, request.PrescriptionId, patientId);

            return Created(
                $"api/patient/{patientId}/interactionscreening/{screeningId}",
                new ScreeningResponse { Id = screeningId, Message = "Interaction screening completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error screening prescription {PrescriptionId} patient {PatientId}",
                request.PrescriptionId, patientId);
            return StatusCode(500, "An error occurred while screening the prescription.");
        }
    }

    /// <summary>Overrides a blocking interaction with documented clinical reason.</summary>
    [HttpPost("{screeningId}/override")]
    public async Task<IActionResult> OverrideInteraction(
        string patientId,
        string screeningId,
        [FromBody] OverrideInteractionRequest request)
    {
        try
        {
            await GetWorkflow(patientId).OverrideInteractionBlockAsync(
                screeningId, request.FindingIndex, request.PharmacistId, request.Reason);

            _logger.LogInformation("Interaction override on screening {ScreeningId} finding {Index} by {PharmacistId}",
                screeningId, request.FindingIndex, request.PharmacistId);

            return Ok(new { ScreeningId = screeningId, Message = $"Interaction at index {request.FindingIndex} overridden." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error overriding interaction on screening {ScreeningId} patient {PatientId}",
                screeningId, patientId);
            return StatusCode(500, "An error occurred while overriding the interaction.");
        }
    }
}

// ── Request/Response DTOs ───────────────────────────────────────────────────

public record ScreenPrescriptionRequest
{
    public string PrescriptionId { get; init; } = string.Empty;
    public string DrugName { get; init; } = string.Empty;
    public List<DrugIngredient> NewDrugIngredients { get; init; } = new();
    public List<DrugIngredient> ExistingMedicationIngredients { get; init; } = new();
    public string? ScreenedBy { get; init; }
}

public record OverrideInteractionRequest
{
    public int FindingIndex { get; init; }
    public string PharmacistId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public class ScreeningResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class FillClearedResponse
{
    public string PrescriptionId { get; set; } = string.Empty;
    public bool Cleared { get; set; }
}
