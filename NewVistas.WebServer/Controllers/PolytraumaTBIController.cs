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
[Route("api/polytraumatbi")]
public class PolytraumaTBIController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PolytraumaTBIController> _logger;

    public PolytraumaTBIController(IGrainFactory grainFactory, ILogger<PolytraumaTBIController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPolytraumaRegistryIndexGrain RegistryIndex =>
        _grainFactory.GetGrain<IPolytraumaRegistryIndexGrain>("PT-REGISTRY-IDX");

    private ITBIScreeningIndexGrain GetScreeningIndex(string patientId) =>
        _grainFactory.GetGrain<ITBIScreeningIndexGrain>($"TBI-SCREEN-IDX:{patientId}");

    // ── TBI Screening Queries ─────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/screenings")]
    public async Task<IActionResult> GetScreenings(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            List<TBIScreeningSummaryEntry> result = await GetScreeningIndex(decodedId).GetAllScreeningsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TBI screenings for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving TBI screenings.");
        }
    }

    [HttpGet("patients/{patientId}/screenings/positive")]
    public async Task<IActionResult> GetPositiveScreenings(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            List<TBIScreeningSummaryEntry> result = await GetScreeningIndex(decodedId).GetPositiveScreeningsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving positive TBI screenings for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving positive TBI screenings.");
        }
    }

    [HttpGet("screenings/{screeningId}")]
    public async Task<IActionResult> GetScreening(string screeningId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(screeningId);
            ITBIScreeningGrain grain = _grainFactory.GetGrain<ITBIScreeningGrain>(decodedId);
            TBIScreeningState result = await grain.GetScreeningAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TBI screening {ScreeningId}", screeningId);
            return StatusCode(500, "An error occurred retrieving the TBI screening.");
        }
    }

    [HttpPost("patients/{patientId}/screenings")]
    public async Task<IActionResult> CreateScreening(string patientId, [FromBody] CreateTBIScreeningRequest req)
    {
        try
        {
            string decodedPatientId = Uri.UnescapeDataString(patientId);
            string screeningId = $"TBI-SCREEN:{Guid.NewGuid()}";

            ITBIScreeningGrain grain = _grainFactory.GetGrain<ITBIScreeningGrain>(screeningId);
            await grain.CreateScreeningAsync(
                decodedPatientId, req.PatientName, req.ScreeningDate,
                req.ScreeningLocation, req.ScreenedById, req.ScreenedByName,
                req.EncounterType, req.Answers, req.Notes);

            await GetScreeningIndex(decodedPatientId).UpsertScreeningAsync(new TBIScreeningSummaryEntry
            {
                ScreeningId = screeningId,
                PatientId = decodedPatientId,
                PatientName = req.PatientName,
                ScreeningDate = req.ScreeningDate,
                Result = TBIScreeningResult.Inconclusive,
                ScreenedById = req.ScreenedById,
                ScreenedByName = req.ScreenedByName,
                TriggeredFullEvaluation = false,
            });

            return Created($"/api/polytraumatbi/screenings/{Uri.EscapeDataString(screeningId)}", new { screeningId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating TBI screening for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred creating the TBI screening.");
        }
    }

    [HttpPost("screenings/{screeningId}/finalize")]
    public async Task<IActionResult> FinalizeScreening(string screeningId, [FromBody] FinalizeTBIScreeningRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(screeningId);
            ITBIScreeningGrain grain = _grainFactory.GetGrain<ITBIScreeningGrain>(decodedId);
            await grain.FinalizeScreeningAsync(req.Result, req.TriggeredFullEvaluation);

            TBIScreeningState state = await grain.GetScreeningAsync();
            await GetScreeningIndex(state.PatientId).UpsertScreeningAsync(new TBIScreeningSummaryEntry
            {
                ScreeningId = decodedId,
                PatientId = state.PatientId,
                PatientName = state.PatientName,
                ScreeningDate = state.ScreeningDate,
                Result = state.Result,
                ScreenedById = state.ScreenedById,
                ScreenedByName = state.ScreenedByName,
                TriggeredFullEvaluation = state.TriggeredFullEvaluation,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing TBI screening {ScreeningId}", screeningId);
            return StatusCode(500, "An error occurred finalizing the TBI screening.");
        }
    }

    [HttpPost("screenings/{screeningId}/evaluation")]
    public async Task<IActionResult> RecordFullEvaluation(string screeningId, [FromBody] TBIFullEvaluationRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(screeningId);
            ITBIScreeningGrain grain = _grainFactory.GetGrain<ITBIScreeningGrain>(decodedId);
            await grain.RecordFullEvaluationAsync(req.FullEvalDate, req.ProviderId, req.ProviderName, req.ConfirmedSeverity);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording full TBI evaluation for screening {ScreeningId}", screeningId);
            return StatusCode(500, "An error occurred recording the full TBI evaluation.");
        }
    }

    // ── Polytrauma Registry Queries ───────────────────────────────────────────

    [HttpGet("registry")]
    public async Task<IActionResult> GetRegistry()
    {
        try
        {
            List<PolytraumaRegistrySummaryEntry> result = await RegistryIndex.GetAllPatientsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving polytrauma registry");
            return StatusCode(500, "An error occurred retrieving the polytrauma registry.");
        }
    }

    [HttpGet("registry/active")]
    public async Task<IActionResult> GetActiveRegistry()
    {
        try
        {
            List<PolytraumaRegistrySummaryEntry> result = await RegistryIndex.GetActivePatientAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active polytrauma patients");
            return StatusCode(500, "An error occurred retrieving active polytrauma patients.");
        }
    }

    [HttpGet("patients/{patientId}/polytrauma")]
    public async Task<IActionResult> GetPolytraumaRecord(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPolytraumaRecordGrain grain = _grainFactory.GetGrain<IPolytraumaRecordGrain>($"PT-RECORD:{decodedId}");
            PolytraumaRecordState result = await grain.GetRecordAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving polytrauma record for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving the polytrauma record.");
        }
    }

    [HttpPost("patients/{patientId}/polytrauma")]
    public async Task<IActionResult> RegisterPatient(string patientId, [FromBody] CreatePolytraumaRecordRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string grainKey = $"PT-RECORD:{decodedId}";
            IPolytraumaRecordGrain grain = _grainFactory.GetGrain<IPolytraumaRecordGrain>(grainKey);
            await grain.RegisterPatientAsync(
                decodedId, req.PatientName, req.DateOfBirth,
                req.TraumaMechanism, req.TraumaDate, req.TraumaLocation,
                req.PolytraumaNetworkSite, req.ReferralSource,
                req.PrimaryTeamId, req.PrimaryTeamName,
                req.CaseManagerId, req.CaseManagerName, req.Notes);

            PolytraumaRecordState state = await grain.GetRecordAsync();
            await RegistryIndex.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
            {
                PatientId = decodedId,
                PatientName = req.PatientName,
                Status = PolytraumaStatus.Active,
                RegistrationDate = state.RegistrationDate,
                PrimaryCareTeam = req.PrimaryTeamName,
                TBISeverity = null,
                InjuryCount = 0,
                IssTotalScore = 0,
                LastModifiedDate = DateTime.UtcNow,
            });

            return Created($"/api/polytraumatbi/patients/{Uri.EscapeDataString(patientId)}/polytrauma", new { patientId = decodedId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering polytrauma patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred registering the polytrauma patient.");
        }
    }

    [HttpPost("patients/{patientId}/polytrauma/injuries")]
    public async Task<IActionResult> AddInjury(string patientId, [FromBody] PolytraumaInjury injury)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPolytraumaRecordGrain grain = _grainFactory.GetGrain<IPolytraumaRecordGrain>($"PT-RECORD:{decodedId}");
            await grain.AddInjuryAsync(injury);

            PolytraumaRecordState state = await grain.GetRecordAsync();
            await RegistryIndex.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                Status = state.Status,
                RegistrationDate = state.RegistrationDate,
                PrimaryCareTeam = state.PrimaryPolytraumaTeamName,
                TBISeverity = state.TBISeverity,
                InjuryCount = state.Injuries.Count,
                IssTotalScore = state.IssTotalScore,
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding injury for polytrauma patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred adding the injury.");
        }
    }

    [HttpPost("patients/{patientId}/polytrauma/status")]
    public async Task<IActionResult> UpdateStatus(string patientId, [FromBody] PolytraumaUpdateStatusRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPolytraumaRecordGrain grain = _grainFactory.GetGrain<IPolytraumaRecordGrain>($"PT-RECORD:{decodedId}");
            await grain.UpdateStatusAsync(req.Status, req.DeactivationDate);

            PolytraumaRecordState state = await grain.GetRecordAsync();
            await RegistryIndex.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                Status = state.Status,
                RegistrationDate = state.RegistrationDate,
                PrimaryCareTeam = state.PrimaryPolytraumaTeamName,
                TBISeverity = state.TBISeverity,
                InjuryCount = state.Injuries.Count,
                IssTotalScore = state.IssTotalScore,
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for polytrauma patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating the polytrauma status.");
        }
    }

    [HttpPost("patients/{patientId}/polytrauma/tbi")]
    public async Task<IActionResult> UpdateTBIStatus(string patientId, [FromBody] PolytraumaUpdateTBIRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPolytraumaRecordGrain grain = _grainFactory.GetGrain<IPolytraumaRecordGrain>($"PT-RECORD:{decodedId}");
            await grain.UpdateTBIStatusAsync(req.HasTBI, req.Severity);

            PolytraumaRecordState state = await grain.GetRecordAsync();
            await RegistryIndex.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                Status = state.Status,
                RegistrationDate = state.RegistrationDate,
                PrimaryCareTeam = state.PrimaryPolytraumaTeamName,
                TBISeverity = state.TBISeverity,
                InjuryCount = state.Injuries.Count,
                IssTotalScore = state.IssTotalScore,
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating TBI status for polytrauma patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating the TBI status.");
        }
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateTBIScreeningRequest(
    string PatientName,
    DateTime ScreeningDate,
    string ScreeningLocation,
    string ScreenedById,
    string ScreenedByName,
    string EncounterType,
    List<TBIScreeningAnswer> Answers,
    string? Notes);

public record FinalizeTBIScreeningRequest(TBIScreeningResult Result, bool TriggeredFullEvaluation);

public record TBIFullEvaluationRequest(
    DateTime FullEvalDate,
    string ProviderId,
    string ProviderName,
    TBISeverity ConfirmedSeverity);

public record CreatePolytraumaRecordRequest(
    string PatientName,
    DateTime? DateOfBirth,
    TraumaMechanism TraumaMechanism,
    DateTime? TraumaDate,
    string TraumaLocation,
    string PolytraumaNetworkSite,
    string ReferralSource,
    string PrimaryTeamId,
    string PrimaryTeamName,
    string CaseManagerId,
    string CaseManagerName,
    string? Notes);

public record PolytraumaUpdateStatusRequest(PolytraumaStatus Status, DateTime? DeactivationDate);

public record PolytraumaUpdateTBIRequest(bool HasTBI, TBISeverity? Severity);
