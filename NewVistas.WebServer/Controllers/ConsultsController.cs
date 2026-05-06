// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Consults API — REQUEST/CONSULTATION file (#123).
///
/// Mirrors VistA consult workflow (GMRCACTM.m):
///   Request → Accept → Schedule → Complete (with result note)
///   Request → Cancel / Discontinue
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/consults")]
[Produces("application/json")]
public class ConsultsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ConsultsController> _logger;

    public ConsultsController(IGrainFactory grainFactory, ILogger<ConsultsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// List consults for a patient. Optionally filter by status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ConsultSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ConsultSummary>>> GetConsults(
        string patientId,
        [FromQuery] string? status = null,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            var consults = await GetWorkflow(patientId).GetConsultsAsync(status, maxResults);
            return Ok(consults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consults for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving consults");
        }
    }

    /// <summary>
    /// Get a specific consult by ID.
    /// </summary>
    [HttpGet("{consultId}")]
    [ProducesResponseType(typeof(ConsultState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultState>> GetConsult(string patientId, string consultId)
    {
        try
        {
            var consult = await GetWorkflow(patientId).GetConsultAsync(consultId);
            if (string.IsNullOrEmpty(consult.PatientId))
                return NotFound(new { Message = $"Consult '{consultId}' not found" });

            return Ok(consult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while retrieving the consult");
        }
    }

    /// <summary>
    /// Request a new consult.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConsultResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestConsult(string patientId, [FromBody] RequestConsultRequest request)
    {
        try
        {
            var consultId = await GetWorkflow(patientId).RequestConsultAsync(
                request.ToService, request.ToServiceId,
                request.FromService, request.FromServiceId,
                request.Urgency ?? "ROUTINE",
                request.RequestingProviderId, request.RequestingProviderName,
                request.AttentionProviderId, request.AttentionProviderName,
                request.ReasonForRequest, request.ProvisionalDiagnosis,
                request.OrderId, request.LocationId, request.LocationName);

            _logger.LogInformation(
                "Requested consult {ConsultId} to {ToService} for patient {PatientId}",
                consultId, request.ToService, patientId);

            return Created($"api/patient/{patientId}/consults/{consultId}",
                new ConsultResponse { ConsultId = consultId, Message = "Consult requested successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting consult for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while requesting the consult");
        }
    }

    /// <summary>
    /// Accept a consult. PENDING → ACTIVE.
    /// </summary>
    [HttpPost("{consultId}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptConsult(string patientId, string consultId)
    {
        try
        {
            await GetWorkflow(patientId).AcceptConsultAsync(consultId);
            return Ok(new { ConsultId = consultId, Message = "Consult accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while accepting the consult");
        }
    }

    /// <summary>
    /// Schedule a consult. ACTIVE → SCHEDULED.
    /// </summary>
    [HttpPost("{consultId}/schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ScheduleConsult(string patientId, string consultId)
    {
        try
        {
            await GetWorkflow(patientId).ScheduleConsultAsync(consultId);
            return Ok(new { ConsultId = consultId, Message = "Consult scheduled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while scheduling the consult");
        }
    }

    /// <summary>
    /// Complete a consult with an optional result note.
    /// </summary>
    [HttpPost("{consultId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteConsult(string patientId, string consultId, [FromBody] CompleteConsultRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CompleteConsultAsync(
                consultId, request.ResultNoteText,
                request.AuthorId, request.AuthorName);
            return Ok(new { ConsultId = consultId, Message = "Consult completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while completing the consult");
        }
    }

    /// <summary>
    /// Cancel a consult.
    /// </summary>
    [HttpPost("{consultId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelConsult(string patientId, string consultId, [FromBody] ConsultCommentRequest? request)
    {
        try
        {
            await GetWorkflow(patientId).CancelConsultAsync(consultId, request?.Comments);
            return Ok(new { ConsultId = consultId, Message = "Consult cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while cancelling the consult");
        }
    }

    /// <summary>
    /// Discontinue a consult.
    /// </summary>
    [HttpPost("{consultId}/discontinue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DiscontinueConsult(string patientId, string consultId, [FromBody] ConsultCommentRequest? request)
    {
        try
        {
            await GetWorkflow(patientId).DiscontinueConsultAsync(consultId, request?.Comments);
            return Ok(new { ConsultId = consultId, Message = "Consult discontinued" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while discontinuing the consult");
        }
    }

    // ─── Phase 4 Depth Endpoints ─────────────────────────────────────────

    private IConsultGrain ConsultGrain(string consultId) =>
        _grainFactory.GetGrain<IConsultGrain>(consultId);

    /// <summary>
    /// Add a tracking comment to a consult.
    /// </summary>
    [HttpPost("{consultId}/tracking-comment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddTrackingComment(string patientId, string consultId, [FromBody] TrackingCommentRequest request)
    {
        try
        {
            await ConsultGrain(consultId).AddTrackingCommentAsync(
                request.AuthorId, request.AuthorName, request.CommentText, request.ActionTaken);
            return Ok(new { ConsultId = consultId, Message = "Tracking comment added" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracking comment for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while adding the tracking comment");
        }
    }

    /// <summary>
    /// Record accept details for a consult.
    /// </summary>
    [HttpPost("{consultId}/accept-details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptWithDetails(string patientId, string consultId, [FromBody] AcceptDetailsRequest request)
    {
        try
        {
            await ConsultGrain(consultId).AcceptWithDetailsAsync(request.AcceptedById, request.AcceptedByName);
            return Ok(new { ConsultId = consultId, Message = "Accept details recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording accept details for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while recording accept details");
        }
    }

    /// <summary>
    /// Record schedule details for a consult.
    /// </summary>
    [HttpPost("{consultId}/schedule-details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ScheduleWithDetails(string patientId, string consultId, [FromBody] ScheduleDetailsRequest request)
    {
        try
        {
            await ConsultGrain(consultId).ScheduleWithDetailsAsync(
                request.ScheduledDateTime, request.ClinicId, request.ClinicName);
            return Ok(new { ConsultId = consultId, Message = "Schedule details recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording schedule details for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while recording schedule details");
        }
    }

    /// <summary>
    /// Set the consult type.
    /// </summary>
    [HttpPost("{consultId}/consult-type")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetConsultType(string patientId, string consultId, [FromBody] ConsultTypeRequest request)
    {
        try
        {
            await ConsultGrain(consultId).SetConsultTypeAsync(request.ConsultType);
            return Ok(new { ConsultId = consultId, Message = "Consult type set" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting consult type for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while setting the consult type");
        }
    }

    /// <summary>
    /// Set the clinical history for a consult.
    /// </summary>
    [HttpPost("{consultId}/clinical-history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetClinicalHistory(string patientId, string consultId, [FromBody] ClinicalHistoryRequest request)
    {
        try
        {
            await ConsultGrain(consultId).SetClinicalHistoryAsync(request.ClinicalHistory);
            return Ok(new { ConsultId = consultId, Message = "Clinical history recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting clinical history for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while setting the clinical history");
        }
    }

    /// <summary>
    /// Set follow-up recommendation for a consult.
    /// </summary>
    [HttpPost("{consultId}/follow-up")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetFollowUp(string patientId, string consultId, [FromBody] FollowUpRequest request)
    {
        try
        {
            await ConsultGrain(consultId).SetFollowUpRecommendationAsync(request.Recommendation);
            return Ok(new { ConsultId = consultId, Message = "Follow-up recommendation recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting follow-up for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while setting the follow-up recommendation");
        }
    }

    /// <summary>
    /// Set the consulting provider for a consult.
    /// </summary>
    [HttpPost("{consultId}/consulting-provider")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetConsultingProvider(string patientId, string consultId, [FromBody] ConsultingProviderRequest request)
    {
        try
        {
            await ConsultGrain(consultId).SetConsultingProviderAsync(request.ProviderId, request.ProviderName);
            return Ok(new { ConsultId = consultId, Message = "Consulting provider set" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting consulting provider for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while setting the consulting provider");
        }
    }

    /// <summary>
    /// Mark a consult as interfacility.
    /// </summary>
    [HttpPost("{consultId}/interfacility")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkInterfacility(string patientId, string consultId, [FromBody] InterfacilityRequest request)
    {
        try
        {
            await ConsultGrain(consultId).MarkInterfacilityAsync(request.ExternalFacilityId, request.ExternalFacilityName);
            return Ok(new { ConsultId = consultId, Message = "Marked as interfacility" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking interfacility for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while marking as interfacility");
        }
    }

    /// <summary>
    /// Get tracking comments for a consult.
    /// </summary>
    [HttpGet("{consultId}/tracking-comments")]
    [ProducesResponseType(typeof(List<ConsultTrackingComment>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrackingComments(string patientId, string consultId)
    {
        try
        {
            var comments = await ConsultGrain(consultId).GetTrackingCommentsAsync();
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracking comments for consult {ConsultId}", consultId);
            return StatusCode(500, "An error occurred while retrieving tracking comments");
        }
    }
}

public class RequestConsultRequest
{
    public string ToService { get; set; } = string.Empty;
    public string? ToServiceId { get; set; }
    public string? FromService { get; set; }
    public string? FromServiceId { get; set; }
    public string? Urgency { get; set; }
    public string? RequestingProviderId { get; set; }
    public string? RequestingProviderName { get; set; }
    public string? AttentionProviderId { get; set; }
    public string? AttentionProviderName { get; set; }
    public string? ReasonForRequest { get; set; }
    public string? ProvisionalDiagnosis { get; set; }
    public string? OrderId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
}

public class CompleteConsultRequest
{
    public string? ResultNoteText { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
}

public class ConsultCommentRequest
{
    public string? Comments { get; set; }
}

public class ConsultResponse
{
    public string ConsultId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public record TrackingCommentRequest
{
    public string AuthorId { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string CommentText { get; init; } = string.Empty;
    public string? ActionTaken { get; init; }
}

public record AcceptDetailsRequest
{
    public string AcceptedById { get; init; } = string.Empty;
    public string AcceptedByName { get; init; } = string.Empty;
}

public record ScheduleDetailsRequest
{
    public DateTime ScheduledDateTime { get; init; }
    public string? ClinicId { get; init; }
    public string? ClinicName { get; init; }
}

public record ConsultTypeRequest
{
    public string ConsultType { get; init; } = string.Empty;
}

public record ClinicalHistoryRequest
{
    public string ClinicalHistory { get; init; } = string.Empty;
}

public record FollowUpRequest
{
    public string Recommendation { get; init; } = string.Empty;
}

public record ConsultingProviderRequest
{
    public string ProviderId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
}

public record InterfacilityRequest
{
    public string ExternalFacilityId { get; init; } = string.Empty;
    public string ExternalFacilityName { get; init; } = string.Empty;
}
