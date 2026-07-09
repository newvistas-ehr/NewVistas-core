// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Nursing REST API — VistA Files #210-212 (NURSING UNIT, NURSING PATIENT).
/// Patient-centric routes: /api/nursing/{patientId}/...
/// Unit-centric routes:    /api/nursing/units/...
/// </summary>
[Authorize]
[ApiController]
public class NursingController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<NursingController> _log;

    public NursingController(IGrainFactory gf, ILogger<NursingController> log)
    {
        _gf  = gf;
        _log = log;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _gf.GetGrain<IPatientWorkflowGrain>(patientId);

    // Bed/unit truth lives on the inpatient unit grain ("UNIT:{institutionId}:{unitId}");
    // the per-institution capacity grain is its directory. Default institution "500"
    // keeps existing single-site callers working unchanged.
    private IInpatientUnitGrain UnitGrain(string unitId, string institutionId = "500")
        => _gf.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    private IBedCapacityGrain CapacityGrain(string institutionId = "500")
        => _gf.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");

    // ─── Assessments ──────────────────────────────────────────────────────────

    /// <summary>GET /api/nursing/{patientId}/assessments — list all assessments (newest first).</summary>
    [HttpGet("api/nursing/{patientId}/assessments")]
    public async Task<IActionResult> GetAssessments(string patientId)
    {
        try
        {
            List<NursingAssessmentIndexEntry> list =
                await Workflow(Uri.UnescapeDataString(patientId)).GetNursingAssessmentsAsync();
            return Ok(list.Select(ToAssessmentSummaryDto));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving nursing assessments for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving nursing assessments.");
        }
    }

    /// <summary>GET /api/nursing/{patientId}/assessments/{assessmentId} — full assessment detail.</summary>
    [HttpGet("api/nursing/{patientId}/assessments/{assessmentId}")]
    public async Task<IActionResult> GetAssessment(string patientId, string assessmentId)
    {
        try
        {
            NursingAssessmentState state =
                await Workflow(Uri.UnescapeDataString(patientId)).GetNursingAssessmentAsync(assessmentId);
            return Ok(state);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error retrieving nursing assessment.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/assessments — create a nursing assessment.</summary>
    [HttpPost("api/nursing/{patientId}/assessments")]
    public async Task<IActionResult> CreateAssessment(
        string patientId, [FromBody] CreateNursingAssessmentRequest req)
    {
        try
        {
            string id = await Workflow(Uri.UnescapeDataString(patientId))
                .CreateNursingAssessmentAsync(
                    req.AssessmentDateTime,
                    req.AssessmentType,
                    req.NurseId,
                    req.NurseName,
                    req.LocationId,
                    req.LocationName,
                    req.LevelOfConsciousness,
                    req.Orientation,
                    req.BreathSounds,
                    req.OxygenTherapy,
                    req.SpO2,
                    req.HeartRhythm,
                    req.Edema,
                    req.SkinIntegrity,
                    req.BradenScore,
                    req.PainScore,
                    req.PainLocation,
                    req.BowelSounds,
                    req.AppetiteAssessment,
                    req.UrineOutput,
                    req.HasFoley,
                    req.AnxietyLevel,
                    req.Mood,
                    req.MorseScore,
                    req.FallRiskLevel,
                    req.FallPrecautions,
                    req.AdlMobility,
                    req.NarrativeNotes);
            return Created($"api/nursing/{patientId}/assessments/{id}", new { assessmentId = id });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error creating nursing assessment for {PatientId}", patientId);
            return StatusCode(500, "Error creating nursing assessment.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/assessments/{assessmentId}/sign — sign an assessment.</summary>
    [HttpPost("api/nursing/{patientId}/assessments/{assessmentId}/sign")]
    public async Task<IActionResult> SignAssessment(
        string patientId, string assessmentId, [FromBody] SignAssessmentRequest req)
    {
        try
        {
            await Workflow(Uri.UnescapeDataString(patientId))
                .SignNursingAssessmentAsync(assessmentId, req.NurseId, req.NurseName);
            return Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error signing assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error signing assessment.");
        }
    }

    // ─── Care Plan ────────────────────────────────────────────────────────────

    /// <summary>GET /api/nursing/{patientId}/careplan — full nursing care plan.</summary>
    [HttpGet("api/nursing/{patientId}/careplan")]
    public async Task<IActionResult> GetCarePlan(string patientId)
    {
        try
        {
            NursingCarePlanState state =
                await Workflow(Uri.UnescapeDataString(patientId)).GetNursingCarePlanAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving care plan for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving nursing care plan.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/careplan/diagnoses — add a nursing diagnosis.</summary>
    [HttpPost("api/nursing/{patientId}/careplan/diagnoses")]
    public async Task<IActionResult> AddDiagnosis(
        string patientId, [FromBody] AddNursingDiagnosisRequest req)
    {
        try
        {
            string problemId = await Workflow(Uri.UnescapeDataString(patientId))
                .AddNursingDiagnosisAsync(
                    req.NursingDiagnosis,
                    req.RelatedTo,
                    req.EvidencedBy,
                    req.Priority,
                    req.NurseId,
                    req.NurseName);
            return Created(string.Empty, new { problemId });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding nursing diagnosis for {PatientId}", patientId);
            return StatusCode(500, "Error adding nursing diagnosis.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/careplan/diagnoses/{problemId}/goals — add a goal.</summary>
    [HttpPost("api/nursing/{patientId}/careplan/diagnoses/{problemId}/goals")]
    public async Task<IActionResult> AddGoal(
        string patientId, string problemId, [FromBody] AddCarePlanGoalRequest req)
    {
        try
        {
            await Workflow(Uri.UnescapeDataString(patientId))
                .AddCarePlanGoalAsync(problemId, req.GoalText, req.TargetDate);
            return Created(string.Empty, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding care plan goal for {PatientId}", patientId);
            return StatusCode(500, "Error adding care plan goal.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/careplan/diagnoses/{problemId}/interventions — add an intervention.</summary>
    [HttpPost("api/nursing/{patientId}/careplan/diagnoses/{problemId}/interventions")]
    public async Task<IActionResult> AddIntervention(
        string patientId, string problemId, [FromBody] AddCarePlanInterventionRequest req)
    {
        try
        {
            await Workflow(Uri.UnescapeDataString(patientId))
                .AddCarePlanInterventionAsync(
                    problemId, req.InterventionText, req.Frequency, req.NurseId, req.NurseName);
            return Created(string.Empty, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding care plan intervention for {PatientId}", patientId);
            return StatusCode(500, "Error adding care plan intervention.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/careplan/diagnoses/{problemId}/outcomes — record outcome evaluation.</summary>
    [HttpPost("api/nursing/{patientId}/careplan/diagnoses/{problemId}/outcomes")]
    public async Task<IActionResult> RecordOutcome(
        string patientId, string problemId, [FromBody] RecordCarePlanOutcomeRequest req)
    {
        try
        {
            await Workflow(Uri.UnescapeDataString(patientId))
                .RecordCarePlanOutcomeAsync(
                    problemId, req.Rating, req.EvaluatedById, req.EvaluatedByName, req.Notes);
            return Created(string.Empty, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording outcome for {PatientId}", patientId);
            return StatusCode(500, "Error recording care plan outcome.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/careplan/diagnoses/{problemId}/resolve — resolve a diagnosis.</summary>
    [HttpPost("api/nursing/{patientId}/careplan/diagnoses/{problemId}/resolve")]
    public async Task<IActionResult> ResolveDiagnosis(
        string patientId, string problemId, [FromBody] ResolveDiagnosisRequest req)
    {
        try
        {
            await Workflow(Uri.UnescapeDataString(patientId))
                .ResolveNursingDiagnosisAsync(problemId, req.ResolutionNotes);
            return Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error resolving diagnosis {ProblemId}", problemId);
            return StatusCode(500, "Error resolving nursing diagnosis.");
        }
    }

    // ─── Acuity ───────────────────────────────────────────────────────────────

    /// <summary>GET /api/nursing/{patientId}/acuity — current acuity and history.</summary>
    [HttpGet("api/nursing/{patientId}/acuity")]
    public async Task<IActionResult> GetAcuity(string patientId)
    {
        try
        {
            NursingAcuityState state =
                await Workflow(Uri.UnescapeDataString(patientId)).GetNursingAcuityAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving acuity for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving nursing acuity.");
        }
    }

    /// <summary>POST /api/nursing/{patientId}/acuity — record an acuity classification.</summary>
    [HttpPost("api/nursing/{patientId}/acuity")]
    public async Task<IActionResult> RecordAcuity(
        string patientId, [FromBody] RecordAcuityRequest req)
    {
        try
        {
            await Workflow(Uri.UnescapeDataString(patientId))
                .RecordNursingAcuityAsync(
                    req.Level, req.Score, req.NurseId, req.NurseName, req.Shift, req.Notes);
            return Created(string.Empty, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording acuity for {PatientId}", patientId);
            return StatusCode(500, "Error recording nursing acuity.");
        }
    }

    // ─── Nursing Units ────────────────────────────────────────────────────────

    /// <summary>GET /api/nursing/units — list all nursing units (live capacity directory).</summary>
    [HttpGet("api/nursing/units")]
    public async Task<IActionResult> GetUnits([FromQuery] string institutionId = "500")
    {
        try
        {
            List<UnitCapacitySummary> units = await CapacityGrain(institutionId).GetUnitsAsync();
            return Ok(units);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving nursing units");
            return StatusCode(500, "Error retrieving nursing units.");
        }
    }

    /// <summary>GET /api/nursing/units/{unitId} — full unit state with rooms, beds, and nursing assignments.</summary>
    [HttpGet("api/nursing/units/{unitId}")]
    public async Task<IActionResult> GetUnit(string unitId, [FromQuery] string institutionId = "500")
    {
        try
        {
            InpatientUnitState state = await UnitGrain(unitId, institutionId).GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving unit {UnitId}", unitId);
            return StatusCode(500, "Error retrieving nursing unit.");
        }
    }

    /// <summary>
    /// POST /api/nursing/units — configure a unit; when it has no beds yet, TotalBeds
    /// numbered Regular beds are created (small-site quick setup).
    /// </summary>
    [HttpPost("api/nursing/units")]
    public async Task<IActionResult> CreateUnit([FromBody] CreateNursingUnitRequest req,
        [FromQuery] string institutionId = "500")
    {
        try
        {
            IInpatientUnitGrain unit = UnitGrain(req.UnitId, institutionId);
            await unit.ConfigureUnitAsync(req.UnitName, req.UnitType, null);
            InpatientUnitState state = await unit.GetAsync();
            for (int i = state.Beds.Count + 1; i <= req.TotalBeds; i++)
                await unit.AddBedAsync(i.ToString(), null, BedType.Regular);
            return Created($"api/nursing/units/{req.UnitId}", new { req.UnitId });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error creating nursing unit {UnitId}", req.UnitId);
            return StatusCode(500, "Error creating nursing unit.");
        }
    }

    /// <summary>POST /api/nursing/units/{unitId}/charge-nurse — set charge nurse.</summary>
    [HttpPost("api/nursing/units/{unitId}/charge-nurse")]
    public async Task<IActionResult> SetChargeNurse(
        string unitId, [FromBody] SetChargeNurseRequest req, [FromQuery] string institutionId = "500")
    {
        try
        {
            await UnitGrain(unitId, institutionId).SetChargeNurseAsync(req.NurseId, req.NurseName);
            return Ok();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting charge nurse on unit {UnitId}", unitId);
            return StatusCode(500, "Error setting charge nurse.");
        }
    }

    /// <summary>
    /// POST /api/nursing/units/{unitId}/beds/{bed}/nurse — assign the bed's attending nurse.
    /// (Patient placement is deliberately NOT a nursing-API operation — admissions and
    /// discharges go through the ADT workflow, which owns bed occupancy.)
    /// </summary>
    [HttpPost("api/nursing/units/{unitId}/beds/{bed}/nurse")]
    public async Task<IActionResult> AssignBedNurse(
        string unitId, string bed, [FromBody] AssignBedNurseRequest req, [FromQuery] string institutionId = "500")
    {
        try
        {
            await UnitGrain(unitId, institutionId).AssignBedNurseAsync(bed, req.NurseId, req.NurseName);
            return Ok();
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error assigning nurse to bed {Bed} on unit {UnitId}", bed, unitId);
            return StatusCode(500, "Error assigning bed nurse.");
        }
    }

    /// <summary>POST /api/nursing/units/{unitId}/beds/{bed}/acuity — set bed acuity level.</summary>
    [HttpPost("api/nursing/units/{unitId}/beds/{bed}/acuity")]
    public async Task<IActionResult> UpdateBedAcuity(
        string unitId, string bed, [FromBody] UpdateBedAcuityRequest req, [FromQuery] string institutionId = "500")
    {
        try
        {
            await UnitGrain(unitId, institutionId).UpdateBedAcuityAsync(bed, req.Level);
            return Ok();
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error updating acuity for bed {Bed} on unit {UnitId}", bed, unitId);
            return StatusCode(500, "Error updating bed acuity.");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static AssessmentSummaryDto ToAssessmentSummaryDto(NursingAssessmentIndexEntry e) =>
        new(e.AssessmentId, e.AssessmentDateTime, e.AssessmentType,
            e.NurseName, e.Status.ToString(), e.LocationName,
            e.PainScore, e.MorseScore, e.BradenScore);

    // ─── Request / Response DTOs ──────────────────────────────────────────────

    public record CreateNursingAssessmentRequest(
        DateTime AssessmentDateTime,
        string AssessmentType,
        string NurseId,
        string NurseName,
        string? LocationId,
        string? LocationName,
        string? LevelOfConsciousness,
        List<string>? Orientation,
        string? BreathSounds,
        string? OxygenTherapy,
        decimal? SpO2,
        string? HeartRhythm,
        string? Edema,
        string? SkinIntegrity,
        int? BradenScore,
        int? PainScore,
        string? PainLocation,
        string? BowelSounds,
        string? AppetiteAssessment,
        decimal? UrineOutput,
        bool HasFoley,
        string? AnxietyLevel,
        string? Mood,
        int? MorseScore,
        string? FallRiskLevel,
        List<string>? FallPrecautions,
        string? AdlMobility,
        string? NarrativeNotes);

    public record SignAssessmentRequest(string NurseId, string NurseName);

    public record AddNursingDiagnosisRequest(
        string NursingDiagnosis,
        string? RelatedTo,
        string? EvidencedBy,
        int? Priority,
        string? NurseId,
        string? NurseName);

    public record AddCarePlanGoalRequest(string GoalText, DateTime? TargetDate);

    public record AddCarePlanInterventionRequest(
        string InterventionText,
        string? Frequency,
        string? NurseId,
        string? NurseName);

    public record RecordCarePlanOutcomeRequest(
        NursingOutcomeRating Rating,
        string EvaluatedById,
        string EvaluatedByName,
        string? Notes);

    public record ResolveDiagnosisRequest(string? ResolutionNotes);

    public record RecordAcuityRequest(
        AcuityLevel Level,
        int? Score,
        string NurseId,
        string NurseName,
        string? Shift,
        string? Notes);

    public record CreateNursingUnitRequest(
        string UnitId,
        string UnitName,
        string? UnitType,
        int TotalBeds);

    public record SetChargeNurseRequest(string NurseId, string NurseName);

    public record AssignBedNurseRequest(string? NurseId, string? NurseName);

    public record UpdateBedAcuityRequest(AcuityLevel Level);

    public record AssessmentSummaryDto(
        string AssessmentId,
        DateTime AssessmentDateTime,
        string AssessmentType,
        string NurseName,
        string Status,
        string? LocationName,
        int? PainScore,
        int? MorseScore,
        int? BradenScore);
}
