// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// PCE — Patient Care Encounter controller.
/// VistA File #9000010 (VISIT), sub-files #9000010.07 (V POV), #9000010.18 (V CPT).
/// MUMPS references: PXCE*.m, PXK*.m, PXSAVE.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/pce")]
public class PceController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PceController> _logger;

    public PceController(IGrainFactory grainFactory, ILogger<PceController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── GET endpoints ─────────────────────────────────────────────────────────

    [HttpGet("{patientId}/visits")]
    public async Task<IActionResult> GetVisits(string patientId, [FromQuery] int max = 50)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<PceVisitEntry> visits = await GetWorkflow(pid).GetEncounterListAsync(max);
            return Ok(visits.Select(v => new PceVisitSummaryDto(v)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting visits for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving visits.");
        }
    }

    [HttpGet("{patientId}/visits/{visitId}")]
    public async Task<IActionResult> GetVisit(string patientId, string visitId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            VisitState state = await GetWorkflow(pid).GetEncounterAsync(visitId);
            return Ok(new PceVisitDetailDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred retrieving the visit.");
        }
    }

    // ─── POST endpoints ────────────────────────────────────────────────────────

    [HttpPost("{patientId}/visits")]
    public async Task<IActionResult> CreateVisit(string patientId, [FromBody] PceCreateVisitRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string visitId = await GetWorkflow(pid).CreateEncounterAsync(
                req.VisitDateTime, req.ServiceCategory,
                req.LocationId, req.LocationName,
                req.VisitType, req.StopCode,
                req.PrimaryProviderId, req.PrimaryProviderName,
                req.LinkedAppointmentId, req.Comments);
            return Created($"api/pce/{patientId}/visits/{visitId}", new { visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating visit for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred creating the visit.");
        }
    }

    [HttpPost("{patientId}/visits/{visitId}/checkout")]
    public async Task<IActionResult> CheckOut(string patientId, string visitId,
        [FromBody] PceCheckOutRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).CheckOutEncounterAsync(visitId, req.CheckOutDateTime);
            return Ok(new { visitId, status = "CHECKED OUT" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/visits/{visitId}/diagnoses")]
    public async Task<IActionResult> AddDiagnosis(string patientId, string visitId,
        [FromBody] PceAddDiagnosisRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).AddEncounterDiagnosisAsync(
                visitId, req.Icd10Code, req.Description, req.IsPrimary,
                req.ProviderId, req.ProviderName);
            return Created($"api/pce/{patientId}/visits/{visitId}", new { visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding diagnosis to visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/visits/{visitId}/procedures")]
    public async Task<IActionResult> AddProcedure(string patientId, string visitId,
        [FromBody] PceAddProcedureRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).AddEncounterProcedureAsync(
                visitId, req.CptCode, req.Description, req.Quantity,
                req.Modifiers, req.ProviderId, req.ProviderName);
            return Created($"api/pce/{patientId}/visits/{visitId}", new { visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding procedure to visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/visits/{visitId}/cancel")]
    public async Task<IActionResult> CancelVisit(string patientId, string visitId,
        [FromBody] PceCancelRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).CancelEncounterAsync(visitId, req.Reason);
            return Ok(new { visitId, status = "CANCELLED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo([FromQuery] string patientId = "PATIENT-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = GetWorkflow(patientId);

            // Visit 1 — Ambulatory primary care (checked out, with diagnoses + procedures)
            string v1 = await wf.CreateEncounterAsync(
                DateTime.UtcNow.AddDays(-30), "A",
                "LOC-PCARE", "Primary Care Clinic", "ESTABLISHED", "323",
                "PROV-001", "Dr. Sarah Jones",
                null, "Annual wellness visit");
            await wf.AddEncounterDiagnosisAsync(v1, "Z00.00",
                "Encounter for general adult medical examination", true, "PROV-001", "Dr. Sarah Jones");
            await wf.AddEncounterDiagnosisAsync(v1, "I10",
                "Essential hypertension", false, "PROV-001", "Dr. Sarah Jones");
            await wf.AddEncounterProcedureAsync(v1, "99395",
                "Preventive medicine E/M, established patient 18-39 years", 1,
                null, "PROV-001", "Dr. Sarah Jones");
            await wf.AddEncounterProcedureAsync(v1, "85025",
                "Complete blood count (CBC) with differential", 1,
                null, "PROV-001", "Dr. Sarah Jones");
            await wf.CheckOutEncounterAsync(v1, DateTime.UtcNow.AddDays(-30).AddHours(1));

            // Visit 2 — Ambulatory cardiology (checked out)
            string v2 = await wf.CreateEncounterAsync(
                DateTime.UtcNow.AddDays(-14), "A",
                "LOC-CARD", "Cardiology Clinic", "NEW", "301",
                "PROV-002", "Dr. Michael Chen",
                null, "Hypertension follow-up");
            await wf.AddEncounterDiagnosisAsync(v2, "I10",
                "Essential hypertension", true, "PROV-002", "Dr. Michael Chen");
            await wf.AddEncounterDiagnosisAsync(v2, "E11.9",
                "Type 2 diabetes mellitus without complications", false, "PROV-002", "Dr. Michael Chen");
            await wf.AddEncounterProcedureAsync(v2, "99213",
                "Office/outpatient E/M, established patient, low complexity", 1,
                null, "PROV-002", "Dr. Michael Chen");
            await wf.AddEncounterProcedureAsync(v2, "93000",
                "Electrocardiogram routine ECG with at least 12 leads", 1,
                null, "PROV-002", "Dr. Michael Chen");
            await wf.CheckOutEncounterAsync(v2, DateTime.UtcNow.AddDays(-14).AddHours(1));

            // Visit 3 — Telehealth (open)
            string v3 = await wf.CreateEncounterAsync(
                DateTime.UtcNow.AddDays(-2), "T",
                "LOC-TELE", "Telehealth", "ESTABLISHED", "179",
                "PROV-001", "Dr. Sarah Jones",
                null, "Medication review via telehealth");
            await wf.AddEncounterDiagnosisAsync(v3, "Z79.899",
                "Other long-term (current) drug therapy", true, "PROV-001", "Dr. Sarah Jones");
            await wf.AddEncounterProcedureAsync(v3, "99213",
                "Office/outpatient E/M, established patient, low complexity", 1,
                "95", "PROV-001", "Dr. Sarah Jones");

            return Ok(new
            {
                message = "Demo PCE data loaded",
                patientId,
                visitsCreated = 3,
                visitIds = new[] { v1, v2, v3 }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading PCE demo data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record PceVisitSummaryDto(
    string VisitId,
    DateTime VisitDateTime,
    string ServiceCategory,
    string? LocationName,
    string? PrimaryProviderName,
    string Status,
    int DiagnosisCount,
    int ProcedureCount)
{
    public PceVisitSummaryDto(PceVisitEntry v)
        : this(v.VisitId, v.VisitDateTime, v.ServiceCategory, v.LocationName,
               v.PrimaryProviderName, v.Status, v.DiagnosisCount, v.ProcedureCount) { }
}

public record PceDiagnosisDto(
    string Icd10Code,
    string Description,
    bool IsPrimary,
    string? ProviderName,
    DateTime RecordedDateTime)
{
    public PceDiagnosisDto(PceEncounterDiagnosis d)
        : this(d.Icd10Code, d.Description, d.IsPrimary, d.ProviderName, d.RecordedDateTime) { }
}

public record PceProcedureDto(
    string CptCode,
    string Description,
    int Quantity,
    string? Modifiers,
    string? ProviderName,
    DateTime RecordedDateTime)
{
    public PceProcedureDto(PceProcedure p)
        : this(p.CptCode, p.Description, p.Quantity, p.Modifiers, p.ProviderName, p.RecordedDateTime) { }
}

public record PceVisitDetailDto(
    string VisitId,
    string PatientId,
    DateTime VisitDateTime,
    string ServiceCategory,
    string? LocationId,
    string? LocationName,
    string? VisitType,
    string? StopCode,
    string? PrimaryProviderName,
    DateTime? CheckOutDateTime,
    string Status,
    string? CancellationReason,
    string? Comments,
    IReadOnlyList<PceDiagnosisDto> Diagnoses,
    IReadOnlyList<PceProcedureDto> Procedures)
{
    public PceVisitDetailDto(VisitState s)
        : this(s.VisitId, s.PatientId, s.VisitDateTime, s.ServiceCategory,
               s.LocationId, s.LocationName, s.VisitType, s.StopCode,
               s.PrimaryProviderName, s.CheckOutDateTime, s.Status, s.CancellationReason,
               s.Comments,
               s.Diagnoses.Select(d => new PceDiagnosisDto(d)).ToList(),
               s.Procedures.Select(p => new PceProcedureDto(p)).ToList()) { }
}

public record PceCreateVisitRequest(
    DateTime VisitDateTime,
    string ServiceCategory,
    string? LocationId,
    string? LocationName,
    string? VisitType,
    string? StopCode,
    string? PrimaryProviderId,
    string? PrimaryProviderName,
    string? LinkedAppointmentId,
    string? Comments);

public record PceCheckOutRequest(DateTime CheckOutDateTime);

public record PceAddDiagnosisRequest(
    string Icd10Code,
    string Description,
    bool IsPrimary,
    string? ProviderId,
    string? ProviderName);

public record PceAddProcedureRequest(
    string CptCode,
    string Description,
    int Quantity,
    string? Modifiers,
    string? ProviderId,
    string? ProviderName);

public record PceCancelRequest(string? Reason);
