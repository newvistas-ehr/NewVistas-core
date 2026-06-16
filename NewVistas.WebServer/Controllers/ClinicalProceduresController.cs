// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ClinicalProceduresController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ClinicalProceduresController> _logger;

    public ClinicalProceduresController(IGrainFactory grainFactory, ILogger<ClinicalProceduresController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(Uri.UnescapeDataString(patientId));

    // ── Queries ───────────────────────────────────────────────────────────────

    [HttpGet("{patientId}/procedures")]
    public async Task<IActionResult> GetAllProcedures(string patientId)
    {
        try
        {
            List<ClinicProcedureIndexEntry> result = await GetWorkflow(patientId).GetClinicProceduresAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clinical procedures for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving clinical procedures.");
        }
    }

    [HttpGet("{patientId}/procedures/category/{category}")]
    public async Task<IActionResult> GetByCategory(string patientId, ClinicProcedureCategory category)
    {
        try
        {
            List<ClinicProcedureIndexEntry> result = await GetWorkflow(patientId).GetClinicProceduresByCategoryAsync(category);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clinical procedures by category for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving clinical procedures.");
        }
    }

    [HttpGet("{patientId}/procedures/completed")]
    public async Task<IActionResult> GetCompleted(string patientId)
    {
        try
        {
            List<ClinicProcedureIndexEntry> result = await GetWorkflow(patientId).GetCompletedClinicProceduresAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed clinical procedures for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving clinical procedures.");
        }
    }

    [HttpGet("{patientId}/procedures/{procedureId}")]
    public async Task<IActionResult> GetProcedure(string patientId, string procedureId)
    {
        try
        {
            ClinicProcedureState result = await GetWorkflow(patientId)
                .GetClinicProcedureAsync(Uri.UnescapeDataString(procedureId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clinical procedure {ProcedureId} for {PatientId}", procedureId, patientId);
            return StatusCode(500, "An error occurred retrieving the clinical procedure.");
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [HttpPost("{patientId}/procedures")]
    public async Task<IActionResult> OrderProcedure(string patientId, [FromBody] OrderClinicProcedureRequest req)
    {
        try
        {
            string procedureId = await GetWorkflow(patientId).OrderClinicProcedureAsync(
                req.Category, req.ProcedureCode, req.ProcedureDescription,
                req.OrderedDate, req.ProviderId, req.ProviderName,
                req.LocationId, req.LocationName, req.Indication);
            return Created($"/api/clinicalprocedures/{Uri.EscapeDataString(patientId)}/procedures/{Uri.EscapeDataString(procedureId)}", new { procedureId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ordering clinical procedure for {PatientId}", patientId);
            return StatusCode(500, "An error occurred ordering the clinical procedure.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/schedule")]
    public async Task<IActionResult> ScheduleProcedure(string patientId, string procedureId, [FromBody] ScheduleClinicProcedureRequest req)
    {
        try
        {
            await GetWorkflow(patientId).ScheduleClinicProcedureAsync(
                Uri.UnescapeDataString(procedureId), req.ScheduledDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling clinical procedure {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred scheduling the clinical procedure.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/complete")]
    public async Task<IActionResult> CompleteProcedure(string patientId, string procedureId, [FromBody] CompleteClinicProcedureRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CompleteClinicProcedureAsync(
                Uri.UnescapeDataString(procedureId), req.PerformedDate, req.Findings, req.Impression, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing clinical procedure {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred completing the clinical procedure.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/cancel")]
    public async Task<IActionResult> CancelProcedure(string patientId, string procedureId, [FromBody] CancelClinicProcedureRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CancelClinicProcedureAsync(
                Uri.UnescapeDataString(procedureId), req.Reason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling clinical procedure {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred cancelling the clinical procedure.");
        }
    }

    // ── Type-specific results ─────────────────────────────────────────────────

    [HttpPost("{patientId}/procedures/{procedureId}/eeg")]
    public async Task<IActionResult> RecordEegResults(string patientId, string procedureId, [FromBody] RecordEegRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordClinicEegResultsAsync(
                Uri.UnescapeDataString(procedureId),
                req.DurationMinutes, req.Background, req.AlertType,
                req.SeizureActivity, req.FocalRegion, req.Activations);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording EEG results for {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred recording EEG results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/emg")]
    public async Task<IActionResult> RecordEmgResults(string patientId, string procedureId, [FromBody] RecordEmgRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordClinicEmgResultsAsync(
                Uri.UnescapeDataString(procedureId),
                req.MusclesStudied, req.FindingType, req.SpontaneousActivity, req.MupDescription);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording EMG results for {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred recording EMG results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/ncs")]
    public async Task<IActionResult> RecordNcsResults(string patientId, string procedureId, [FromBody] RecordNcsRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordClinicNcsResultsAsync(
                Uri.UnescapeDataString(procedureId),
                req.NervesStudied, req.MeanMotorVelocity, req.MeanSensoryVelocity,
                req.FWavesObtained, req.FindingType);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording NCS results for {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred recording NCS results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/sleep")]
    public async Task<IActionResult> RecordSleepStudyResults(string patientId, string procedureId, [FromBody] RecordSleepStudyRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordClinicSleepStudyResultsAsync(
                Uri.UnescapeDataString(procedureId),
                req.StudyType, req.ApneaType, req.ApneaHypopneaIndex,
                req.CpapPressureCmH2O, req.SleepEfficiencyPct,
                req.TotalSleepTimeMin, req.SleepLatencyMin, req.RemLatencyMin);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording sleep study results for {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred recording sleep study results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/audiometry")]
    public async Task<IActionResult> RecordAudiometryResults(string patientId, string procedureId, [FromBody] RecordAudiometryRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordClinicAudiometryResultsAsync(
                Uri.UnescapeDataString(procedureId),
                req.HearingLossType, req.RightEarPta, req.LeftEarPta,
                req.SpeechDiscriminationRight, req.SpeechDiscriminationLeft,
                req.TympanometryRight, req.TympanometryLeft);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording audiometry results for {ProcedureId}", procedureId);
            return StatusCode(500, "An error occurred recording audiometry results.");
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record OrderClinicProcedureRequest(
    ClinicProcedureCategory Category,
    string ProcedureCode,
    string ProcedureDescription,
    DateTime OrderedDate,
    string? ProviderId,
    string? ProviderName,
    string? LocationId,
    string? LocationName,
    string? Indication);

public record ScheduleClinicProcedureRequest(DateTime ScheduledDate);

public record CompleteClinicProcedureRequest(
    DateTime PerformedDate,
    string? Findings,
    string? Impression,
    string? Notes);

public record CancelClinicProcedureRequest(string? Reason);

public record RecordEegRequest(
    int? DurationMinutes,
    string? Background,
    EegAlertType? AlertType,
    bool? SeizureActivity,
    string? FocalRegion,
    List<string>? Activations);

public record RecordEmgRequest(
    List<string>? MusclesStudied,
    EmgFindingType? FindingType,
    string? SpontaneousActivity,
    string? MupDescription);

public record RecordNcsRequest(
    List<string>? NervesStudied,
    decimal? MeanMotorVelocity,
    decimal? MeanSensoryVelocity,
    bool? FWavesObtained,
    EmgFindingType? FindingType);

public record RecordSleepStudyRequest(
    SleepStudyType StudyType,
    SleepApneaType? ApneaType,
    decimal? ApneaHypopneaIndex,
    decimal? CpapPressureCmH2O,
    decimal? SleepEfficiencyPct,
    int? TotalSleepTimeMin,
    decimal? SleepLatencyMin,
    decimal? RemLatencyMin);

public record RecordAudiometryRequest(
    HearingLossType? HearingLossType,
    decimal? RightEarPta,
    decimal? LeftEarPta,
    decimal? SpeechDiscriminationRight,
    decimal? SpeechDiscriminationLeft,
    string? TympanometryRight,
    string? TympanometryLeft);
