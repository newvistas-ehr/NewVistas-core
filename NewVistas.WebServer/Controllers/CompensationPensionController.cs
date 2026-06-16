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
/// REST API for Compensation &amp; Pension exam scheduling, DBQs, and rating preparation.
/// VistA File #396 (DVBAB5.m, DVBABEXT.m).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CompensationPensionController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CompensationPensionController> _logger;

    public CompensationPensionController(IGrainFactory grainFactory, ILogger<CompensationPensionController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Exams ────────────────────────────────────────────────────────────────

    [HttpGet("{patientId}/exams")]
    public async Task<IActionResult> GetExams(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            List<CPExamIndexEntry> exams = await GetWorkflow(id).GetCPExamsAsync();
            return Ok(exams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving C&P exams for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving C&P exams.");
        }
    }

    [HttpGet("{patientId}/exams/scheduled")]
    public async Task<IActionResult> GetScheduledExams(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            List<CPExamIndexEntry> exams = await GetWorkflow(id).GetScheduledCPExamsAsync();
            return Ok(exams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled C&P exams for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving scheduled C&P exams.");
        }
    }

    [HttpGet("{patientId}/exams/completed")]
    public async Task<IActionResult> GetCompletedExams(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            List<CPExamIndexEntry> exams = await GetWorkflow(id).GetCompletedCPExamsAsync();
            return Ok(exams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed C&P exams for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving completed C&P exams.");
        }
    }

    [HttpGet("{patientId}/exams/{examId}")]
    public async Task<IActionResult> GetExam(string patientId, string examId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string eid = Uri.UnescapeDataString(examId.Trim());
            CPExamState exam = await GetWorkflow(id).GetCPExamAsync(eid);
            return Ok(exam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving C&P exam {ExamId} for {PatientId}", examId, patientId);
            return StatusCode(500, "Error retrieving C&P exam.");
        }
    }

    [HttpPost("{patientId}/exams")]
    public async Task<IActionResult> ScheduleExam(string patientId, [FromBody] ScheduleCPExamRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string examId = await GetWorkflow(id).ScheduleCPExamAsync(
                request.PatientName,
                request.ExamType,
                request.ScheduledDate,
                request.ExaminerName,
                request.ExaminerTitle,
                request.ExaminerType,
                request.ExamLocation,
                request.ExamFacility,
                request.ClaimNumber,
                request.BenefitType,
                request.DisabilityClaimedCodes,
                request.CreatedBy);
            return Created($"/api/compensationpension/{Uri.EscapeDataString(id)}/exams/{Uri.EscapeDataString(examId)}", new { examId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling C&P exam for {PatientId}", patientId);
            return StatusCode(500, "Error scheduling C&P exam.");
        }
    }

    [HttpPost("{patientId}/exams/{examId}/complete")]
    public async Task<IActionResult> CompleteExam(string patientId, string examId, [FromBody] CompleteCPExamRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string eid = Uri.UnescapeDataString(examId.Trim());
            await GetWorkflow(id).CompleteCPExamAsync(eid, request.Diagnoses, request.Nexus, request.NexusRationale);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing C&P exam {ExamId} for {PatientId}", examId, patientId);
            return StatusCode(500, "Error completing C&P exam.");
        }
    }

    [HttpPost("{patientId}/exams/{examId}/cancel")]
    public async Task<IActionResult> CancelExam(string patientId, string examId, [FromBody] CancelCPExamRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string eid = Uri.UnescapeDataString(examId.Trim());
            await GetWorkflow(id).CancelCPExamAsync(eid, request.CancellationReason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling C&P exam {ExamId} for {PatientId}", examId, patientId);
            return StatusCode(500, "Error cancelling C&P exam.");
        }
    }

    [HttpPost("{patientId}/exams/{examId}/reschedule")]
    public async Task<IActionResult> RescheduleExam(string patientId, string examId, [FromBody] RescheduleCPExamRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string eid = Uri.UnescapeDataString(examId.Trim());
            await GetWorkflow(id).RescheduleCPExamAsync(eid, request.NewScheduledDate, request.Reason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling C&P exam {ExamId} for {PatientId}", examId, patientId);
            return StatusCode(500, "Error rescheduling C&P exam.");
        }
    }

    // ── DBQs ─────────────────────────────────────────────────────────────────

    [HttpGet("{patientId}/dbqs")]
    public async Task<IActionResult> GetDBQs(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            List<DBQIndexEntry> dbqs = await GetWorkflow(id).GetDBQsAsync();
            return Ok(dbqs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DBQs for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving DBQs.");
        }
    }

    [HttpGet("{patientId}/dbqs/{dbqId}")]
    public async Task<IActionResult> GetDBQ(string patientId, string dbqId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string did = Uri.UnescapeDataString(dbqId.Trim());
            DBQState dbq = await GetWorkflow(id).GetDBQAsync(did);
            return Ok(dbq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DBQ {DbqId} for {PatientId}", dbqId, patientId);
            return StatusCode(500, "Error retrieving DBQ.");
        }
    }

    [HttpGet("{patientId}/exams/{examId}/dbqs")]
    public async Task<IActionResult> GetDBQsForExam(string patientId, string examId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string eid = Uri.UnescapeDataString(examId.Trim());
            List<DBQIndexEntry> dbqs = await GetWorkflow(id).GetDBQsForExamAsync(eid);
            return Ok(dbqs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DBQs for exam {ExamId} / {PatientId}", examId, patientId);
            return StatusCode(500, "Error retrieving DBQs for exam.");
        }
    }

    [HttpPost("{patientId}/dbqs")]
    public async Task<IActionResult> CreateDBQ(string patientId, [FromBody] CreateDBQRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string dbqId = await GetWorkflow(id).CreateDBQAsync(
                request.ExamId,
                request.PatientName,
                request.DbqType,
                request.DbqFormNumber,
                request.DbqTitle,
                request.ClaimNumber,
                request.ConditionClaimed,
                request.DiagnosisCode,
                request.DiagnosisDescription);
            return Created($"/api/compensationpension/{Uri.EscapeDataString(id)}/dbqs/{Uri.EscapeDataString(dbqId)}", new { dbqId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating DBQ for {PatientId}", patientId);
            return StatusCode(500, "Error creating DBQ.");
        }
    }

    [HttpPost("{patientId}/dbqs/{dbqId}/sections")]
    public async Task<IActionResult> UpdateDBQSections(string patientId, string dbqId, [FromBody] UpdateDBQSectionsRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string did = Uri.UnescapeDataString(dbqId.Trim());
            await GetWorkflow(id).UpdateDBQSectionsAsync(
                did,
                request.HistorySection,
                request.SymptomsSection,
                request.FunctionalImpactSection,
                request.RangeOfMotionSection,
                request.MentalStatusSection,
                request.DiagnosticTestsSection);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating DBQ sections {DbqId} for {PatientId}", dbqId, patientId);
            return StatusCode(500, "Error updating DBQ sections.");
        }
    }

    [HttpPost("{patientId}/dbqs/{dbqId}/opinion")]
    public async Task<IActionResult> RecordDBQOpinion(string patientId, string dbqId, [FromBody] RecordDBQOpinionRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string did = Uri.UnescapeDataString(dbqId.Trim());
            await GetWorkflow(id).RecordDBQOpinionAsync(
                did,
                request.NexusOpinion,
                request.NexusStatement,
                request.OpinionsSection,
                request.ServiceConnectionType,
                request.ResidualsPermanent,
                request.ExpectedImprovement);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording DBQ opinion {DbqId} for {PatientId}", dbqId, patientId);
            return StatusCode(500, "Error recording DBQ opinion.");
        }
    }

    [HttpPost("{patientId}/dbqs/{dbqId}/sign")]
    public async Task<IActionResult> SignDBQ(string patientId, string dbqId, [FromBody] SignDBQRequest request)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            string did = Uri.UnescapeDataString(dbqId.Trim());
            await GetWorkflow(id).CompleteDBQAsync(did);
            await GetWorkflow(id).SignDBQAsync(did, request.SignedBy);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing DBQ {DbqId} for {PatientId}", dbqId, patientId);
            return StatusCode(500, "Error signing DBQ.");
        }
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public record ScheduleCPExamRequest(
    string PatientName,
    CPExamType ExamType,
    DateTime ScheduledDate,
    string ExaminerName,
    string ExaminerTitle,
    CPExaminerType ExaminerType,
    string ExamLocation,
    string ExamFacility,
    string ClaimNumber,
    string BenefitType,
    List<string> DisabilityClaimedCodes,
    string CreatedBy);

public record CompleteCPExamRequest(
    List<string> Diagnoses,
    bool Nexus,
    string NexusRationale);

public record CancelCPExamRequest(string CancellationReason);

public record RescheduleCPExamRequest(DateTime NewScheduledDate, string Reason);

public record CreateDBQRequest(
    string ExamId,
    string PatientName,
    DBQType DbqType,
    string DbqFormNumber,
    string DbqTitle,
    string ClaimNumber,
    string ConditionClaimed,
    string DiagnosisCode,
    string DiagnosisDescription);

public record UpdateDBQSectionsRequest(
    string HistorySection,
    string SymptomsSection,
    string FunctionalImpactSection,
    string RangeOfMotionSection,
    string MentalStatusSection,
    string DiagnosticTestsSection);

public record RecordDBQOpinionRequest(
    bool NexusOpinion,
    string NexusStatement,
    string OpinionsSection,
    ServiceConnectionType ServiceConnectionType,
    bool ResidualsPermanent,
    bool ExpectedImprovement);

public record SignDBQRequest(string SignedBy);
