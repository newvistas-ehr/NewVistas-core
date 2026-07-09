// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// ADT — Admission / Discharge / Transfer
/// VistA PATIENT MOVEMENT file (#405) / WARD LOCATION file (#42)
///
/// Placement is structured: institutionId + unitId are required, bedId is optional
/// (null = unit boarder). Bed truth lives on the inpatient unit grain; movements
/// occupy/release beds atomically through the workflow.
/// </summary>
[Authorize]
[ApiController]
[Route("api/adt")]
[Produces("application/json")]
public class AdtController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AdtController> _logger;

    public AdtController(IGrainFactory grainFactory, ILogger<AdtController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Patient movement history ────────────────────────────────────────

    [HttpGet("{patientId}/movements")]
    public async Task<IActionResult> GetMovements(string patientId)
    {
        try { return Ok(await W(patientId).GetAdtMovementsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting ADT movements for {PatientId}", patientId); return StatusCode(500, "Error retrieving movements."); }
    }

    // ── Admission ───────────────────────────────────────────────────────

    [HttpPost("{patientId}/movements/admit")]
    public async Task<IActionResult> Admit(string patientId, [FromBody] AdtAdmitRequest r)
    {
        try
        {
            string id = await W(patientId).RecordAdmissionAsync(
                r.MovementDateTime, r.InstitutionId ?? "500", r.UnitId, r.BedId,
                r.TreatingSpecialtyName,
                r.AttendingPhysicianId, r.AttendingPhysicianName,
                r.AdmissionDiagnosis, r.Comments);
            return Created(string.Empty, new { MovementId = id });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { _logger.LogError(ex, "Error admitting patient {PatientId}", patientId); return StatusCode(500, "Error recording admission."); }
    }

    // ── Transfer ────────────────────────────────────────────────────────

    [HttpPost("{patientId}/movements/{movementId}/transfer")]
    public async Task<IActionResult> Transfer(string patientId, string movementId, [FromBody] AdtTransferRequest r)
    {
        try
        {
            string id = await W(patientId).RecordTransferAsync(
                movementId, r.TransferDateTime,
                r.ToInstitutionId ?? "500", r.ToUnitId, r.ToBedId,
                r.ToSpecialtyId, r.ToSpecialtyName,
                r.AttendingPhysicianId, r.AttendingPhysicianName,
                r.Comments, r.OverrideReservation);
            return Created(string.Empty, new { MovementId = id });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { _logger.LogError(ex, "Error transferring patient {PatientId}", patientId); return StatusCode(500, "Error recording transfer."); }
    }

    // ── Discharge ───────────────────────────────────────────────────────

    [HttpPost("{patientId}/movements/{movementId}/discharge")]
    public async Task<IActionResult> Discharge(string patientId, string movementId, [FromBody] AdtDischargeRequest r)
    {
        try
        {
            await W(patientId).RecordDischargeAsync(
                movementId, r.DischargeDateTime,
                r.DischargeDiagnosis, r.Disposition, r.Comments);
            return Ok(new { MovementId = movementId, Message = "Discharged" });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error discharging patient {PatientId}", patientId); return StatusCode(500, "Error recording discharge."); }
    }

    // ── Unit directory (route kept from the old ward list; payload is the live capacity summary) ──

    [HttpGet("wards")]
    public async Task<IActionResult> GetWards([FromQuery] string institutionId = "500")
    {
        try
        {
            IBedCapacityGrain capacity = _grainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");
            return Ok(await capacity.GetUnitsAsync());
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting unit directory"); return StatusCode(500, "Error retrieving wards."); }
    }

    // ── Unit census ─────────────────────────────────────────────────────

    [HttpGet("wards/{unitId}/census")]
    public async Task<IActionResult> GetCensus(string unitId, [FromQuery] string institutionId = "500")
    {
        try
        {
            IInpatientUnitGrain unit = _grainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");
            return Ok(await unit.GetCensusAsync());
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting census for unit {UnitId}", unitId); return StatusCode(500, "Error retrieving census."); }
    }

    // ── Demo data load ──────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> DemoLoad([FromQuery] string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            return BadRequest("patientId query parameter is required.");
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = W(patientId);

            // Admit to Medical Ward 3A
            string admitId = await wf.RecordAdmissionAsync(
                DateTime.UtcNow.AddDays(-3), "500", "MED-3A", "301-A",
                "Internal Medicine", "PROV-001", "Dr. Smith",
                "Community-acquired pneumonia", "Demo admission");

            // Transfer to ICU after one day
            await wf.RecordTransferAsync(
                admitId, DateTime.UtcNow.AddDays(-2),
                "500", "ICU-1", "ICU-5",
                null, "Critical Care", "PROV-002", "Dr. Jones",
                "Deteriorating respiratory status");

            return Ok(new { AdmissionId = admitId, Message = "Demo ADT data loaded." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error loading demo ADT data for {PatientId}", patientId); return StatusCode(500, "Error loading demo data."); }
        finally { DemoSeedHelper.RestoreContext(saved); }
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────

public record AdtAdmitRequest(
    DateTime MovementDateTime,
    string UnitId,
    string? BedId,
    string? InstitutionId,
    string? TreatingSpecialtyId,
    string? TreatingSpecialtyName,
    string? AttendingPhysicianId,
    string? AttendingPhysicianName,
    string? TypeOfPatient,
    string? AdmissionDiagnosis,
    string? Comments);

public record AdtTransferRequest(
    DateTime TransferDateTime,
    string ToUnitId,
    string? ToBedId,
    string? ToInstitutionId,
    string? ToSpecialtyId,
    string? ToSpecialtyName,
    string? AttendingPhysicianId,
    string? AttendingPhysicianName,
    string? Comments,
    bool OverrideReservation = false);

public record AdtDischargeRequest(
    DateTime DischargeDateTime,
    string? DischargeDiagnosis,
    string? Disposition,
    string? Comments);
