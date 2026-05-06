// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Structured Pain Assessment — DVPRS, Wong-Baker, FLACC, NRS, CPOT, VAS.
/// </summary>
[Authorize]
[ApiController]
[Route("api/nursing/pain")]
[Produces("application/json")]
public class PainAssessmentController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PainAssessmentController> _logger;

    public PainAssessmentController(IGrainFactory grainFactory, ILogger<PainAssessmentController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}")]
    public async Task<IActionResult> Record(string patientId, [FromBody] RecordPainRequest req)
    {
        try
        {
            string id = await W(patientId).RecordPainAssessmentAsync(
                req.Tool, req.PainScore, req.PainLocation, req.PainCharacter, req.PainOnset,
                req.AggravatingFactors, req.AlleviatingFactors, req.Radiation,
                req.AcceptablePainLevel, req.DvprsSupplemental, req.FlaccComponents,
                req.InterventionProvided, req.NurseId, req.NurseName, req.Notes);
            return Created("", new { AssessmentId = id });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error recording pain for {PatientId}", patientId); return StatusCode(500, "Failed to record pain assessment."); }
    }

    [HttpPost("{patientId}/reassess")]
    public async Task<IActionResult> Reassess(string patientId, [FromBody] ReassessRequest req)
    {
        try
        {
            string id = await W(patientId).RecordPainReassessmentAsync(
                req.InitialAssessmentId, req.Tool, req.PostInterventionScore,
                req.MinutesSinceIntervention, req.InterventionProvided,
                req.NurseId, req.NurseName, req.Notes);
            return Created("", new { AssessmentId = id });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error recording reassessment"); return StatusCode(500, "Failed to record reassessment."); }
    }

    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetAll(string patientId)
    {
        try { return Ok(await W(patientId).GetPainAssessmentsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting assessments"); return StatusCode(500, "Failed to get pain assessments."); }
    }

    [HttpGet("{patientId}/{assessmentId}")]
    public async Task<IActionResult> Get(string patientId, string assessmentId)
    {
        try { return Ok(await W(patientId).GetPainAssessmentAsync(assessmentId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting assessment"); return StatusCode(500, "Failed to get pain assessment."); }
    }

    [HttpGet("{patientId}/latest")]
    public async Task<IActionResult> GetLatest(string patientId)
    {
        try
        {
            PainAssessmentIndexEntry? entry = await W(patientId).GetLatestPainAssessmentAsync();
            return entry is not null ? Ok(entry) : NotFound();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting latest"); return StatusCode(500, "Failed to get latest assessment."); }
    }

    public record RecordPainRequest
    {
        public PainAssessmentTool Tool { get; init; }
        public int PainScore { get; init; }
        public string? PainLocation { get; init; }
        public string? PainCharacter { get; init; }
        public string? PainOnset { get; init; }
        public string? AggravatingFactors { get; init; }
        public string? AlleviatingFactors { get; init; }
        public string? Radiation { get; init; }
        public int? AcceptablePainLevel { get; init; }
        public DvprsSupplemental? DvprsSupplemental { get; init; }
        public FlaccScore? FlaccComponents { get; init; }
        public string? InterventionProvided { get; init; }
        public string NurseId { get; init; } = string.Empty;
        public string NurseName { get; init; } = string.Empty;
        public string? Notes { get; init; }
    }

    public record ReassessRequest
    {
        public string InitialAssessmentId { get; init; } = string.Empty;
        public PainAssessmentTool Tool { get; init; }
        public int PostInterventionScore { get; init; }
        public int MinutesSinceIntervention { get; init; }
        public string? InterventionProvided { get; init; }
        public string NurseId { get; init; } = string.Empty;
        public string NurseName { get; init; } = string.Empty;
        public string? Notes { get; init; }
    }
}
