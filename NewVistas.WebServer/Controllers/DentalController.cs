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
/// REST API controller for the VA Dental module.
/// Maps to VistA Files #228 (DENTAL PATIENT) and #228.1 (DENTAL TREATMENT).
/// All patient-scoped operations delegate through IPatientWorkflowGrain.
/// </summary>
[Authorize]
[ApiController]
[Route("api/dental")]
[Produces("application/json")]
public class DentalController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DentalController> _logger;

    public DentalController(IGrainFactory grainFactory, ILogger<DentalController> logger)
    {
        _grainFactory = grainFactory;
        _logger       = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Dental Patient Record (File #228) ───────────────────────────────────

    /// <summary>Returns the patient's dental record (eligibility, clinical status, visit dates).</summary>
    [HttpGet("patients/{patientId}/dental-record")]
    public async Task<IActionResult> GetDentalRecord(string patientId)
    {
        try { return Ok(await W(patientId).GetDentalPatientAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dental record for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving dental record.");
        }
    }

    /// <summary>Updates the patient's VA dental eligibility and basis.</summary>
    [HttpPut("patients/{patientId}/eligibility")]
    public async Task<IActionResult> UpdateEligibility(
        string patientId,
        [FromBody] UpdateDentalEligibilityRequest req)
    {
        try
        {
            if (!Enum.TryParse<DentalEligibilityStatus>(req.EligibilityStatus, out DentalEligibilityStatus status))
                return BadRequest($"Unknown eligibility status: {req.EligibilityStatus}");

            await W(patientId).UpdateDentalEligibilityAsync(
                status, req.EligibilityBasisCode, req.EligibilityBasisDescription);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dental eligibility for {PatientId}", patientId);
            return StatusCode(500, "Error updating dental eligibility.");
        }
    }

    /// <summary>Sets or updates the patient's primary VA dentist.</summary>
    [HttpPut("patients/{patientId}/primary-dentist")]
    public async Task<IActionResult> SetPrimaryDentist(
        string patientId,
        [FromBody] SetPrimaryDentistRequest req)
    {
        try
        {
            await W(patientId).SetPrimaryDentistAsync(req.DentistId, req.DentistName);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary dentist for {PatientId}", patientId);
            return StatusCode(500, "Error setting primary dentist.");
        }
    }

    /// <summary>Updates the patient's clinical dental status fields.</summary>
    [HttpPut("patients/{patientId}/clinical-status")]
    public async Task<IActionResult> UpdateClinicalStatus(
        string patientId,
        [FromBody] UpdateDentalClinicalStatusRequest req)
    {
        try
        {
            if (!Enum.TryParse<DentalPeriodontalStatus>(req.PeriodontalStatus, out DentalPeriodontalStatus periStatus))
                return BadRequest($"Unknown periodontal status: {req.PeriodontalStatus}");

            await W(patientId).UpdateDentalClinicalStatusAsync(
                periStatus, req.ProstheticStatus, req.RemainingTeethCount,
                req.OnFluoride, req.ClinicalNotes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dental clinical status for {PatientId}", patientId);
            return StatusCode(500, "Error updating dental clinical status.");
        }
    }

    /// <summary>Records dental visit dates (exam, x-ray, cleaning).</summary>
    [HttpPut("patients/{patientId}/visit-dates")]
    public async Task<IActionResult> RecordVisitDates(
        string patientId,
        [FromBody] RecordDentalVisitDatesRequest req)
    {
        try
        {
            await W(patientId).RecordDentalVisitDatesAsync(
                req.LastExamDate, req.LastXRayDate, req.LastCleaningDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording dental visit dates for {PatientId}", patientId);
            return StatusCode(500, "Error recording dental visit dates.");
        }
    }

    // ─── Dental Treatments (File #228.1) ─────────────────────────────────────

    /// <summary>Returns all dental treatment summaries for the patient, newest first.</summary>
    [HttpGet("patients/{patientId}/treatments")]
    public async Task<IActionResult> GetTreatments(
        string patientId,
        [FromQuery] string? status = null)
    {
        try
        {
            if (status != null && Enum.TryParse<DentalTreatmentStatus>(status, out DentalTreatmentStatus txStatus))
                return Ok(await W(patientId).GetDentalTreatmentsByStatusAsync(txStatus));

            return Ok(await W(patientId).GetDentalTreatmentsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dental treatments for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving dental treatments.");
        }
    }

    /// <summary>Returns the full state of a single dental treatment record.</summary>
    [HttpGet("treatments/{treatmentId}")]
    public async Task<IActionResult> GetTreatment(string treatmentId, [FromQuery] string patientId)
    {
        try { return Ok(await W(patientId).GetDentalTreatmentAsync(treatmentId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dental treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Error retrieving dental treatment.");
        }
    }

    /// <summary>Records a new dental treatment / procedure for the patient.</summary>
    [HttpPost("patients/{patientId}/treatments")]
    public async Task<IActionResult> RecordTreatment(
        string patientId,
        [FromBody] RecordDentalTreatmentRequest req)
    {
        try
        {
            if (!Enum.TryParse<DentalProcedureCategory>(req.ProcedureCategory, out DentalProcedureCategory category))
                return BadRequest($"Unknown procedure category: {req.ProcedureCategory}");

            string treatmentId = await W(patientId).RecordDentalTreatmentAsync(
                req.TreatmentDate,
                req.ProcedureCode,
                req.ProcedureDescription,
                category,
                req.ToothNumbers ?? new List<int>(),
                req.Surfaces ?? new List<string>(),
                req.ProviderId,
                req.ProviderName,
                req.LocationId,
                req.LocationName,
                req.DiagnosisCode,
                req.AnesthesiaType,
                req.ChargeAmount,
                req.Notes);

            return Created($"api/dental/treatments/{treatmentId}", new { treatmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording dental treatment for {PatientId}", patientId);
            return StatusCode(500, "Error recording dental treatment.");
        }
    }

    /// <summary>Marks a dental treatment as completed.</summary>
    [HttpPost("treatments/{treatmentId}/complete")]
    public async Task<IActionResult> CompleteTreatment(
        string treatmentId,
        [FromBody] CompleteDentalTreatmentRequest req)
    {
        try
        {
            await W(req.PatientId).CompleteDentalTreatmentAsync(
                treatmentId, req.CompletedDate, req.CompletedByUserId, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing dental treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Error completing dental treatment.");
        }
    }

    /// <summary>Cancels a dental treatment.</summary>
    [HttpPost("treatments/{treatmentId}/cancel")]
    public async Task<IActionResult> CancelTreatment(
        string treatmentId,
        [FromBody] DentalTreatmentActionRequest req)
    {
        try
        {
            await W(req.PatientId).CancelDentalTreatmentAsync(treatmentId, req.Reason, req.UserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling dental treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Error cancelling dental treatment.");
        }
    }

    /// <summary>Marks a dental treatment as referred to a specialist.</summary>
    [HttpPost("treatments/{treatmentId}/refer")]
    public async Task<IActionResult> ReferTreatment(
        string treatmentId,
        [FromBody] DentalTreatmentActionRequest req)
    {
        try
        {
            await W(req.PatientId).ReferDentalTreatmentAsync(treatmentId, req.Reason, req.UserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error referring dental treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Error referring dental treatment.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record UpdateDentalEligibilityRequest(
    string EligibilityStatus,
    string? EligibilityBasisCode,
    string? EligibilityBasisDescription);

public record SetPrimaryDentistRequest(
    string DentistId,
    string DentistName);

public record UpdateDentalClinicalStatusRequest(
    string PeriodontalStatus,
    string? ProstheticStatus,
    int? RemainingTeethCount,
    bool OnFluoride,
    string? ClinicalNotes);

public record RecordDentalVisitDatesRequest(
    DateTime? LastExamDate,
    DateTime? LastXRayDate,
    DateTime? LastCleaningDate);

public record RecordDentalTreatmentRequest(
    DateTime TreatmentDate,
    string ProcedureCode,
    string ProcedureDescription,
    string ProcedureCategory,
    List<int>? ToothNumbers,
    List<string>? Surfaces,
    string ProviderId,
    string ProviderName,
    string? LocationId,
    string? LocationName,
    string? DiagnosisCode,
    string? AnesthesiaType,
    decimal? ChargeAmount,
    string? Notes);

public record CompleteDentalTreatmentRequest(
    string PatientId,
    DateTime CompletedDate,
    string CompletedByUserId,
    string? Notes);

public record DentalTreatmentActionRequest(
    string PatientId,
    string Reason,
    string UserId);
