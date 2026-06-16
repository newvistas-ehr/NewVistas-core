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
[Route("api/oncology")]
public class OncologyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OncologyController> _logger;

    public OncologyController(IGrainFactory grainFactory, ILogger<OncologyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Tumor Registry ──────────────────────────────────────────────────────

    [HttpGet("{patientId}/tumors")]
    public async Task<IActionResult> GetTumors(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<OncologyTumorIndexEntry> tumors = await GetWorkflow(pid).GetOncologyTumorsAsync();
            return Ok(tumors.Select(t => new OncologyTumorSummaryResponse(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching oncology tumors for {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve tumor registry.");
        }
    }

    [HttpGet("{patientId}/tumors/active")]
    public async Task<IActionResult> GetActiveTumors(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<OncologyTumorIndexEntry> tumors = await GetWorkflow(pid).GetActiveOncologyTumorsAsync();
            return Ok(tumors.Select(t => new OncologyTumorSummaryResponse(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active oncology tumors for {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve active tumors.");
        }
    }

    [HttpGet("{patientId}/tumors/{tumorId}")]
    public async Task<IActionResult> GetTumor(string patientId, string tumorId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string tid = Uri.UnescapeDataString(tumorId);
            OncologyTumorState state = await GetWorkflow(pid).GetOncologyTumorAsync(tid);
            return Ok(new OncologyTumorDetailResponse(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tumor {TumorId} for {PatientId}", tumorId, patientId);
            return StatusCode(500, "Failed to retrieve tumor record.");
        }
    }

    [HttpPost("{patientId}/tumors")]
    public async Task<IActionResult> RegisterTumor(string patientId, [FromBody] RegisterTumorRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string tumorId = await GetWorkflow(pid).RegisterOncologyTumorAsync(
                req.PrimarySite, req.PrimarySiteText,
                req.Histology, req.HistologyText,
                req.Laterality, req.DateOfDiagnosis,
                req.DiagnosisBasis, req.SequenceNumber,
                req.OncologistId, req.OncologistName);
            return Created($"api/oncology/{patientId}/tumors/{Uri.EscapeDataString(tumorId)}", new { tumorId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering tumor for {PatientId}", patientId);
            return StatusCode(500, "Failed to register tumor.");
        }
    }

    [HttpPost("{patientId}/tumors/{tumorId}/staging")]
    public async Task<IActionResult> RecordStaging(string patientId, string tumorId, [FromBody] RecordStagingRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string tid = Uri.UnescapeDataString(tumorId);
            await GetWorkflow(pid).RecordOncologyStagingAsync(
                tid,
                req.ClinicalT, req.ClinicalN, req.ClinicalM,
                req.PathologicT, req.PathologicN, req.PathologicM,
                req.StageGroup, req.SeerSummaryStage);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording staging for tumor {TumorId}", tumorId);
            return StatusCode(500, "Failed to record staging.");
        }
    }

    [HttpPost("{patientId}/tumors/{tumorId}/status")]
    public async Task<IActionResult> UpdateStatus(string patientId, string tumorId, [FromBody] UpdateOncologyStatusRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string tid = Uri.UnescapeDataString(tumorId);
            await GetWorkflow(pid).UpdateOncologyStatusAsync(tid, req.Status, req.StatusChangeDate, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for tumor {TumorId}", tumorId);
            return StatusCode(500, "Failed to update tumor status.");
        }
    }

    [HttpPost("{patientId}/tumors/{tumorId}/recurrence")]
    public async Task<IActionResult> RecordRecurrence(string patientId, string tumorId, [FromBody] RecordRecurrenceRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string tid = Uri.UnescapeDataString(tumorId);
            await GetWorkflow(pid).RecordOncologyRecurrenceAsync(tid, req.RecurrenceDate, req.RecurrenceSite, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording recurrence for tumor {TumorId}", tumorId);
            return StatusCode(500, "Failed to record recurrence.");
        }
    }

    // ── Treatments ──────────────────────────────────────────────────────────

    [HttpGet("{patientId}/treatments")]
    public async Task<IActionResult> GetTreatments(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<OncologyTreatmentIndexEntry> treatments = await GetWorkflow(pid).GetOncologyTreatmentsAsync();
            return Ok(treatments.Select(t => new OncologyTreatmentSummaryResponse(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching treatments for {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve treatments.");
        }
    }

    [HttpGet("{patientId}/treatments/tumor/{tumorId}")]
    public async Task<IActionResult> GetTreatmentsByTumor(string patientId, string tumorId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string tid = Uri.UnescapeDataString(tumorId);
            List<OncologyTreatmentIndexEntry> treatments = await GetWorkflow(pid).GetOncologyTreatmentsByTumorAsync(tid);
            return Ok(treatments.Select(t => new OncologyTreatmentSummaryResponse(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching treatments for tumor {TumorId}", tumorId);
            return StatusCode(500, "Failed to retrieve treatments for tumor.");
        }
    }

    [HttpGet("{patientId}/treatments/{treatmentId}")]
    public async Task<IActionResult> GetTreatment(string patientId, string treatmentId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string txid = Uri.UnescapeDataString(treatmentId);
            OncologyTreatmentState state = await GetWorkflow(pid).GetOncologyTreatmentAsync(txid);
            return Ok(new OncologyTreatmentDetailResponse(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Failed to retrieve treatment record.");
        }
    }

    [HttpPost("{patientId}/treatments")]
    public async Task<IActionResult> CreateTreatment(string patientId, [FromBody] CreateOncologyTreatmentRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string treatmentId = await GetWorkflow(pid).CreateOncologyTreatmentAsync(
                req.TumorId, req.TreatmentType, req.AgentName,
                req.DoseDescription, req.ProviderId, req.ProviderName,
                req.FacilityName, req.Notes);
            return Created($"api/oncology/{patientId}/treatments/{Uri.EscapeDataString(treatmentId)}", new { treatmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating treatment for {PatientId}", patientId);
            return StatusCode(500, "Failed to create treatment.");
        }
    }

    [HttpPost("{patientId}/treatments/{treatmentId}/start")]
    public async Task<IActionResult> StartTreatment(string patientId, string treatmentId, [FromBody] StartOncologyTreatmentRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string txid = Uri.UnescapeDataString(treatmentId);
            await GetWorkflow(pid).StartOncologyTreatmentAsync(txid, req.StartDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Failed to start treatment.");
        }
    }

    [HttpPost("{patientId}/treatments/{treatmentId}/complete")]
    public async Task<IActionResult> CompleteTreatment(string patientId, string treatmentId, [FromBody] CompleteOncologyTreatmentRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string txid = Uri.UnescapeDataString(treatmentId);
            await GetWorkflow(pid).CompleteOncologyTreatmentAsync(txid, req.EndDate, req.ResponseAssessment, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Failed to complete treatment.");
        }
    }

    [HttpPost("{patientId}/treatments/{treatmentId}/discontinue")]
    public async Task<IActionResult> DiscontinueTreatment(string patientId, string treatmentId, [FromBody] DiscontinueOncologyTreatmentRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string txid = Uri.UnescapeDataString(treatmentId);
            await GetWorkflow(pid).DiscontinueOncologyTreatmentAsync(txid, req.EndDate, req.DiscontinuationReason, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Failed to discontinue treatment.");
        }
    }

    [HttpPost("{patientId}/treatments/{treatmentId}/response")]
    public async Task<IActionResult> RecordResponse(string patientId, string treatmentId, [FromBody] RecordOncologyResponseRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string txid = Uri.UnescapeDataString(treatmentId);
            await GetWorkflow(pid).RecordOncologyResponseAsync(txid, req.ResponseAssessment, req.AssessmentDate, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording response for treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Failed to record response assessment.");
        }
    }

    [HttpPost("{patientId}/treatments/{treatmentId}/cycles")]
    public async Task<IActionResult> UpdateCycles(string patientId, string treatmentId, [FromBody] UpdateOncologyCyclesRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string txid = Uri.UnescapeDataString(treatmentId);
            await GetWorkflow(pid).UpdateOncologyCyclesAsync(txid, req.CyclesCompleted);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cycles for treatment {TreatmentId}", treatmentId);
            return StatusCode(500, "Failed to update cycle count.");
        }
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public record RegisterTumorRequest(
    string PrimarySite,
    string PrimarySiteText,
    string Histology,
    string HistologyText,
    TumorLaterality Laterality,
    DateTime DateOfDiagnosis,
    DiagnosisBasis DiagnosisBasis,
    int SequenceNumber,
    string? OncologistId,
    string? OncologistName);

public record RecordStagingRequest(
    string? ClinicalT,
    string? ClinicalN,
    string? ClinicalM,
    string? PathologicT,
    string? PathologicN,
    string? PathologicM,
    string? StageGroup,
    string? SeerSummaryStage);

public record UpdateOncologyStatusRequest(
    OncologyStatus Status,
    DateTime? StatusChangeDate,
    string? Notes);

public record RecordRecurrenceRequest(
    DateTime RecurrenceDate,
    string? RecurrenceSite,
    string? Notes);

public record CreateOncologyTreatmentRequest(
    string TumorId,
    OncologyTreatmentType TreatmentType,
    string AgentName,
    string? DoseDescription,
    string? ProviderId,
    string? ProviderName,
    string? FacilityName,
    string? Notes);

public record StartOncologyTreatmentRequest(DateTime StartDate);

public record CompleteOncologyTreatmentRequest(
    DateTime EndDate,
    TreatmentResponseAssessment ResponseAssessment,
    string? Notes);

public record DiscontinueOncologyTreatmentRequest(
    DateTime EndDate,
    string DiscontinuationReason,
    string? Notes);

public record RecordOncologyResponseRequest(
    TreatmentResponseAssessment ResponseAssessment,
    DateTime AssessmentDate,
    string? Notes);

public record UpdateOncologyCyclesRequest(int CyclesCompleted);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record OncologyTumorSummaryResponse(
    string TumorId,
    string PrimarySite,
    string PrimarySiteText,
    string Histology,
    string HistologyText,
    DateTime DateOfDiagnosis,
    OncologyStatus Status,
    string? StageGroup,
    int SequenceNumber,
    string? OncologistName)
{
    public OncologyTumorSummaryResponse(OncologyTumorIndexEntry e) : this(
        e.TumorId, e.PrimarySite, e.PrimarySiteText,
        e.Histology, e.HistologyText,
        e.DateOfDiagnosis, e.Status, e.StageGroup,
        e.SequenceNumber, e.OncologistName) { }
}

public record OncologyTumorDetailResponse(
    string TumorId,
    string PatientId,
    string PrimarySite,
    string PrimarySiteText,
    string Histology,
    string HistologyText,
    TumorLaterality Laterality,
    DateTime DateOfDiagnosis,
    DiagnosisBasis DiagnosisBasis,
    int SequenceNumber,
    string? OncologistId,
    string? OncologistName,
    string? ClinicalT,
    string? ClinicalN,
    string? ClinicalM,
    string? PathologicT,
    string? PathologicN,
    string? PathologicM,
    string? StageGroup,
    string? SeerSummaryStage,
    DateTime? StagingDate,
    OncologyStatus Status,
    DateTime? StatusChangeDate,
    DateTime? RecurrenceDate,
    string? RecurrenceSite,
    DateTime? DateOfLastContact,
    List<string> TreatmentIds,
    string? Comments,
    DateTime CreatedDate,
    DateTime LastModifiedDate)
{
    public OncologyTumorDetailResponse(OncologyTumorState s) : this(
        s.TumorId, s.PatientId, s.PrimarySite, s.PrimarySiteText,
        s.Histology, s.HistologyText, s.Laterality,
        s.DateOfDiagnosis, s.DiagnosisBasis, s.SequenceNumber,
        s.OncologistId, s.OncologistName,
        s.ClinicalT, s.ClinicalN, s.ClinicalM,
        s.PathologicT, s.PathologicN, s.PathologicM,
        s.StageGroup, s.SeerSummaryStage, s.StagingDate,
        s.Status, s.StatusChangeDate, s.RecurrenceDate,
        s.RecurrenceSite, s.DateOfLastContact,
        s.TreatmentIds, s.Comments,
        s.CreatedDate, s.LastModifiedDate) { }
}

public record OncologyTreatmentSummaryResponse(
    string TreatmentId,
    string TumorId,
    OncologyTreatmentType TreatmentType,
    string AgentName,
    DateTime? StartDate,
    DateTime? EndDate,
    OncologyTreatmentStatus Status,
    TreatmentResponseAssessment ResponseAssessment,
    string? ProviderName)
{
    public OncologyTreatmentSummaryResponse(OncologyTreatmentIndexEntry e) : this(
        e.TreatmentId, e.TumorId, e.TreatmentType, e.AgentName,
        e.StartDate, e.EndDate, e.Status, e.ResponseAssessment, e.ProviderName) { }
}

public record OncologyTreatmentDetailResponse(
    string TreatmentId,
    string TumorId,
    string PatientId,
    OncologyTreatmentType TreatmentType,
    string AgentName,
    DateTime? StartDate,
    DateTime? EndDate,
    OncologyTreatmentStatus Status,
    int? CyclesCompleted,
    string? DoseDescription,
    string? ProviderId,
    string? ProviderName,
    string? FacilityName,
    TreatmentResponseAssessment ResponseAssessment,
    DateTime? ResponseAssessmentDate,
    string? DiscontinuationReason,
    string? Notes,
    DateTime CreatedDate,
    DateTime LastModifiedDate)
{
    public OncologyTreatmentDetailResponse(OncologyTreatmentState s) : this(
        s.TreatmentId, s.TumorId, s.PatientId,
        s.TreatmentType, s.AgentName,
        s.StartDate, s.EndDate, s.Status,
        s.CyclesCompleted, s.DoseDescription,
        s.ProviderId, s.ProviderName, s.FacilityName,
        s.ResponseAssessment, s.ResponseAssessmentDate,
        s.DiscontinuationReason, s.Notes,
        s.CreatedDate, s.LastModifiedDate) { }
}
