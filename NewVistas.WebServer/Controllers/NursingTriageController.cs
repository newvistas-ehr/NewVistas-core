// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Nursing Intake/Triage Assessment with ESI scoring.
/// </summary>
[Authorize]
[ApiController]
[Route("api/nursing/triage")]
[Produces("application/json")]
public class NursingTriageController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<NursingTriageController> _logger;

    public NursingTriageController(IGrainFactory grainFactory, ILogger<NursingTriageController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}")]
    public async Task<IActionResult> CreateTriage(string patientId, [FromBody] CreateTriageRequest req)
    {
        try
        {
            string id = await W(patientId).CreateTriageAssessmentAsync(
                req.TriageDateTime ?? DateTime.UtcNow, req.NurseId, req.NurseName,
                req.LocationId, req.LocationName, req.ChiefComplaint, req.HistoryOfPresentIllness,
                req.Temperature, req.HeartRate, req.RespiratoryRate,
                req.SystolicBP, req.DiastolicBP, req.SpO2, req.PainScore,
                req.TriageLevel, req.ExpectedResources,
                req.LevelOfConsciousness, req.ModeOfArrival,
                req.IsAcuteDistress, req.ArrivedByAmbulance, req.Notes);
            return Created("", new { TriageId = id });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating triage for {PatientId}", patientId); return StatusCode(500, "Failed to create triage."); }
    }

    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetTriages(string patientId)
    {
        try { return Ok(await W(patientId).GetTriageAssessmentsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting triages"); return StatusCode(500, "Failed to get triages."); }
    }

    [HttpGet("{patientId}/{triageId}")]
    public async Task<IActionResult> GetTriage(string patientId, string triageId)
    {
        try { return Ok(await W(patientId).GetTriageAssessmentAsync(triageId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting triage {TriageId}", triageId); return StatusCode(500, "Failed to get triage."); }
    }

    [HttpPost("{patientId}/{triageId}/sign")]
    public async Task<IActionResult> Sign(string patientId, string triageId, [FromBody] SignRequest req)
    {
        try { await W(patientId).SignTriageAssessmentAsync(triageId, req.NurseId, req.NurseName); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error signing triage"); return StatusCode(500, "Failed to sign triage."); }
    }

    [HttpPost("{patientId}/{triageId}/disposition")]
    public async Task<IActionResult> SetDisposition(string patientId, string triageId, [FromBody] DispositionRequest req)
    {
        try { await W(patientId).SetTriageDispositionAsync(triageId, req.Disposition); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error setting disposition"); return StatusCode(500, "Failed to set disposition."); }
    }

    public record CreateTriageRequest
    {
        public DateTime? TriageDateTime { get; init; }
        public string NurseId { get; init; } = string.Empty;
        public string NurseName { get; init; } = string.Empty;
        public string? LocationId { get; init; }
        public string? LocationName { get; init; }
        public string ChiefComplaint { get; init; } = string.Empty;
        public string? HistoryOfPresentIllness { get; init; }
        public decimal? Temperature { get; init; }
        public int? HeartRate { get; init; }
        public int? RespiratoryRate { get; init; }
        public int? SystolicBP { get; init; }
        public int? DiastolicBP { get; init; }
        public decimal? SpO2 { get; init; }
        public int? PainScore { get; init; }
        public TriageLevel TriageLevel { get; init; }
        public int? ExpectedResources { get; init; }
        public string? LevelOfConsciousness { get; init; }
        public string? ModeOfArrival { get; init; }
        public bool IsAcuteDistress { get; init; }
        public bool ArrivedByAmbulance { get; init; }
        public string? Notes { get; init; }
    }
    public record SignRequest { public string NurseId { get; init; } = string.Empty; public string NurseName { get; init; } = string.Empty; }
    public record DispositionRequest { public TriageDisposition Disposition { get; init; } }
}
