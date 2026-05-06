// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Social Work API — VistA File #707 (SOCIAL WORK ASSESSMENT).
///
/// Mirrors VistA social work workflows (SWRPATCH.m):
///   Assessments (psychosocial, discharge risk, homeless risk, etc.)
///   Referrals to community/VA services and case management tracking.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/socialwork")]
[Produces("application/json")]
public class SocialWorkController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SocialWorkController> _logger;

    public SocialWorkController(IGrainFactory grainFactory, ILogger<SocialWorkController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Assessments ───────────────────────────────────────────────────────────

    /// <summary>
    /// List all social work assessments for a patient.
    /// </summary>
    [HttpGet("assessments")]
    [ProducesResponseType(typeof(List<SocialWorkAssessmentIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SocialWorkAssessmentIndexEntry>>> GetAssessments(string patientId)
    {
        try
        {
            List<SocialWorkAssessmentIndexEntry> results =
                await GetWorkflow(patientId).GetSocialWorkAssessmentsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving social work assessments for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving social work assessments");
        }
    }

    /// <summary>
    /// Get a single social work assessment by ID.
    /// </summary>
    [HttpGet("assessments/{assessmentId}")]
    [ProducesResponseType(typeof(SocialWorkAssessmentState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialWorkAssessmentState>> GetAssessment(
        string patientId, string assessmentId)
    {
        try
        {
            SocialWorkAssessmentState state =
                await GetWorkflow(patientId).GetSocialWorkAssessmentAsync(assessmentId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Assessment '{assessmentId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving social work assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "An error occurred while retrieving the assessment");
        }
    }

    /// <summary>
    /// Create a new social work assessment.
    /// </summary>
    [HttpPost("assessments")]
    [ProducesResponseType(typeof(SocialWorkResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAssessment(
        string patientId,
        [FromBody] CreateSocialWorkAssessmentRequest request)
    {
        try
        {
            string assessmentId = await GetWorkflow(patientId).CreateSocialWorkAssessmentAsync(
                request.AssessmentType,
                request.AssessmentDate,
                request.SocialWorkerId,
                request.SocialWorkerName,
                request.RiskLevel,
                request.HousingStatus,
                request.EmploymentStatus,
                request.SocialSupport,
                request.FinancialStressors,
                request.SubstanceUseHistory,
                request.AbuseConcernsIdentified,
                request.SafetyPlanInPlace,
                request.AnticipatedDischargeDate,
                request.DischargeDisposition,
                request.DischargePlan,
                request.DischargeBarriers,
                request.Recommendations,
                request.Notes,
                request.LocationId,
                request.LocationName);

            _logger.LogInformation(
                "Created social work assessment {AssessmentId} (type={Type}) for patient {PatientId}",
                assessmentId, request.AssessmentType, patientId);

            return Created(
                $"api/patient/{patientId}/socialwork/assessments/{assessmentId}",
                new SocialWorkResponse { Id = assessmentId, Message = "Assessment created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating social work assessment for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the assessment");
        }
    }

    /// <summary>
    /// Complete/sign a draft social work assessment.
    /// </summary>
    [HttpPost("assessments/{assessmentId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteAssessment(
        string patientId, string assessmentId,
        [FromBody] CompleteSocialWorkAssessmentRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CompleteSocialWorkAssessmentAsync(
                assessmentId,
                request.CompletedDate,
                request.Recommendations,
                request.Notes);
            return Ok(new { AssessmentId = assessmentId, Message = "Assessment completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing social work assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "An error occurred while completing the assessment");
        }
    }

    /// <summary>
    /// Close a social work assessment.
    /// </summary>
    [HttpPost("assessments/{assessmentId}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseAssessment(
        string patientId, string assessmentId,
        [FromBody] CloseReasonRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CloseSocialWorkAssessmentAsync(assessmentId, request.Reason);
            return Ok(new { AssessmentId = assessmentId, Message = "Assessment closed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing social work assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "An error occurred while closing the assessment");
        }
    }

    // ── Referrals ─────────────────────────────────────────────────────────────

    /// <summary>
    /// List all social work referrals for a patient.
    /// </summary>
    [HttpGet("referrals")]
    [ProducesResponseType(typeof(List<SocialWorkReferralIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SocialWorkReferralIndexEntry>>> GetReferrals(
        string patientId,
        [FromQuery] SocialWorkReferralStatus? status = null)
    {
        try
        {
            List<SocialWorkReferralIndexEntry> results = status.HasValue
                ? await GetWorkflow(patientId).GetSocialWorkReferralsByStatusAsync(status.Value)
                : await GetWorkflow(patientId).GetSocialWorkReferralsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving social work referrals for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving social work referrals");
        }
    }

    /// <summary>
    /// Get a single social work referral by ID.
    /// </summary>
    [HttpGet("referrals/{referralId}")]
    [ProducesResponseType(typeof(SocialWorkReferralState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialWorkReferralState>> GetReferral(
        string patientId, string referralId)
    {
        try
        {
            SocialWorkReferralState state =
                await GetWorkflow(patientId).GetSocialWorkReferralAsync(referralId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Referral '{referralId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving social work referral {ReferralId}", referralId);
            return StatusCode(500, "An error occurred while retrieving the referral");
        }
    }

    /// <summary>
    /// Create a new social work referral.
    /// </summary>
    [HttpPost("referrals")]
    [ProducesResponseType(typeof(SocialWorkResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReferral(
        string patientId,
        [FromBody] CreateSocialWorkReferralRequest request)
    {
        try
        {
            string referralId = await GetWorkflow(patientId).CreateSocialWorkReferralAsync(
                request.ReferralDate,
                request.ReferralSource,
                request.ReferralReason,
                request.ServiceType,
                request.AgencyName,
                request.AgencyContact,
                request.AgencyPhone,
                request.SocialWorkerId,
                request.SocialWorkerName,
                request.FollowUpDate,
                request.AssessmentId,
                request.LocationId,
                request.LocationName,
                request.Comments);

            _logger.LogInformation(
                "Created social work referral {ReferralId} (service={ServiceType}) for patient {PatientId}",
                referralId, request.ServiceType, patientId);

            return Created(
                $"api/patient/{patientId}/socialwork/referrals/{referralId}",
                new SocialWorkResponse { Id = referralId, Message = "Referral created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating social work referral for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the referral");
        }
    }

    /// <summary>
    /// Update the status of a social work referral.
    /// </summary>
    [HttpPost("referrals/{referralId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReferralStatus(
        string patientId, string referralId,
        [FromBody] UpdateReferralStatusRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateSocialWorkReferralStatusAsync(
                referralId, request.Status, request.OutcomeNotes, request.FollowUpDate);
            return Ok(new { ReferralId = referralId, Message = "Referral status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating social work referral {ReferralId}", referralId);
            return StatusCode(500, "An error occurred while updating the referral");
        }
    }

    /// <summary>
    /// Close a social work referral.
    /// </summary>
    [HttpPost("referrals/{referralId}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseReferral(
        string patientId, string referralId,
        [FromBody] OutcomeNotesRequest? request)
    {
        try
        {
            await GetWorkflow(patientId).CloseSocialWorkReferralAsync(referralId, request?.OutcomeNotes);
            return Ok(new { ReferralId = referralId, Message = "Referral closed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing social work referral {ReferralId}", referralId);
            return StatusCode(500, "An error occurred while closing the referral");
        }
    }
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public record CreateSocialWorkAssessmentRequest
{
    public SocialWorkAssessmentType AssessmentType { get; init; }
    public DateTime AssessmentDate { get; init; }
    public string? SocialWorkerId { get; init; }
    public string? SocialWorkerName { get; init; }
    public SocialWorkRiskLevel RiskLevel { get; init; }
    public string? HousingStatus { get; init; }
    public string? EmploymentStatus { get; init; }
    public string? SocialSupport { get; init; }
    public string? FinancialStressors { get; init; }
    public string? SubstanceUseHistory { get; init; }
    public bool? AbuseConcernsIdentified { get; init; }
    public bool? SafetyPlanInPlace { get; init; }
    public DateTime? AnticipatedDischargeDate { get; init; }
    public string? DischargeDisposition { get; init; }
    public string? DischargePlan { get; init; }
    public List<string>? DischargeBarriers { get; init; }
    public string? Recommendations { get; init; }
    public string? Notes { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
}

public record CompleteSocialWorkAssessmentRequest
{
    public DateTime CompletedDate { get; init; }
    public string? Recommendations { get; init; }
    public string? Notes { get; init; }
}

public record CloseReasonRequest
{
    public string Reason { get; init; } = string.Empty;
}

public record CreateSocialWorkReferralRequest
{
    public DateTime ReferralDate { get; init; }
    public string? ReferralSource { get; init; }
    public string? ReferralReason { get; init; }
    public SocialWorkReferralServiceType ServiceType { get; init; }
    public string? AgencyName { get; init; }
    public string? AgencyContact { get; init; }
    public string? AgencyPhone { get; init; }
    public string? SocialWorkerId { get; init; }
    public string? SocialWorkerName { get; init; }
    public DateTime? FollowUpDate { get; init; }
    public string? AssessmentId { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? Comments { get; init; }
}

public record UpdateReferralStatusRequest
{
    public SocialWorkReferralStatus Status { get; init; }
    public string? OutcomeNotes { get; init; }
    public DateTime? FollowUpDate { get; init; }
}

public record OutcomeNotesRequest
{
    public string? OutcomeNotes { get; init; }
}

public class SocialWorkResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
