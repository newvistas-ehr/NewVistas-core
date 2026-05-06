// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Drug Utilization Review (DUR) API — VistA PSO package.
///
/// Provides pre-fill safety checks for prescriptions: duplicate drug, duplicate
/// therapy, drug-allergy contraindication, drug-drug interaction, max dose,
/// days supply, refill timing, age-based dosing, renal/hepatic adjustments,
/// and controlled substance enforcement.
///
/// MUMPS references: PSOORED.m, PSODRDUP.m, PSOVER1.m, DRGINT.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/dur")]
[Produces("application/json")]
public class DurController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DurController> _logger;

    public DurController(IGrainFactory grainFactory, ILogger<DurController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── GET endpoints ───────────────────────────────────────────────────────

    /// <summary>Gets all DUR assessments for a patient.</summary>
    [HttpGet]
    public async Task<ActionResult<List<DurAssessmentIndexEntry>>> GetAssessments(string patientId)
    {
        try
        {
            List<DurAssessmentIndexEntry> results = await GetWorkflow(patientId).GetDurAssessmentsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DUR assessments for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving DUR assessments.");
        }
    }

    /// <summary>Gets DUR assessments pending pharmacist review.</summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<DurAssessmentIndexEntry>>> GetPendingReviews(string patientId)
    {
        try
        {
            List<DurAssessmentIndexEntry> results = await GetWorkflow(patientId).GetPendingDurReviewsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending DUR reviews for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving pending DUR reviews.");
        }
    }

    /// <summary>Gets a single DUR assessment with all check details.</summary>
    [HttpGet("{assessmentId}")]
    public async Task<ActionResult<DurAssessmentState>> GetAssessment(string patientId, string assessmentId)
    {
        try
        {
            DurAssessmentState state = await GetWorkflow(patientId).GetDurAssessmentAsync(assessmentId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"DUR assessment '{assessmentId}' not found." });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DUR assessment {AssessmentId} for patient {PatientId}", assessmentId, patientId);
            return StatusCode(500, "An error occurred while retrieving the DUR assessment.");
        }
    }

    /// <summary>Gets the DUR assessment for a specific prescription.</summary>
    [HttpGet("prescription/{prescriptionId}")]
    public async Task<ActionResult<DurAssessmentIndexEntry>> GetForPrescription(string patientId, string prescriptionId)
    {
        try
        {
            DurAssessmentIndexEntry? entry = await GetWorkflow(patientId).GetDurForPrescriptionAsync(prescriptionId);
            if (entry is null)
                return NotFound(new { Message = $"No DUR assessment found for prescription '{prescriptionId}'." });
            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DUR for prescription {PrescriptionId} patient {PatientId}", prescriptionId, patientId);
            return StatusCode(500, "An error occurred while retrieving the DUR assessment.");
        }
    }

    // ── POST endpoints ──────────────────────────────────────────────────────

    /// <summary>Performs a full DUR assessment for a prescription.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DurResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> PerformDur(
        string patientId,
        [FromBody] PerformDurRequest request)
    {
        try
        {
            string assessmentId = await GetWorkflow(patientId).PerformDurAsync(
                request.PrescriptionId,
                request.DrugName,
                request.DrugId,
                request.DrugClass,
                request.Dosage,
                request.Route,
                request.Schedule,
                request.DaysSupply,
                request.Quantity,
                request.MaxDaysSupply,
                request.MaxQuantity,
                request.IsControlledSubstance,
                request.DeaSchedule,
                request.PerformedBy,
                request.IngredientIens,
                request.MaxDailyDoseMg);

            _logger.LogInformation("DUR assessment {AssessmentId} performed for prescription {PrescriptionId} patient {PatientId}",
                assessmentId, request.PrescriptionId, patientId);

            return Created(
                $"api/patient/{patientId}/dur/{assessmentId}",
                new DurResponse { Id = assessmentId, Message = "DUR assessment completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing DUR for prescription {PrescriptionId} patient {PatientId}",
                request.PrescriptionId, patientId);
            return StatusCode(500, "An error occurred while performing the DUR assessment.");
        }
    }

    /// <summary>Overrides a failed DUR check with documented clinical reason.</summary>
    [HttpPost("{assessmentId}/override")]
    public async Task<IActionResult> OverrideCheck(
        string patientId,
        string assessmentId,
        [FromBody] OverrideDurCheckRequest request)
    {
        try
        {
            await GetWorkflow(patientId).OverrideDurCheckAsync(
                assessmentId, request.CheckType, request.PharmacistId, request.Reason);

            _logger.LogInformation("DUR check {CheckType} overridden on assessment {AssessmentId} by {PharmacistId}",
                request.CheckType, assessmentId, request.PharmacistId);

            return Ok(new { AssessmentId = assessmentId, Message = $"Check {request.CheckType} overridden." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error overriding DUR check on assessment {AssessmentId} patient {PatientId}",
                assessmentId, patientId);
            return StatusCode(500, "An error occurred while overriding the DUR check.");
        }
    }

    /// <summary>Acknowledges DUR assessment results.</summary>
    [HttpPost("{assessmentId}/acknowledge")]
    public async Task<IActionResult> Acknowledge(
        string patientId,
        string assessmentId,
        [FromBody] AcknowledgeDurRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AcknowledgeDurAsync(assessmentId, request.PharmacistId, request.Notes);

            _logger.LogInformation("DUR assessment {AssessmentId} acknowledged by {PharmacistId}",
                assessmentId, request.PharmacistId);

            return Ok(new { AssessmentId = assessmentId, Message = "DUR assessment acknowledged." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging DUR assessment {AssessmentId} patient {PatientId}",
                assessmentId, patientId);
            return StatusCode(500, "An error occurred while acknowledging the DUR assessment.");
        }
    }
}

// ── Request/Response DTOs ───────────────────────────────────────────────────

public record PerformDurRequest
{
    public string PrescriptionId { get; init; } = string.Empty;
    public string DrugName { get; init; } = string.Empty;
    public string? DrugId { get; init; }
    public string? DrugClass { get; init; }
    public string? Dosage { get; init; }
    public string? Route { get; init; }
    public string? Schedule { get; init; }
    public int? DaysSupply { get; init; }
    public int? Quantity { get; init; }
    public int? MaxDaysSupply { get; init; }
    public int? MaxQuantity { get; init; }
    public bool IsControlledSubstance { get; init; }
    public string? DeaSchedule { get; init; }
    public string? PerformedBy { get; init; }
    public List<string>? IngredientIens { get; init; }
    public decimal? MaxDailyDoseMg { get; init; }
}

public record OverrideDurCheckRequest
{
    public DurCheckType CheckType { get; init; }
    public string PharmacistId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record AcknowledgeDurRequest
{
    public string PharmacistId { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public class DurResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
