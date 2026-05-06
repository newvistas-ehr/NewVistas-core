// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Nursing Care Plans — NANDA diagnoses, goals, interventions, outcomes.
/// VistA File #212 (NURSING PATIENT).
/// </summary>
[Authorize]
[ApiController]
[Route("api/nursing/careplan")]
[Produces("application/json")]
public class NursingCarePlanController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<NursingCarePlanController> _logger;

    public NursingCarePlanController(IGrainFactory grainFactory, ILogger<NursingCarePlanController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetCarePlan(string patientId)
    {
        try { return Ok(await W(patientId).GetNursingCarePlanAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting care plan for {PatientId}", patientId); return StatusCode(500, "Failed to get care plan."); }
    }

    [HttpPost("{patientId}/diagnoses")]
    public async Task<IActionResult> AddDiagnosis(string patientId, [FromBody] AddDiagnosisRequest req)
    {
        try
        {
            string id = await W(patientId).AddNursingDiagnosisAsync(
                req.NursingDiagnosis, req.RelatedTo, req.EvidencedBy, req.Priority, req.NurseId, req.NurseName);
            return Created("", new { ProblemId = id });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error adding diagnosis for {PatientId}", patientId); return StatusCode(500, "Failed to add diagnosis."); }
    }

    [HttpPost("{patientId}/diagnoses/{problemId}/goals")]
    public async Task<IActionResult> AddGoal(string patientId, string problemId, [FromBody] AddGoalRequest req)
    {
        try { await W(patientId).AddCarePlanGoalAsync(problemId, req.GoalText, req.TargetDate); return Created("", null); }
        catch (Exception ex) { _logger.LogError(ex, "Error adding goal"); return StatusCode(500, "Failed to add goal."); }
    }

    [HttpPost("{patientId}/diagnoses/{problemId}/interventions")]
    public async Task<IActionResult> AddIntervention(string patientId, string problemId, [FromBody] AddInterventionRequest req)
    {
        try { await W(patientId).AddCarePlanInterventionAsync(problemId, req.InterventionText, req.Frequency, req.NurseId, req.NurseName); return Created("", null); }
        catch (Exception ex) { _logger.LogError(ex, "Error adding intervention"); return StatusCode(500, "Failed to add intervention."); }
    }

    [HttpPost("{patientId}/diagnoses/{problemId}/outcomes")]
    public async Task<IActionResult> RecordOutcome(string patientId, string problemId, [FromBody] RecordOutcomeRequest req)
    {
        try { await W(patientId).RecordCarePlanOutcomeAsync(problemId, req.Rating, req.EvaluatedById, req.EvaluatedByName, req.Notes); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error recording outcome"); return StatusCode(500, "Failed to record outcome."); }
    }

    [HttpPut("{patientId}/diagnoses/{problemId}/goals/{goalId}/status")]
    public async Task<IActionResult> UpdateGoalStatus(string patientId, string problemId, string goalId, [FromBody] UpdateGoalStatusRequest req)
    {
        try { await W(patientId).UpdateCarePlanGoalStatusAsync(problemId, goalId, req.Status); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error updating goal status"); return StatusCode(500, "Failed to update goal status."); }
    }

    [HttpPost("{patientId}/diagnoses/{problemId}/resolve")]
    public async Task<IActionResult> ResolveDiagnosis(string patientId, string problemId, [FromBody] ResolveRequest? req)
    {
        try { await W(patientId).ResolveNursingDiagnosisAsync(problemId, req?.ResolutionNotes); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error resolving diagnosis"); return StatusCode(500, "Failed to resolve diagnosis."); }
    }

    public record AddDiagnosisRequest { public string NursingDiagnosis { get; init; } = string.Empty; public string? RelatedTo { get; init; } public string? EvidencedBy { get; init; } public int? Priority { get; init; } public string? NurseId { get; init; } public string? NurseName { get; init; } }
    public record AddGoalRequest { public string GoalText { get; init; } = string.Empty; public DateTime? TargetDate { get; init; } }
    public record AddInterventionRequest { public string InterventionText { get; init; } = string.Empty; public string? Frequency { get; init; } public string? NurseId { get; init; } public string? NurseName { get; init; } }
    public record RecordOutcomeRequest { public NursingOutcomeRating Rating { get; init; } public string EvaluatedById { get; init; } = string.Empty; public string EvaluatedByName { get; init; } = string.Empty; public string? Notes { get; init; } }
    public record UpdateGoalStatusRequest { public NursingGoalStatus Status { get; init; } }
    public record ResolveRequest { public string? ResolutionNotes { get; init; } }
}
