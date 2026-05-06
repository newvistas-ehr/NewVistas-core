// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Women's Health API — VistA File #790 (WOMEN'S HEALTH).
///
/// Covers clinical notifications for mammography, Pap smears, contraception,
/// pregnancy/prenatal care, breast health, and menopause/hormone therapy.
/// Mirrors WOCT.m, WOCPAT.m Women's Health package routines.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/womenshealth")]
[Produces("application/json")]
public class WomensHealthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<WomensHealthController> _logger;

    public WomensHealthController(IGrainFactory grainFactory, ILogger<WomensHealthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── List / Get ────────────────────────────────────────────────────────────

    /// <summary>
    /// List all Women's Health notifications for a patient, optionally filtered by type.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<WomensHealthIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WomensHealthIndexEntry>>> GetNotifications(
        string patientId,
        [FromQuery] WomensHealthNotificationType? type = null)
    {
        try
        {
            List<WomensHealthIndexEntry> results = type.HasValue
                ? await GetWorkflow(patientId).GetWomensHealthNotificationsByTypeAsync(type.Value)
                : await GetWorkflow(patientId).GetWomensHealthNotificationsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Women's Health notifications for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving Women's Health notifications");
        }
    }

    /// <summary>
    /// Get all Women's Health notifications where follow-up is required.
    /// </summary>
    [HttpGet("followup")]
    [ProducesResponseType(typeof(List<WomensHealthIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WomensHealthIndexEntry>>> GetFollowUpRequired(string patientId)
    {
        try
        {
            List<WomensHealthIndexEntry> results =
                await GetWorkflow(patientId).GetWomensHealthFollowUpRequiredAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Women's Health follow-up list for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the follow-up list");
        }
    }

    /// <summary>
    /// Get the full state of a single Women's Health notification.
    /// </summary>
    [HttpGet("{notificationId}")]
    [ProducesResponseType(typeof(WomensHealthNotificationState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WomensHealthNotificationState>> GetNotification(
        string patientId, string notificationId)
    {
        try
        {
            WomensHealthNotificationState state =
                await GetWorkflow(patientId).GetWomensHealthNotificationAsync(notificationId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Notification '{notificationId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Women's Health notification {NotificationId}", notificationId);
            return StatusCode(500, "An error occurred while retrieving the notification");
        }
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Create a new Women's Health notification record.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WomensHealthResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNotification(
        string patientId,
        [FromBody] CreateWomensHealthNotificationRequest request)
    {
        try
        {
            string notificationId = await GetWorkflow(patientId).CreateWomensHealthNotificationAsync(
                request.NotificationType,
                request.ProcedureDate,
                request.ProviderId,
                request.ProviderName,
                request.LocationId,
                request.LocationName,
                request.MammographyResult,
                request.BiRadsScore,
                request.PapSmearResult,
                request.ContraceptiveMethod,
                request.GestationalAgeWeeks,
                request.EstimatedDueDate,
                request.PregnancyOutcome,
                request.FollowUpRequired,
                request.NextDueDate,
                request.IsRefusal,
                request.Notes);

            _logger.LogInformation(
                "Created Women's Health notification {NotificationId} (type={Type}) for patient {PatientId}",
                notificationId, request.NotificationType, patientId);

            return Created(
                $"api/patient/{patientId}/womenshealth/{notificationId}",
                new WomensHealthResponse { Id = notificationId, Message = "Notification created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Women's Health notification for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the notification");
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Mark a Women's Health notification as completed.
    /// </summary>
    [HttpPost("{notificationId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteNotification(
        string patientId, string notificationId,
        [FromBody] CompleteWomensHealthRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CompleteWomensHealthNotificationAsync(
                notificationId, request.FollowUpCompletedDate, request.Notes);
            return Ok(new { NotificationId = notificationId, Message = "Notification completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing Women's Health notification {NotificationId}", notificationId);
            return StatusCode(500, "An error occurred while completing the notification");
        }
    }

    /// <summary>
    /// Update the follow-up required flag and optional next due date.
    /// </summary>
    [HttpPost("{notificationId}/followup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetFollowUp(
        string patientId, string notificationId,
        [FromBody] SetWomensHealthFollowUpRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetWomensHealthFollowUpAsync(
                notificationId, request.Required, request.NextDueDate);
            return Ok(new { NotificationId = notificationId, Message = "Follow-up updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating follow-up for Women's Health notification {NotificationId}", notificationId);
            return StatusCode(500, "An error occurred while updating the follow-up");
        }
    }

    /// <summary>
    /// Cancel a Women's Health notification.
    /// </summary>
    [HttpPost("{notificationId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelNotification(string patientId, string notificationId)
    {
        try
        {
            await GetWorkflow(patientId).CancelWomensHealthNotificationAsync(notificationId);
            return Ok(new { NotificationId = notificationId, Message = "Notification cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Women's Health notification {NotificationId}", notificationId);
            return StatusCode(500, "An error occurred while cancelling the notification");
        }
    }
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public record CreateWomensHealthNotificationRequest
{
    public WomensHealthNotificationType NotificationType { get; init; }
    public DateTime ProcedureDate { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public MammographyResult? MammographyResult { get; init; }
    public int? BiRadsScore { get; init; }
    public PapSmearResult? PapSmearResult { get; init; }
    public string? ContraceptiveMethod { get; init; }
    public int? GestationalAgeWeeks { get; init; }
    public DateTime? EstimatedDueDate { get; init; }
    public string? PregnancyOutcome { get; init; }
    public bool FollowUpRequired { get; init; }
    public DateTime? NextDueDate { get; init; }
    public bool IsRefusal { get; init; }
    public string? Notes { get; init; }
}

public record CompleteWomensHealthRequest
{
    public DateTime? FollowUpCompletedDate { get; init; }
    public string? Notes { get; init; }
}

public record SetWomensHealthFollowUpRequest
{
    public bool Required { get; init; }
    public DateTime? NextDueDate { get; init; }
}

public class WomensHealthResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
