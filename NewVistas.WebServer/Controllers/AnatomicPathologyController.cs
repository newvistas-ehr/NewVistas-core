// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Anatomic Pathology REST API.
/// Maps to VistA LAB DATA file (#63) subfiles:
///   #63.08 Surgical Pathology
///   #63.09 Cytology
///   #63.19 Autopsy
/// MUMPS routines: LRAP.m, LRAPSC.m, LRAPACC.m, LRAPAU.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/anatomicpathology")]
public class AnatomicPathologyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AnatomicPathologyController> _logger;

    public AnatomicPathologyController(IGrainFactory grainFactory, ILogger<AnatomicPathologyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Case List ────────────────────────────────────────────────────────────

    [HttpGet("{patientId}/cases")]
    public async Task<IActionResult> GetCases(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<APCaseIndexEntry> cases = await GetWorkflow(pid).GetAPCasesAsync();
            return Ok(cases.Select(c => new APCaseIndexDto(c)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving AP cases for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving anatomic pathology cases.");
        }
    }

    [HttpGet("{patientId}/cases/type/{caseType}")]
    public async Task<IActionResult> GetCasesByType(string patientId, APCaseType caseType)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<APCaseIndexEntry> cases = await GetWorkflow(pid).GetAPCasesByTypeAsync(caseType);
            return Ok(cases.Select(c => new APCaseIndexDto(c)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {CaseType} cases for patient {PatientId}", caseType, patientId);
            return StatusCode(500, "An error occurred retrieving cases.");
        }
    }

    // ─── Case Detail ──────────────────────────────────────────────────────────

    [HttpGet("{patientId}/cases/{caseId}")]
    public async Task<IActionResult> GetCase(string patientId, string caseId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            AnatomicPathologyState state = await GetWorkflow(pid).GetAPCaseAsync(caseId);
            return Ok(new APCaseDetailDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving AP case {CaseId}", caseId);
            return StatusCode(500, "An error occurred retrieving the case.");
        }
    }

    // ─── Accession ────────────────────────────────────────────────────────────

    [HttpPost("{patientId}/cases")]
    public async Task<IActionResult> AccessionCase(string patientId, [FromBody] AccessionAPCaseRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string caseId = await GetWorkflow(pid).AccessionAPCaseAsync(
                req.CaseType,
                req.AccessionNumber,
                req.SpecimenSource,
                req.SpecimenDescription,
                req.SpecimenType,
                req.ClinicalHistory,
                req.ClinicalDiagnosis,
                req.ReferringProviderId,
                req.ReferringProviderName,
                req.CollectionLocation,
                req.DateCollected,
                req.DateReceived);
            return Created($"api/anatomicpathology/{patientId}/cases/{caseId}", new { caseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accessioning AP case for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred accessioning the case.");
        }
    }

    // ─── Gross Description ────────────────────────────────────────────────────

    [HttpPost("{patientId}/cases/{caseId}/gross")]
    public async Task<IActionResult> RecordGross(
        string patientId, string caseId, [FromBody] RecordGrossRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).RecordAPGrossDescriptionAsync(
                caseId,
                req.GrossDescription,
                req.PathologistId,
                req.PathologistName,
                req.SpecimenPartCount,
                req.SpecimenWeightGrams,
                req.FrozenSectionDiagnosis);
            return Ok(new { message = "Gross description recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording gross description for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred recording the gross description.");
        }
    }

    // ─── Microscopic Description ──────────────────────────────────────────────

    [HttpPost("{patientId}/cases/{caseId}/microscopic")]
    public async Task<IActionResult> RecordMicroscopic(
        string patientId, string caseId, [FromBody] RecordMicroscopicRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).RecordAPMicroscopicDescriptionAsync(caseId, req.MicroscopicDescription);
            return Ok(new { message = "Microscopic description recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording microscopic description for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred recording the microscopic description.");
        }
    }

    // ─── Diagnosis / Sign-Out ─────────────────────────────────────────────────

    [HttpPost("{patientId}/cases/{caseId}/signout")]
    public async Task<IActionResult> SignOut(
        string patientId, string caseId, [FromBody] SignOutAPRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).SignOutAPDiagnosisAsync(
                caseId,
                req.Diagnosis,
                req.DiagnosisCodes,
                req.PathologistId,
                req.PathologistName,
                req.SignOutDateTime);
            return Ok(new { message = "Case signed out." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing out case {CaseId}", caseId);
            return StatusCode(500, "An error occurred signing out the case.");
        }
    }

    [HttpPost("{patientId}/cases/{caseId}/preliminary")]
    public async Task<IActionResult> IssuePreliminary(
        string patientId, string caseId, [FromBody] PreliminaryAPRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).IssueAPPreliminaryDiagnosisAsync(
                caseId, req.PreliminaryDiagnosis, req.PathologistId, req.PathologistName);
            return Ok(new { message = "Preliminary diagnosis issued." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing preliminary diagnosis for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred issuing the preliminary diagnosis.");
        }
    }

    // ─── Addendum / Amendment ─────────────────────────────────────────────────

    [HttpPost("{patientId}/cases/{caseId}/addendum")]
    public async Task<IActionResult> AddAddendum(
        string patientId, string caseId, [FromBody] APAddendumRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).AddAPAddendumAsync(
                caseId, req.AddendumText, req.PathologistId, req.PathologistName);
            return Ok(new { message = "Addendum appended." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding addendum to case {CaseId}", caseId);
            return StatusCode(500, "An error occurred adding the addendum.");
        }
    }

    [HttpPost("{patientId}/cases/{caseId}/amend")]
    public async Task<IActionResult> AmendDiagnosis(
        string patientId, string caseId, [FromBody] AmendAPRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).AmendAPDiagnosisAsync(
                caseId,
                req.CorrectedDiagnosis,
                req.CorrectedCodes,
                req.AmendmentReason,
                req.PathologistId,
                req.PathologistName);
            return Ok(new { message = "Diagnosis amended." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error amending diagnosis for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred amending the diagnosis.");
        }
    }

    // ─── Cytology Details ─────────────────────────────────────────────────────

    [HttpPost("{patientId}/cases/{caseId}/cytology")]
    public async Task<IActionResult> RecordCytology(
        string patientId, string caseId, [FromBody] CytologyDetailsRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).RecordAPCytologyDetailsAsync(
                caseId, req.BethesdaCategory, req.SpecimenAdequacy);
            return Ok(new { message = "Cytology details recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording cytology details for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred recording cytology details.");
        }
    }

    // ─── Autopsy Findings ─────────────────────────────────────────────────────

    [HttpPost("{patientId}/cases/{caseId}/autopsy")]
    public async Task<IActionResult> RecordAutopsy(
        string patientId, string caseId, [FromBody] AutopsyFindingsRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).RecordAPAutopsyFindingsAsync(
                caseId,
                req.CauseOfDeath,
                req.UnderlyingCauseOfDeath,
                req.MannerOfDeath,
                req.ToxicologyFindings,
                req.BodyWeightKg,
                req.NeuropathologyFindings);
            return Ok(new { message = "Autopsy findings recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording autopsy findings for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred recording autopsy findings.");
        }
    }
}

