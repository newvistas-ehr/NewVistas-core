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
[Route("api/medicine")]
public class MedicineController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MedicineController> _logger;

    public MedicineController(IGrainFactory grainFactory, ILogger<MedicineController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Procedure List ─────────────────────────────────────────────────────────

    [HttpGet("{patientId}/procedures")]
    public async Task<IActionResult> GetProcedures(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<MedProcedureIndexEntry> procedures = await GetWorkflow(pid).GetMedProceduresAsync();
            return Ok(procedures.Select(p => new MedProcedureSummaryResponse(p)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching medicine procedures for {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve Medicine procedures.");
        }
    }

    [HttpGet("{patientId}/procedures/category/{category}")]
    public async Task<IActionResult> GetProceduresByCategory(string patientId, MedProcedureCategory category)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<MedProcedureIndexEntry> procedures = await GetWorkflow(pid).GetMedProceduresByCategoryAsync(category);
            return Ok(procedures.Select(p => new MedProcedureSummaryResponse(p)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching medicine procedures by category for {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve procedures by category.");
        }
    }

    [HttpGet("{patientId}/procedures/completed")]
    public async Task<IActionResult> GetCompletedProcedures(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<MedProcedureIndexEntry> procedures = await GetWorkflow(pid).GetCompletedMedProceduresAsync();
            return Ok(procedures.Select(p => new MedProcedureSummaryResponse(p)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching completed medicine procedures for {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve completed procedures.");
        }
    }

    [HttpGet("{patientId}/procedures/{procedureId}")]
    public async Task<IActionResult> GetProcedure(string patientId, string procedureId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            MedProcedureState state = await GetWorkflow(pid).GetMedProcedureAsync(procId);
            return Ok(new MedProcedureDetailResponse(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching procedure {ProcedureId} for {PatientId}", procedureId, patientId);
            return StatusCode(500, "Failed to retrieve procedure record.");
        }
    }

    // ── Order / Complete / Cancel ──────────────────────────────────────────────

    [HttpPost("{patientId}/procedures")]
    public async Task<IActionResult> OrderProcedure(string patientId, [FromBody] OrderMedProcedureRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procedureId = await GetWorkflow(pid).OrderMedProcedureAsync(
                req.Category, req.ProcedureCode, req.ProcedureDescription,
                req.OrderedDate, req.ProviderId, req.ProviderName,
                req.LocationId, req.LocationName, req.Indication);
            return Created($"api/medicine/{patientId}/procedures/{Uri.EscapeDataString(procedureId)}", new { procedureId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ordering medicine procedure for {PatientId}", patientId);
            return StatusCode(500, "Failed to order procedure.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/schedule")]
    public async Task<IActionResult> ScheduleProcedure(string patientId, string procedureId, [FromBody] ScheduleMedProcedureRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).ScheduleMedProcedureAsync(procId, req.ScheduledDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling procedure {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to schedule procedure.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/complete")]
    public async Task<IActionResult> CompleteProcedure(string patientId, string procedureId, [FromBody] CompleteMedProcedureRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).CompleteMedProcedureAsync(procId, req.PerformedDate, req.Findings, req.Impression, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing procedure {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to complete procedure.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/cancel")]
    public async Task<IActionResult> CancelProcedure(string patientId, string procedureId, [FromBody] CancelMedProcedureRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).CancelMedProcedureAsync(procId, req.Reason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling procedure {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to cancel procedure.");
        }
    }

    // ── Type-specific results ──────────────────────────────────────────────────

    [HttpPost("{patientId}/procedures/{procedureId}/ecg")]
    public async Task<IActionResult> RecordEcgResults(string patientId, string procedureId, [FromBody] RecordEcgRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).RecordMedEcgResultsAsync(
                procId, req.Rate, req.Rhythm, req.PrIntervalMs, req.QrsDurationMs,
                req.QtcMs, req.AxisDegrees, req.Interpretation, req.IsNormal);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording ECG results for {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to record ECG results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/echo")]
    public async Task<IActionResult> RecordEchoResults(string patientId, string procedureId, [FromBody] RecordEchoRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).RecordMedEchoResultsAsync(procId, req.LvEjectionFraction, req.LvDiastolicFunction, req.ValvularFindings);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording echo results for {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to record echocardiogram results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/stresstest")]
    public async Task<IActionResult> RecordStressTestResults(string patientId, string procedureId, [FromBody] RecordStressTestRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).RecordMedStressTestResultsAsync(procId, req.PeakMets, req.TargetHeartRatePct, req.InducibleIschemia);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording stress test results for {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to record stress test results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/pft")]
    public async Task<IActionResult> RecordPftResults(string patientId, string procedureId, [FromBody] RecordPftRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).RecordMedPftResultsAsync(
                procId, req.Fev1, req.Fev1PctPredicted, req.Fvc, req.FvcPctPredicted,
                req.Fev1FvcRatio, req.Dlco, req.DlcoPctPredicted, req.Tlc, req.Rv,
                req.Obstructive, req.Restrictive, req.BronchodilatorResponse);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording PFT results for {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to record pulmonary function results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/abg")]
    public async Task<IActionResult> RecordAbgResults(string patientId, string procedureId, [FromBody] RecordAbgRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).RecordMedAbgResultsAsync(procId, req.Ph, req.Pao2, req.Paco2, req.Hco3, req.Sao2);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording ABG results for {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to record arterial blood gas results.");
        }
    }

    [HttpPost("{patientId}/procedures/{procedureId}/endoscopy")]
    public async Task<IActionResult> RecordEndoscopyResults(string patientId, string procedureId, [FromBody] RecordEndoscopyRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string procId = Uri.UnescapeDataString(procedureId);
            await GetWorkflow(pid).RecordMedEndoscopyResultsAsync(
                procId, req.EndoscopyType, req.BowelPrepQuality, req.CecumReached,
                req.ScopeAdvancedCm, req.BiopsyTaken, req.BiopsySites,
                req.PolypCount, req.PolypDescriptions, req.EndoscopicInterventions);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording endoscopy results for {ProcedureId}", procedureId);
            return StatusCode(500, "Failed to record endoscopy results.");
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record OrderMedProcedureRequest(
    MedProcedureCategory Category,
    string ProcedureCode,
    string ProcedureDescription,
    DateTime OrderedDate,
    string? ProviderId,
    string? ProviderName,
    string? LocationId,
    string? LocationName,
    string? Indication);

public record ScheduleMedProcedureRequest(DateTime ScheduledDate);

public record CompleteMedProcedureRequest(
    DateTime PerformedDate,
    string? Findings,
    string? Impression,
    string? Notes);

public record CancelMedProcedureRequest(string? Reason);

public record RecordEcgRequest(
    int? Rate,
    CardiacRhythm? Rhythm,
    int? PrIntervalMs,
    int? QrsDurationMs,
    int? QtcMs,
    int? AxisDegrees,
    string? Interpretation,
    bool? IsNormal);

public record RecordEchoRequest(
    decimal? LvEjectionFraction,
    string? LvDiastolicFunction,
    string? ValvularFindings);

public record RecordStressTestRequest(
    decimal? PeakMets,
    decimal? TargetHeartRatePct,
    bool? InducibleIschemia);

public record RecordPftRequest(
    decimal? Fev1,
    decimal? Fev1PctPredicted,
    decimal? Fvc,
    decimal? FvcPctPredicted,
    decimal? Fev1FvcRatio,
    decimal? Dlco,
    decimal? DlcoPctPredicted,
    decimal? Tlc,
    decimal? Rv,
    bool? Obstructive,
    bool? Restrictive,
    bool? BronchodilatorResponse);

public record RecordAbgRequest(
    decimal? Ph,
    decimal? Pao2,
    decimal? Paco2,
    decimal? Hco3,
    decimal? Sao2);

public record RecordEndoscopyRequest(
    EndoscopyType EndoscopyType,
    BowelPrepQuality? BowelPrepQuality,
    bool? CecumReached,
    int? ScopeAdvancedCm,
    bool? BiopsyTaken,
    List<string>? BiopsySites,
    int? PolypCount,
    List<string>? PolypDescriptions,
    List<string>? EndoscopicInterventions);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record MedProcedureSummaryResponse(
    string ProcedureId,
    MedProcedureCategory Category,
    string ProcedureCode,
    string ProcedureDescription,
    MedProcedureStatus Status,
    DateTime OrderedDate,
    DateTime? PerformedDate,
    string? ProviderName,
    string? LocationName,
    string? Impression)
{
    public MedProcedureSummaryResponse(MedProcedureIndexEntry e) : this(
        e.ProcedureId, e.Category, e.ProcedureCode, e.ProcedureDescription,
        e.Status, e.OrderedDate, e.PerformedDate, e.ProviderName,
        e.LocationName, e.Impression) { }
}

public record MedProcedureDetailResponse(
    string ProcedureId,
    string PatientId,
    MedProcedureCategory Category,
    string ProcedureCode,
    string ProcedureDescription,
    MedProcedureStatus Status,
    DateTime OrderedDate,
    DateTime? ScheduledDate,
    DateTime? PerformedDate,
    string? ProviderId,
    string? ProviderName,
    string? LocationId,
    string? LocationName,
    string? Indication,
    string? Findings,
    string? Impression,
    string? CancellationReason,
    string? Notes,
    // Cardiology
    CardiologyStudyType? CardiologyStudyType,
    decimal? LvEjectionFraction,
    string? LvDiastolicFunction,
    string? ValvularFindings,
    decimal? PeakMets,
    decimal? TargetHeartRatePct,
    bool? InducibleIschemia,
    int? HolterDurationHours,
    int? HolterArrhythmiaEvents,
    // ECG
    int? EcgRate,
    CardiacRhythm? EcgRhythm,
    int? EcgPrIntervalMs,
    int? EcgQrsDurationMs,
    int? EcgQtcMs,
    int? EcgAxisDegrees,
    string? EcgInterpretation,
    bool? EcgIsNormal,
    // PFT
    decimal? PftFev1,
    decimal? PftFev1PctPredicted,
    decimal? PftFvc,
    decimal? PftFvcPctPredicted,
    decimal? PftFev1FvcRatio,
    decimal? PftDlco,
    decimal? PftDlcoPctPredicted,
    decimal? PftTlc,
    decimal? PftRv,
    bool? PftObstructive,
    bool? PftRestrictive,
    bool? PftBronchodilatorResponse,
    // ABG
    decimal? AbgPh,
    decimal? AbgPao2,
    decimal? AbgPaco2,
    decimal? AbgHco3,
    decimal? AbgSao2,
    // Endoscopy
    EndoscopyType? EndoscopyType,
    BowelPrepQuality? BowelPrepQuality,
    bool? CecumReached,
    int? ScopeAdvancedCm,
    bool? BiopsyTaken,
    List<string> BiopsySites,
    int? PolypCount,
    List<string> PolypDescriptions,
    List<string> EndoscopicInterventions,
    DateTime CreatedDate,
    DateTime LastModifiedDate)
{
    public MedProcedureDetailResponse(MedProcedureState s) : this(
        s.ProcedureId, s.PatientId, s.Category, s.ProcedureCode, s.ProcedureDescription,
        s.Status, s.OrderedDate, s.ScheduledDate, s.PerformedDate,
        s.ProviderId, s.ProviderName, s.LocationId, s.LocationName,
        s.Indication, s.Findings, s.Impression, s.CancellationReason, s.Notes,
        s.CardiologyStudyType, s.LvEjectionFraction, s.LvDiastolicFunction, s.ValvularFindings,
        s.PeakMets, s.TargetHeartRatePct, s.InducibleIschemia,
        s.HolterDurationHours, s.HolterArrhythmiaEvents,
        s.EcgRate, s.EcgRhythm, s.EcgPrIntervalMs, s.EcgQrsDurationMs,
        s.EcgQtcMs, s.EcgAxisDegrees, s.EcgInterpretation, s.EcgIsNormal,
        s.PftFev1, s.PftFev1PctPredicted, s.PftFvc, s.PftFvcPctPredicted,
        s.PftFev1FvcRatio, s.PftDlco, s.PftDlcoPctPredicted, s.PftTlc, s.PftRv,
        s.PftObstructive, s.PftRestrictive, s.PftBronchodilatorResponse,
        s.AbgPh, s.AbgPao2, s.AbgPaco2, s.AbgHco3, s.AbgSao2,
        s.EndoscopyType, s.BowelPrepQuality, s.CecumReached, s.ScopeAdvancedCm,
        s.BiopsyTaken, s.BiopsySites, s.PolypCount, s.PolypDescriptions,
        s.EndoscopicInterventions, s.CreatedDate, s.LastModifiedDate) { }
}
