// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Audit Trail API — per-patient audit event log.
///
/// Mirrors VistA AUDIT file (#1.1) query routines from the Kernel package:
///   - XUSEC LOG  — write an audit record
///   - XUSEC QUERY — retrieve audit records by file/date filters
/// </summary>
[Authorize(Policy = "CanViewAuditTrail")]
[ApiController]
[Route("api/patient/{patientId}/audit")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IGrainFactory grainFactory, ILogger<AuditController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// Get audit events for a patient, with optional domain and date-range filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AuditEventSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditEventSummary>>> GetAuditEvents(
        string patientId,
        [FromQuery] string? domain = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            var events = await GetWorkflow(patientId).GetAuditEventsAsync(domain, from, to, maxResults);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit events for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving audit events");
        }
    }

    /// <summary>
    /// Get all audit events for a specific entity (e.g., one order or one lab test).
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    [ProducesResponseType(typeof(List<AuditEventSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditEventSummary>>> GetEventsByEntity(
        string patientId,
        string entityType,
        string entityId)
    {
        try
        {
            var events = await GetWorkflow(patientId).GetAuditEventsByEntityAsync(entityType, entityId);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit events for entity {EntityType}/{EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while retrieving audit events");
        }
    }

    /// <summary>
    /// Get the full detail of a single audit event by its event ID.
    /// </summary>
    [HttpGet("{eventId}")]
    [ProducesResponseType(typeof(AuditEventState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditEventState>> GetEvent(string patientId, string eventId)
    {
        try
        {
            var auditEvent = await GetWorkflow(patientId).GetAuditEventAsync(eventId);
            if (string.IsNullOrEmpty(auditEvent.PatientId))
                return NotFound(new { Message = $"Audit event '{eventId}' not found" });

            return Ok(auditEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit event {EventId}", eventId);
            return StatusCode(500, "An error occurred while retrieving the audit event");
        }
    }

    /// <summary>
    /// Manually log an audit event (for administrative or integration use).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LogAuditResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> LogAuditEvent(string patientId, [FromBody] LogAuditRequest request)
    {
        try
        {
            var eventId = await GetWorkflow(patientId).LogAuditEventAsync(
                request.Domain, request.Action,
                request.EntityType, request.EntityId,
                request.UserId, request.UserName,
                request.LocationId, request.LocationName,
                request.Details, request.OldValue, request.NewValue);

            _logger.LogInformation(
                "Audit event {EventId} logged: {Domain}/{Action} on {EntityType}/{EntityId} for patient {PatientId}",
                eventId, request.Domain, request.Action, request.EntityType, request.EntityId, patientId);

            return Created($"api/patient/{patientId}/audit/{eventId}",
                new LogAuditResponse { EventId = eventId, Message = "Audit event recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while logging the audit event");
        }
    }
}

public class LogAuditRequest
{
    public string Domain { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? Details { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class LogAuditResponse
{
    public string EventId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
