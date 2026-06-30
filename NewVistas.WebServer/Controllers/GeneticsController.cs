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
/// Hereditary Genetics &amp; Family History API — a patient's interpreted genomics record
/// (genetic test reports + reportable variants) and structured family history, plus the
/// hereditary-risk findings and red flags derived from them.
///
/// All operations are patient-scoped and route through <see cref="IPatientWorkflowGrain"/>
/// keyed by the <c>{patientId}</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GeneticsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<GeneticsController> _logger;

    public GeneticsController(IGrainFactory grainFactory, ILogger<GeneticsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Genomics Reads ──────────────────────────────────────────────────────

    /// <summary>Returns the patient's genomics profile (all recorded genetic test reports + variants).</summary>
    [HttpGet("{patientId}/genomics")]
    [ProducesResponseType(typeof(GenomicsState), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenomicsState>> GetGenomics(string patientId)
    {
        try
        {
            GenomicsState profile = await GetWorkflow(patientId).GetGenomicsProfileAsync();
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving genomics profile for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the genomics profile");
        }
    }

    /// <summary>Returns the hereditary findings derived from the patient's genomics record.</summary>
    [HttpGet("{patientId}/hereditary-findings")]
    [ProducesResponseType(typeof(List<Abstractions.Clinical.HereditaryFinding>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Abstractions.Clinical.HereditaryFinding>>> GetHereditaryFindings(string patientId)
    {
        try
        {
            List<Abstractions.Clinical.HereditaryFinding> findings =
                await GetWorkflow(patientId).GetHereditaryFindingsAsync();
            return Ok(findings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hereditary findings for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the hereditary findings");
        }
    }

    // ─── Report Record / Variant / Remove ────────────────────────────────────

    /// <summary>Records a genetic test report for the patient. Returns the report id.</summary>
    [HttpPost("{patientId}/reports")]
    [ProducesResponseType(typeof(RecordGeneticReportResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecordGeneticReportResponse>> RecordReport(
        string patientId, [FromBody] RecordGeneticReportRequest request)
    {
        try
        {
            string reportId = await GetWorkflow(patientId).RecordGeneticTestReportAsync(
                request.TestName,
                request.Lab,
                request.Method,
                request.Indication,
                request.CollectionDate,
                request.ReportDate,
                request.OverallResult,
                request.OrderingProvider ?? string.Empty,
                request.Notes ?? string.Empty,
                request.RecordedBy ?? string.Empty);
            return Created($"/api/genetics/{patientId}/reports/{reportId}",
                new RecordGeneticReportResponse { ReportId = reportId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording genetic test report for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording the genetic test report");
        }
    }

    /// <summary>Adds a reportable variant to an existing genetic test report.</summary>
    [HttpPost("{patientId}/reports/{reportId}/variants")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddVariant(
        string patientId, string reportId, [FromBody] AddGeneticVariantRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AddGeneticVariantAsync(
                reportId,
                request.Gene,
                request.HgvsCoding,
                request.HgvsProtein,
                request.Transcript ?? string.Empty,
                request.Classification,
                request.Zygosity,
                request.Origin,
                request.ClinVarId ?? string.Empty,
                request.DbSnpId ?? string.Empty,
                request.Notes ?? string.Empty);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding genetic variant to report {ReportId} for patient {PatientId}",
                reportId, patientId);
            return StatusCode(500, "An error occurred while adding the genetic variant");
        }
    }

    /// <summary>Removes a genetic test report (and its variants) from the patient's record.</summary>
    [HttpDelete("{patientId}/reports/{reportId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveReport(string patientId, string reportId)
    {
        try
        {
            await GetWorkflow(patientId).RemoveGeneticReportAsync(reportId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing genetic report {ReportId} for patient {PatientId}",
                reportId, patientId);
            return StatusCode(500, "An error occurred while removing the genetic report");
        }
    }

    // ─── Family History Reads ────────────────────────────────────────────────

    /// <summary>Returns the patient's structured family history (all recorded relatives).</summary>
    [HttpGet("{patientId}/family-history")]
    [ProducesResponseType(typeof(FamilyHistoryState), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyHistoryState>> GetFamilyHistory(string patientId)
    {
        try
        {
            FamilyHistoryState history = await GetWorkflow(patientId).GetFamilyHistoryAsync();
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family history for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the family history");
        }
    }

    /// <summary>Returns the hereditary risk red flags derived from the patient's family history.</summary>
    [HttpGet("{patientId}/family-risk-flags")]
    [ProducesResponseType(typeof(List<Abstractions.Clinical.FamilyRiskFlag>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Abstractions.Clinical.FamilyRiskFlag>>> GetFamilyRiskFlags(string patientId)
    {
        try
        {
            List<Abstractions.Clinical.FamilyRiskFlag> flags =
                await GetWorkflow(patientId).GetFamilyRiskFlagsAsync();
            return Ok(flags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family risk flags for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the family risk flags");
        }
    }

    // ─── Family Member Add / Condition / Remove ──────────────────────────────

    /// <summary>Adds a family member (relative) to the patient's family history. Returns the member id.</summary>
    [HttpPost("{patientId}/family-members")]
    [ProducesResponseType(typeof(AddFamilyMemberResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AddFamilyMemberResponse>> AddFamilyMember(
        string patientId, [FromBody] AddFamilyMemberRequest request)
    {
        try
        {
            string memberId = await GetWorkflow(patientId).AddFamilyMemberAsync(
                request.Relationship,
                request.Name ?? string.Empty,
                request.Sex ?? string.Empty,
                request.VitalStatus,
                request.AgeYears,
                request.AgeAtDeath,
                request.CauseOfDeath ?? string.Empty,
                request.Notes ?? string.Empty);
            return Created($"/api/genetics/{patientId}/family-members/{memberId}",
                new AddFamilyMemberResponse { MemberId = memberId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding family member for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding the family member");
        }
    }

    /// <summary>Adds a condition (with age at diagnosis) to an existing family member.</summary>
    [HttpPost("{patientId}/family-members/{memberId}/conditions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddFamilyCondition(
        string patientId, string memberId, [FromBody] AddFamilyConditionRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AddFamilyConditionAsync(
                memberId,
                request.Condition,
                request.Code ?? string.Empty,
                request.AgeAtDiagnosis,
                request.Notes ?? string.Empty);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding family condition to member {MemberId} for patient {PatientId}",
                memberId, patientId);
            return StatusCode(500, "An error occurred while adding the family condition");
        }
    }

    /// <summary>Removes a family member (and their conditions) from the patient's family history.</summary>
    [HttpDelete("{patientId}/family-members/{memberId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFamilyMember(string patientId, string memberId)
    {
        try
        {
            await GetWorkflow(patientId).RemoveFamilyMemberAsync(memberId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing family member {MemberId} for patient {PatientId}",
                memberId, patientId);
            return StatusCode(500, "An error occurred while removing the family member");
        }
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────────────

public record RecordGeneticReportRequest
{
    public string TestName { get; init; } = string.Empty;
    public string Lab { get; init; } = string.Empty;
    public GeneticTestMethod Method { get; init; }
    public string Indication { get; init; } = string.Empty;
    public DateTime? CollectionDate { get; init; }
    public DateTime? ReportDate { get; init; }
    public GeneticReportResult OverallResult { get; init; }
    public string? OrderingProvider { get; init; }
    public string? Notes { get; init; }
    public string? RecordedBy { get; init; }
}

public record RecordGeneticReportResponse
{
    public string ReportId { get; init; } = string.Empty;
}

public record AddGeneticVariantRequest
{
    public string Gene { get; init; } = string.Empty;
    public string HgvsCoding { get; init; } = string.Empty;
    public string HgvsProtein { get; init; } = string.Empty;
    public string? Transcript { get; init; }
    public VariantClassification Classification { get; init; }
    public VariantZygosity Zygosity { get; init; }
    public VariantOrigin Origin { get; init; }
    public string? ClinVarId { get; init; }
    public string? DbSnpId { get; init; }
    public string? Notes { get; init; }
}

public record AddFamilyMemberRequest
{
    public FamilyRelationship Relationship { get; init; }
    public string? Name { get; init; }
    public string? Sex { get; init; }
    public FamilyVitalStatus VitalStatus { get; init; }
    public int? AgeYears { get; init; }
    public int? AgeAtDeath { get; init; }
    public string? CauseOfDeath { get; init; }
    public string? Notes { get; init; }
}

public record AddFamilyMemberResponse
{
    public string MemberId { get; init; } = string.Empty;
}

public record AddFamilyConditionRequest
{
    public string Condition { get; init; } = string.Empty;
    public string? Code { get; init; }
    public int? AgeAtDiagnosis { get; init; }
    public string? Notes { get; init; }
}
