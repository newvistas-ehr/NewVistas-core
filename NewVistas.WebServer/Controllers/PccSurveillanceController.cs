// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/pcc-surveillance")]
public class PccSurveillanceController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PccSurveillanceController> _logger;

    public PccSurveillanceController(IGrainFactory grainFactory, ILogger<PccSurveillanceController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPccSurveillanceConfigIndexGrain ConfigIndex =>
        _grainFactory.GetGrain<IPccSurveillanceConfigIndexGrain>("PCC-SURV-CONFIG-IDX");

    private IPccSurveillanceMatchIndexGrain MatchIndex =>
        _grainFactory.GetGrain<IPccSurveillanceMatchIndexGrain>("PCC-SURV-MATCH-IDX");

    private IPccSurveillanceConfigGrain GetConfigGrain(string configId) =>
        _grainFactory.GetGrain<IPccSurveillanceConfigGrain>($"PCC-SURV-CONFIG:{configId}");

    private IPccSurveillanceMatchGrain GetMatchGrain(string matchId) =>
        _grainFactory.GetGrain<IPccSurveillanceMatchGrain>($"PCC-SURV-MATCH:{matchId}");

    // ── Configs ──────────────────────────────────────────────────────────────

    [HttpGet("configs")]
    public async Task<IActionResult> GetAllConfigs()
    {
        try
        {
            List<PccSurveillanceConfigIndexEntry> result = await ConfigIndex.GetAllAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all PCC surveillance configs");
            return StatusCode(500, "An error occurred retrieving surveillance configs.");
        }
    }

    [HttpGet("configs/active")]
    public async Task<IActionResult> GetActiveConfigs()
    {
        try
        {
            List<PccSurveillanceConfigIndexEntry> result = await ConfigIndex.GetActiveAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active PCC surveillance configs");
            return StatusCode(500, "An error occurred retrieving active surveillance configs.");
        }
    }

    [HttpGet("configs/{configId}")]
    public async Task<IActionResult> GetConfig(string configId)
    {
        try
        {
            PccSurveillanceConfigState result = await GetConfigGrain(configId).GetAsync();
            if (string.IsNullOrEmpty(result.ConditionName))
                return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PCC surveillance config {ConfigId}", configId);
            return StatusCode(500, "An error occurred retrieving the surveillance config.");
        }
    }

    [HttpPost("configs")]
    public async Task<IActionResult> SaveConfig([FromBody] SavePccSurveillanceConfigRequest req)
    {
        try
        {
            string configId = Guid.NewGuid().ToString();
            IPccSurveillanceConfigGrain grain = GetConfigGrain(configId);
            await grain.SaveAsync(
                req.ConditionName, req.Classification,
                req.Criteria, req.RequiredVisitTypes,
                req.DetectComorbidities, req.CaptureVitals,
                req.ScanWindowDays,
                req.Jurisdictions, req.ReportingTimeframe,
                req.IsActive);

            await ConfigIndex.UpsertAsync(new PccSurveillanceConfigIndexEntry
            {
                ConfigId = configId,
                ConditionName = req.ConditionName,
                Classification = req.Classification,
                CriteriaCount = req.Criteria?.Count ?? 0,
                IsActive = req.IsActive,
            });

            return Created($"/api/pcc-surveillance/configs/{configId}",
                new PccSurveillanceResponse { Id = configId, Message = "Config created." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving PCC surveillance config");
            return StatusCode(500, "An error occurred saving the surveillance config.");
        }
    }

    [HttpPost("configs/{configId}/criteria")]
    public async Task<IActionResult> AddCriterion(string configId, [FromBody] AddPccSurveillanceCriterionRequest req)
    {
        try
        {
            PccSurveillanceCriterion criterion = new()
            {
                Code = req.Code,
                CodeSystem = req.CodeSystem,
                Description = req.Description,
                MatchType = req.MatchType,
                ValueOperator = req.ValueOperator,
                ThresholdValue = req.ThresholdValue,
            };
            IPccSurveillanceConfigGrain grain = GetConfigGrain(configId);
            await grain.AddCriterionAsync(criterion);

            // Update index with new criteria count
            PccSurveillanceConfigState state = await grain.GetAsync();
            await ConfigIndex.UpsertAsync(new PccSurveillanceConfigIndexEntry
            {
                ConfigId = configId,
                ConditionName = state.ConditionName,
                Classification = state.Classification,
                CriteriaCount = state.Criteria.Count,
                IsActive = state.IsActive,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding criterion to PCC surveillance config {ConfigId}", configId);
            return StatusCode(500, "An error occurred adding the criterion.");
        }
    }

    [HttpPut("configs/{configId}/active")]
    public async Task<IActionResult> SetActive(string configId, [FromBody] bool isActive)
    {
        try
        {
            IPccSurveillanceConfigGrain grain = GetConfigGrain(configId);
            await grain.SetActiveAsync(isActive);

            PccSurveillanceConfigState state = await grain.GetAsync();
            await ConfigIndex.UpsertAsync(new PccSurveillanceConfigIndexEntry
            {
                ConfigId = configId,
                ConditionName = state.ConditionName,
                Classification = state.Classification,
                CriteriaCount = state.Criteria.Count,
                IsActive = isActive,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active status for PCC surveillance config {ConfigId}", configId);
            return StatusCode(500, "An error occurred updating the config active status.");
        }
    }

    // ── Matches ──────────────────────────────────────────────────────────────

    [HttpGet("matches")]
    public async Task<IActionResult> GetAllMatches([FromQuery] PccSurveillanceMatchStatus? status)
    {
        try
        {
            List<PccSurveillanceMatchIndexEntry> result;
            if (status.HasValue)
                result = await MatchIndex.GetByStatusAsync(status.Value);
            else
                result = await MatchIndex.GetAllAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PCC surveillance matches");
            return StatusCode(500, "An error occurred retrieving surveillance matches.");
        }
    }

    [HttpGet("matches/condition/{conditionName}")]
    public async Task<IActionResult> GetMatchesByCondition(string conditionName)
    {
        try
        {
            List<PccSurveillanceMatchIndexEntry> result = await MatchIndex.GetByConditionAsync(conditionName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving matches for condition {ConditionName}", conditionName);
            return StatusCode(500, "An error occurred retrieving matches by condition.");
        }
    }

    [HttpGet("matches/{matchId}")]
    public async Task<IActionResult> GetMatch(string matchId)
    {
        try
        {
            PccSurveillanceMatchState result = await GetMatchGrain(matchId).GetAsync();
            if (string.IsNullOrEmpty(result.PatientId))
                return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PCC surveillance match {MatchId}", matchId);
            return StatusCode(500, "An error occurred retrieving the surveillance match.");
        }
    }

    [HttpPost("matches")]
    public async Task<IActionResult> CreateMatch([FromBody] CreatePccSurveillanceMatchRequest req)
    {
        try
        {
            string matchId = Guid.NewGuid().ToString();
            IPccSurveillanceMatchGrain grain = GetMatchGrain(matchId);
            await grain.CreateAsync(
                req.PatientId, req.PatientName,
                req.ConfigId, req.ConditionName,
                req.Classification,
                req.EncounterDate, req.VisitType,
                req.ChiefComplaint, req.FacilityName,
                req.DischargeDate, req.ProviderName,
                req.MatchingDiagnoses, req.MatchingProcedures,
                req.MatchingLabResults, req.MatchingMedications,
                req.Comorbidities, req.Vitals);

            await MatchIndex.AddEntryAsync(new PccSurveillanceMatchIndexEntry
            {
                MatchId = matchId,
                PatientId = req.PatientId,
                ConditionName = req.ConditionName,
                Status = PccSurveillanceMatchStatus.Detected,
                Classification = req.Classification,
                EncounterDate = req.EncounterDate,
                VisitType = req.VisitType,
                CreatedDate = DateTime.UtcNow,
            });

            return Created($"/api/pcc-surveillance/matches/{matchId}",
                new PccSurveillanceResponse { Id = matchId, Message = "Match created." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PCC surveillance match for patient {PatientId}", req.PatientId);
            return StatusCode(500, "An error occurred creating the surveillance match.");
        }
    }

    [HttpPost("matches/{matchId}/status")]
    public async Task<IActionResult> UpdateMatchStatus(string matchId, [FromBody] UpdatePccMatchStatusRequest req)
    {
        try
        {
            IPccSurveillanceMatchGrain grain = GetMatchGrain(matchId);
            await grain.UpdateStatusAsync(req.Status);
            await MatchIndex.UpdateStatusAsync(matchId, req.Status);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for PCC surveillance match {MatchId}", matchId);
            return StatusCode(500, "An error occurred updating the match status.");
        }
    }

    [HttpPost("matches/{matchId}/export")]
    public async Task<IActionResult> ExportMatch(string matchId, [FromBody] ExportPccMatchRequest req)
    {
        try
        {
            IPccSurveillanceMatchGrain grain = GetMatchGrain(matchId);
            await grain.MarkExportedAsync(req.ExportReference);
            await MatchIndex.UpdateStatusAsync(matchId, PccSurveillanceMatchStatus.Exported);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting PCC surveillance match {MatchId}", matchId);
            return StatusCode(500, "An error occurred exporting the match.");
        }
    }
}

// ── Request / Response DTOs ─────────────────────────────────────────────────

public record SavePccSurveillanceConfigRequest(
    string ConditionName,
    PccEncounterClassification Classification,
    List<PccSurveillanceCriterion>? Criteria,
    List<PccVisitType>? RequiredVisitTypes,
    bool DetectComorbidities,
    bool CaptureVitals,
    int ScanWindowDays,
    List<string>? Jurisdictions,
    string ReportingTimeframe,
    bool IsActive);

public record AddPccSurveillanceCriterionRequest(
    string Code,
    string CodeSystem,
    string Description,
    string MatchType,
    string? ValueOperator,
    string? ThresholdValue);

public record CreatePccSurveillanceMatchRequest(
    string PatientId,
    string? PatientName,
    string ConfigId,
    string ConditionName,
    PccEncounterClassification Classification,
    DateTime EncounterDate,
    PccVisitType VisitType,
    string? ChiefComplaint,
    string? FacilityName,
    DateTime? DischargeDate,
    string? ProviderName,
    List<string>? MatchingDiagnoses,
    List<string>? MatchingProcedures,
    List<string>? MatchingLabResults,
    List<string>? MatchingMedications,
    PccComorbidityFlags? Comorbidities,
    PccEncounterVitals? Vitals);

public record UpdatePccMatchStatusRequest(
    PccSurveillanceMatchStatus Status);

public record ExportPccMatchRequest(
    string ExportReference);

public record PccSurveillanceResponse
{
    public string Id { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
