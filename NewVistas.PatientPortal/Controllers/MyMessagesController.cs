// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.PatientPortal.Controllers;

/// <summary>
/// Patient-scoped secure messaging.
/// All endpoints derive patientId from the JWT — no patientId in URLs.
/// §170.315(e)(2) — Secure Messaging.
/// </summary>
[ApiController]
[Route("api/my/messages")]
[Authorize]
public class MyMessagesController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MyMessagesController> _logger;

    public MyMessagesController(IGrainFactory grainFactory, ILogger<MyMessagesController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private string GetPatientId()
        => User.FindFirstValue("patient_id")
            ?? throw new InvalidOperationException("patient_id claim not found.");

    private string GetPatientName()
        => User.FindFirstValue(ClaimTypes.Name) ?? "Patient";

    /// <summary>Get all my message threads.</summary>
    [HttpGet("threads")]
    public async Task<ActionResult> GetMyThreads()
    {
        try
        {
            string patientId = GetPatientId();
            var indexGrain = _grainFactory.GetGrain<ISecureMessageIndexGrain>($"SECURE-MSG-IDX:{patientId}");
            List<SecureMessageThreadSummary> threads = await indexGrain.GetAllThreadsAsync();
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message threads");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my unread threads.</summary>
    [HttpGet("threads/unread")]
    public async Task<ActionResult> GetUnreadThreads()
    {
        try
        {
            string patientId = GetPatientId();
            var indexGrain = _grainFactory.GetGrain<ISecureMessageIndexGrain>($"SECURE-MSG-IDX:{patientId}");
            List<SecureMessageThreadSummary> threads = await indexGrain.GetUnreadByPatientAsync();
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread threads");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get a specific thread (only if it belongs to me).</summary>
    [HttpGet("threads/{threadId}")]
    public async Task<ActionResult> GetThread(string threadId)
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>($"SECURE-MSG-THREAD:{threadId}");
            SecureMessageThreadState thread = await grain.GetThreadAsync();

            if (thread.PatientId != patientId)
                return Forbid();

            return Ok(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thread {Id}", threadId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Create a new message thread.</summary>
    [HttpPost("threads")]
    public async Task<ActionResult> CreateThread([FromBody] CreatePatientThreadRequest request)
    {
        try
        {
            string patientId = GetPatientId();
            string patientName = GetPatientName();
            string threadId = $"MSG-{Guid.NewGuid()}";

            var grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>($"SECURE-MSG-THREAD:{threadId}");
            await grain.CreateThreadAsync(
                patientId, patientName, request.Subject, request.Category,
                request.AssignedProviderId, request.AssignedProviderName);

            // Send the initial message
            await grain.AddMessageAsync("patient", patientId, patientName, request.Body);

            SecureMessageThreadState thread = await grain.GetThreadAsync();
            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = patientId,
                PatientName = patientName,
                Subject = request.Subject,
                Category = request.Category,
                Status = "open",
                LastMessageDate = thread.LastMessageDate,
                MessageCount = 1,
                HasUnreadPatient = false,
                HasUnreadProvider = true
            };

            var indexGrain = _grainFactory.GetGrain<ISecureMessageIndexGrain>($"SECURE-MSG-IDX:{patientId}");
            await indexGrain.AddThreadAsync(summary);

            var queueGrain = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queueGrain.AddThreadAsync(summary);

            _logger.LogInformation("Patient {PatientId} created message thread {ThreadId}", patientId, threadId);
            return Created("", new { ThreadId = threadId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating message thread");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Add a reply to a thread (patient can only send as themselves).</summary>
    [HttpPost("threads/{threadId}/reply")]
    public async Task<ActionResult> ReplyToThread(string threadId, [FromBody] PatientReplyRequest request)
    {
        try
        {
            string patientId = GetPatientId();
            string patientName = GetPatientName();

            var grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>($"SECURE-MSG-THREAD:{threadId}");
            SecureMessageThreadState thread = await grain.GetThreadAsync();

            if (thread.PatientId != patientId)
                return Forbid();

            if (thread.Status != "open")
                return BadRequest(new { Error = "Thread is closed." });

            // Patient can only send as "patient" — enforced server-side
            await grain.AddMessageAsync("patient", patientId, patientName, request.Body);

            // Update index and queue
            thread = await grain.GetThreadAsync();
            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = patientId,
                PatientName = patientName,
                Subject = thread.Subject,
                Category = thread.Category,
                Status = thread.Status,
                LastMessageDate = thread.LastMessageDate,
                MessageCount = thread.Messages.Count,
                HasUnreadPatient = thread.HasUnreadPatient,
                HasUnreadProvider = thread.HasUnreadProvider
            };

            var indexGrain = _grainFactory.GetGrain<ISecureMessageIndexGrain>($"SECURE-MSG-IDX:{patientId}");
            await indexGrain.UpdateThreadAsync(summary);

            var queueGrain = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queueGrain.UpdateThreadAsync(summary);

            return Ok(new { Message = "Reply sent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to thread {Id}", threadId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Mark a thread as read (from patient's perspective).</summary>
    [HttpPut("threads/{threadId}/read")]
    public async Task<ActionResult> MarkRead(string threadId)
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>($"SECURE-MSG-THREAD:{threadId}");
            SecureMessageThreadState thread = await grain.GetThreadAsync();

            if (thread.PatientId != patientId)
                return Forbid();

            await grain.MarkReadAsync("patient");

            // Sync index
            thread = await grain.GetThreadAsync();
            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = patientId,
                Subject = thread.Subject,
                Category = thread.Category,
                Status = thread.Status,
                LastMessageDate = thread.LastMessageDate,
                MessageCount = thread.Messages.Count,
                HasUnreadPatient = thread.HasUnreadPatient,
                HasUnreadProvider = thread.HasUnreadProvider
            };

            var indexGrain = _grainFactory.GetGrain<ISecureMessageIndexGrain>($"SECURE-MSG-IDX:{patientId}");
            await indexGrain.UpdateThreadAsync(summary);

            return Ok(new { Message = "Marked as read." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking thread read {Id}", threadId);
            return StatusCode(500, "An error occurred.");
        }
    }
}

// ─── Request DTOs ───────────────────────────────────────────────────────────

public record CreatePatientThreadRequest
{
    public required string Subject { get; init; }
    public string Category { get; init; } = "general";
    public string? AssignedProviderId { get; init; }
    public string? AssignedProviderName { get; init; }
    public required string Body { get; init; }
}

public record PatientReplyRequest
{
    public required string Body { get; init; }
}