// ─── DTOs ──────────────────────────────────────────────────────────────────────

public record APCaseIndexDto(
    string CaseId,
    string AccessionNumber,
    string CaseType,
    string Status,
    DateTime? DateReceived,
    DateTime? DateReported,
    string? SpecimenSource,
    string? PrimaryDiagnosis,
    string? PathologistName)
{
    public APCaseIndexDto(APCaseIndexEntry e) : this(
        e.CaseId,
        e.AccessionNumber,
        e.CaseType.ToString(),
        e.Status.ToString(),
        e.DateReceived,
        e.DateReported,
        e.SpecimenSource,
        e.PrimaryDiagnosis,
        e.PathologistName) { }
}

public record APCaseDetailDto(
    string CaseId,
    string PatientId,
    string AccessionNumber,
    string CaseType,
    string Status,
    string? SpecimenSource,
    string? SpecimenDescription,
    string? SpecimenType,
    int? SpecimenPartCount,
    decimal? SpecimenWeightGrams,
    string? ClinicalHistory,
    string? ClinicalDiagnosis,
    string? ReferringProviderName,
    string? CollectionLocation,
    DateTime? DateCollected,
    DateTime? DateReceived,
    DateTime? DateReported,
    string? GrossDescription,
    string? GrossPathologistName,
    DateTime? GrossExamDateTime,
    string? MicroscopicDescription,
    string? Diagnosis,
    List<string> DiagnosisCodes,
    string? PathologistName,
    DateTime? SignOutDateTime,
    List<string> SpecialStains,
    List<string> ImmunohistochemistryResults,
    string? FrozenSectionDiagnosis,
    string? BethesdaCategory,
    string? SpecimenAdequacy,
    string? CauseOfDeath,
    string? UnderlyingCauseOfDeath,
    string? MannerOfDeath,
    string? ToxicologyFindings,
    decimal? BodyWeightKg,
    string? NeuropathologyFindings,
    string? Addendum,
    DateTime? AddendumDate,
    string? AddendumPathologistName,
    string? AmendmentReason,
    string? Comments)
{
    public APCaseDetailDto(AnatomicPathologyState s) : this(
        s.CaseId, s.PatientId, s.AccessionNumber,
        s.CaseType.ToString(), s.Status.ToString(),
        s.SpecimenSource, s.SpecimenDescription, s.SpecimenType,
        s.SpecimenPartCount, s.SpecimenWeightGrams,
        s.ClinicalHistory, s.ClinicalDiagnosis,
        s.ReferringProviderName, s.CollectionLocation,
        s.DateCollected, s.DateReceived, s.DateReported,
        s.GrossDescription, s.GrossPathologistName, s.GrossExamDateTime,
        s.MicroscopicDescription,
        s.Diagnosis, s.DiagnosisCodes,
        s.PathologistName, s.SignOutDateTime,
        s.SpecialStains, s.ImmunohistochemistryResults,
        s.FrozenSectionDiagnosis,
        s.BethesdaCategory, s.SpecimenAdequacy,
        s.CauseOfDeath, s.UnderlyingCauseOfDeath,
        s.MannerOfDeath?.ToString(),
        s.ToxicologyFindings, s.BodyWeightKg, s.NeuropathologyFindings,
        s.Addendum, s.AddendumDate, s.AddendumPathologistName,
        s.AmendmentReason, s.Comments) { }
}

public record AccessionAPCaseRequest(
    APCaseType CaseType,
    string AccessionNumber,
    string? SpecimenSource,
    string? SpecimenDescription,
    string? SpecimenType,
    string? ClinicalHistory,
    string? ClinicalDiagnosis,
    string? ReferringProviderId,
    string? ReferringProviderName,
    string? CollectionLocation,
    DateTime? DateCollected,
    DateTime DateReceived);

public record RecordGrossRequest(
    string GrossDescription,
    string? PathologistId,
    string? PathologistName,
    int? SpecimenPartCount,
    decimal? SpecimenWeightGrams,
    string? FrozenSectionDiagnosis);

public record RecordMicroscopicRequest(string MicroscopicDescription);

public record SignOutAPRequest(
    string Diagnosis,
    List<string> DiagnosisCodes,
    string PathologistId,
    string PathologistName,
    DateTime SignOutDateTime);

public record PreliminaryAPRequest(
    string PreliminaryDiagnosis,
    string PathologistId,
    string PathologistName);

public record APAddendumRequest(
    string AddendumText,
    string PathologistId,
    string PathologistName);

public record AmendAPRequest(
    string CorrectedDiagnosis,
    List<string> CorrectedCodes,
    string AmendmentReason,
    string PathologistId,
    string PathologistName);

public record CytologyDetailsRequest(
    string? BethesdaCategory,
    string? SpecimenAdequacy);

public record AutopsyFindingsRequest(
    string? CauseOfDeath,
    string? UnderlyingCauseOfDeath,
    MannerOfDeath? MannerOfDeath,
    string? ToxicologyFindings,
    decimal? BodyWeightKg,
    string? NeuropathologyFindings);
