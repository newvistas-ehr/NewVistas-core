// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Patient Portal Controller — §170.315(e)(2) Secure Messaging + §170.315(e)(3) Patient Health Information Capture.
///
/// Implements:
///   - Patient-submitted health information (demographics, medications, allergies, etc.)
///   - Clinician review workflow (submitted → under-review → accepted/rejected/partial)
///   - Bidirectional secure messaging between patients and care team
///   - Provider message queue for threads needing attention
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize]
public class PatientPortalController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PatientPortalController> _logger;

    public PatientPortalController(IGrainFactory grainFactory, ILogger<PatientPortalController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── Patient Submissions — §170.315(e)(3) ─────────────────────────────────

    /// <summary>POST api/portal/submissions — Patient submits health information.</summary>
    [HttpPost("submissions")]
    public async Task<IActionResult> CreateSubmission([FromBody] PatientSubmissionState submission)
    {
        try
        {
            string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
            submission.SubmissionId = submissionId;
            submission.SubmittedDate = DateTime.UtcNow;

            // Count sections submitted
            int sectionCount = 0;
            if (submission.Demographics != null) sectionCount++;
            if (submission.HealthConcerns.Count > 0) sectionCount++;
            if (submission.Medications.Count > 0) sectionCount++;
            if (submission.Allergies.Count > 0) sectionCount++;
            if (submission.SocialHistory != null) sectionCount++;
            if (submission.FamilyHistory.Count > 0) sectionCount++;
            if (submission.AdvanceDirective != null) sectionCount++;
            if (submission.HealthGoals.Count > 0) sectionCount++;

            // Save submission
            IPatientSubmissionGrain grain = _grainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);
            await grain.CreateSubmissionAsync(submission);

            var summary = new PatientSubmissionSummary
            {
                SubmissionId = submissionId,
                PatientId = submission.PatientId,
                PatientName = submission.PatientName,
                SubmittedDate = submission.SubmittedDate,
                Status = "submitted",
                SectionCount = sectionCount
            };

            // Add to patient index
            IPatientSubmissionIndexGrain patientIndex = _grainFactory.GetGrain<IPatientSubmissionIndexGrain>(
                $"PATIENT-SUB-IDX:{submission.PatientId}");
            await patientIndex.AddSubmissionAsync(summary);

            // Add to review queue
            IPatientSubmissionQueueGrain queue = _grainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            await queue.AddSubmissionAsync(summary);

            return Created($"api/portal/submissions/{submissionId}", new { submissionId, status = "submitted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating patient submission");
            return StatusCode(500, "An error occurred creating the submission.");
        }
    }

    /// <summary>GET api/portal/submissions/{submissionId} — Get submission details.</summary>
    [HttpGet("submissions/{submissionId}")]
    public async Task<IActionResult> GetSubmission(string submissionId)
    {
        try
        {
            IPatientSubmissionGrain grain = _grainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);
            PatientSubmissionState submission = await grain.GetSubmissionAsync();
            if (string.IsNullOrEmpty(submission.PatientId))
                return NotFound($"Submission {submissionId} not found.");
            return Ok(submission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting submission {SubmissionId}", submissionId);
            return StatusCode(500, "An error occurred getting the submission.");
        }
    }

    /// <summary>GET api/portal/submissions/patient/{patientId} — All submissions for a patient.</summary>
    [HttpGet("submissions/patient/{patientId}")]
    public async Task<IActionResult> GetPatientSubmissions(string patientId)
    {
        try
        {
            IPatientSubmissionIndexGrain index = _grainFactory.GetGrain<IPatientSubmissionIndexGrain>(
                $"PATIENT-SUB-IDX:{patientId}");
            List<PatientSubmissionSummary> submissions = await index.GetAllSubmissionsAsync();
            return Ok(submissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing submissions for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing submissions.");
        }
    }

    /// <summary>GET api/portal/submissions/queue — System-wide review queue (pending submissions).</summary>
    [HttpGet("submissions/queue")]
    public async Task<IActionResult> GetSubmissionQueue()
    {
        try
        {
            IPatientSubmissionQueueGrain queue = _grainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            List<PatientSubmissionSummary> pending = await queue.GetPendingSubmissionsAsync();
            return Ok(pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing submission queue");
            return StatusCode(500, "An error occurred listing the submission queue.");
        }
    }

    /// <summary>GET api/portal/submissions/queue/all — All submissions in queue.</summary>
    [HttpGet("submissions/queue/all")]
    public async Task<IActionResult> GetAllQueuedSubmissions()
    {
        try
        {
            IPatientSubmissionQueueGrain queue = _grainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            List<PatientSubmissionSummary> all = await queue.GetAllSubmissionsAsync();
            return Ok(all);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all queued submissions");
            return StatusCode(500, "An error occurred listing submissions.");
        }
    }

    /// <summary>PUT api/portal/submissions/{submissionId}/review — Mark submission as under review.</summary>
    [HttpPut("submissions/{submissionId}/review")]
    public async Task<IActionResult> MarkUnderReview(string submissionId, [FromBody] MarkReviewRequest request)
    {
        try
        {
            IPatientSubmissionGrain grain = _grainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);
            await grain.MarkUnderReviewAsync(request.ReviewerId);

            // Update queue status
            IPatientSubmissionQueueGrain queue = _grainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            await queue.UpdateStatusAsync(submissionId, "under-review");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking submission {SubmissionId} under review", submissionId);
            return StatusCode(500, "An error occurred updating the submission.");
        }
    }

    /// <summary>PUT api/portal/submissions/{submissionId}/complete — Complete review of submission.</summary>
    [HttpPut("submissions/{submissionId}/complete")]
    public async Task<IActionResult> CompleteReview(string submissionId, [FromBody] CompleteSubmissionReviewRequest request)
    {
        try
        {
            IPatientSubmissionGrain grain = _grainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);
            await grain.CompleteReviewAsync(
                request.Status, request.ReviewerId, request.ReviewNotes,
                request.AcceptedSections, request.RejectedSections);

            // Get submission to find patient ID
            PatientSubmissionState sub = await grain.GetSubmissionAsync();

            // Update patient index
            IPatientSubmissionIndexGrain patientIndex = _grainFactory.GetGrain<IPatientSubmissionIndexGrain>(
                $"PATIENT-SUB-IDX:{sub.PatientId}");
            await patientIndex.UpdateStatusAsync(submissionId, request.Status);

            // Update queue
            IPatientSubmissionQueueGrain queue = _grainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            await queue.UpdateStatusAsync(submissionId, request.Status);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing review for submission {SubmissionId}", submissionId);
            return StatusCode(500, "An error occurred completing the review.");
        }
    }

    // ─── Secure Messaging — §170.315(e)(2) ────────────────────────────────────

    /// <summary>POST api/portal/messages/threads — Create a new message thread.</summary>
    [HttpPost("messages/threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
    {
        try
        {
            // Validate that assigned provider is on the patient's care team
            if (!string.IsNullOrEmpty(request.AssignedProviderId))
            {
                ICareTeamGrain careTeam = _grainFactory.GetGrain<ICareTeamGrain>(
                    $"CARE-TEAM:{request.PatientId}");
                bool isMember = await careTeam.HasActiveMemberAsync(request.AssignedProviderId);
                if (!isMember)
                    return BadRequest("Assigned provider is not on the patient's care team.");
            }

            string threadId = $"SECURE-MSG-THREAD:{Guid.NewGuid():N}";

            ISecureMessageThreadGrain grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
            await grain.CreateThreadAsync(
                request.PatientId, request.PatientName, request.Subject,
                request.Category, request.AssignedProviderId, request.AssignedProviderName);

            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = request.PatientId,
                PatientName = request.PatientName,
                Subject = request.Subject,
                Category = request.Category,
                Status = "open",
                LastMessageDate = DateTime.UtcNow,
                MessageCount = 0,
                HasUnreadPatient = false,
                HasUnreadProvider = false
            };

            // Add to patient index
            ISecureMessageIndexGrain patientIndex = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{request.PatientId}");
            await patientIndex.AddThreadAsync(summary);

            // Add to provider queue
            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queue.AddThreadAsync(summary);

            return Created($"api/portal/messages/threads/{threadId}", new { threadId, status = "open" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating message thread");
            return StatusCode(500, "An error occurred creating the thread.");
        }
    }

    /// <summary>POST api/portal/messages/threads/{threadId}/messages — Add a message to a thread.</summary>
    [HttpPost("messages/threads/{threadId}/messages")]
    public async Task<IActionResult> AddMessage(string threadId, [FromBody] AddMessageRequest request)
    {
        try
        {
            ISecureMessageThreadGrain grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
            await grain.AddMessageAsync(request.SenderType, request.SenderId, request.SenderName, request.Body);

            // Get updated thread for index sync
            SecureMessageThreadState thread = await grain.GetThreadAsync();

            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = thread.PatientId,
                PatientName = thread.PatientName,
                Subject = thread.Subject,
                Category = thread.Category,
                Status = thread.Status,
                LastMessageDate = thread.LastMessageDate,
                MessageCount = thread.Messages.Count,
                HasUnreadPatient = thread.HasUnreadPatient,
                HasUnreadProvider = thread.HasUnreadProvider
            };

            // Update patient index
            ISecureMessageIndexGrain patientIndex = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{thread.PatientId}");
            await patientIndex.UpdateThreadAsync(summary);

            // Update provider queue
            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queue.UpdateThreadAsync(summary);

            return Ok(new { threadId, messageCount = thread.Messages.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message to thread {ThreadId}", threadId);
            return StatusCode(500, "An error occurred adding the message.");
        }
    }

    /// <summary>GET api/portal/messages/threads/{threadId} — Get thread with all messages.</summary>
    [HttpGet("messages/threads/{threadId}")]
    public async Task<IActionResult> GetThread(string threadId)
    {
        try
        {
            ISecureMessageThreadGrain grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
            SecureMessageThreadState thread = await grain.GetThreadAsync();
            if (string.IsNullOrEmpty(thread.PatientId))
                return NotFound($"Thread {threadId} not found.");
            return Ok(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thread {ThreadId}", threadId);
            return StatusCode(500, "An error occurred getting the thread.");
        }
    }

    /// <summary>PUT api/portal/messages/threads/{threadId}/read — Mark messages as read.</summary>
    [HttpPut("messages/threads/{threadId}/read")]
    public async Task<IActionResult> MarkRead(string threadId, [FromBody] MarkReadRequest request)
    {
        try
        {
            ISecureMessageThreadGrain grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
            await grain.MarkReadAsync(request.ReaderType);

            // Sync index
            SecureMessageThreadState thread = await grain.GetThreadAsync();
            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = thread.PatientId,
                PatientName = thread.PatientName,
                Subject = thread.Subject,
                Category = thread.Category,
                Status = thread.Status,
                LastMessageDate = thread.LastMessageDate,
                MessageCount = thread.Messages.Count,
                HasUnreadPatient = thread.HasUnreadPatient,
                HasUnreadProvider = thread.HasUnreadProvider
            };

            ISecureMessageIndexGrain patientIndex = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{thread.PatientId}");
            await patientIndex.UpdateThreadAsync(summary);

            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queue.UpdateThreadAsync(summary);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking thread {ThreadId} as read", threadId);
            return StatusCode(500, "An error occurred marking the thread as read.");
        }
    }

    /// <summary>PUT api/portal/messages/threads/{threadId}/close — Close a thread.</summary>
    [HttpPut("messages/threads/{threadId}/close")]
    public async Task<IActionResult> CloseThread(string threadId)
    {
        try
        {
            ISecureMessageThreadGrain grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
            await grain.CloseThreadAsync();

            SecureMessageThreadState thread = await grain.GetThreadAsync();
            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = thread.PatientId,
                PatientName = thread.PatientName,
                Subject = thread.Subject,
                Category = thread.Category,
                Status = "closed",
                LastMessageDate = thread.LastMessageDate,
                MessageCount = thread.Messages.Count,
                HasUnreadPatient = thread.HasUnreadPatient,
                HasUnreadProvider = thread.HasUnreadProvider
            };

            ISecureMessageIndexGrain patientIndex = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{thread.PatientId}");
            await patientIndex.UpdateThreadAsync(summary);

            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queue.RemoveThreadAsync(threadId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing thread {ThreadId}", threadId);
            return StatusCode(500, "An error occurred closing the thread.");
        }
    }

    /// <summary>PUT api/portal/messages/threads/{threadId}/reopen — Reopen a closed thread.</summary>
    [HttpPut("messages/threads/{threadId}/reopen")]
    public async Task<IActionResult> ReopenThread(string threadId)
    {
        try
        {
            ISecureMessageThreadGrain grain = _grainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
            await grain.ReopenThreadAsync();

            SecureMessageThreadState thread = await grain.GetThreadAsync();
            var summary = new SecureMessageThreadSummary
            {
                ThreadId = threadId,
                PatientId = thread.PatientId,
                PatientName = thread.PatientName,
                Subject = thread.Subject,
                Category = thread.Category,
                Status = "open",
                LastMessageDate = thread.LastMessageDate,
                MessageCount = thread.Messages.Count,
                HasUnreadPatient = thread.HasUnreadPatient,
                HasUnreadProvider = thread.HasUnreadProvider
            };

            ISecureMessageIndexGrain patientIndex = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{thread.PatientId}");
            await patientIndex.UpdateThreadAsync(summary);

            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            await queue.AddThreadAsync(summary);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reopening thread {ThreadId}", threadId);
            return StatusCode(500, "An error occurred reopening the thread.");
        }
    }

    /// <summary>GET api/portal/messages/patient/{patientId} — All threads for a patient.</summary>
    [HttpGet("messages/patient/{patientId}")]
    public async Task<IActionResult> GetPatientThreads(string patientId)
    {
        try
        {
            ISecureMessageIndexGrain index = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{patientId}");
            List<SecureMessageThreadSummary> threads = await index.GetAllThreadsAsync();
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing threads for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing threads.");
        }
    }

    /// <summary>GET api/portal/messages/patient/{patientId}/unread — Unread threads for a patient.</summary>
    [HttpGet("messages/patient/{patientId}/unread")]
    public async Task<IActionResult> GetPatientUnreadThreads(string patientId)
    {
        try
        {
            ISecureMessageIndexGrain index = _grainFactory.GetGrain<ISecureMessageIndexGrain>(
                $"SECURE-MSG-IDX:{patientId}");
            List<SecureMessageThreadSummary> threads = await index.GetUnreadByPatientAsync();
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing unread threads for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing threads.");
        }
    }

    /// <summary>GET api/portal/messages/queue — Provider queue (unread threads).</summary>
    [HttpGet("messages/queue")]
    public async Task<IActionResult> GetProviderQueue()
    {
        try
        {
            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            List<SecureMessageThreadSummary> threads = await queue.GetUnreadThreadsAsync();
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing provider message queue");
            return StatusCode(500, "An error occurred listing the message queue.");
        }
    }

    /// <summary>GET api/portal/messages/queue/active — All active threads.</summary>
    [HttpGet("messages/queue/active")]
    public async Task<IActionResult> GetActiveThreads()
    {
        try
        {
            ISecureMessageQueueGrain queue = _grainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
            List<SecureMessageThreadSummary> threads = await queue.GetAllActiveThreadsAsync();
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active threads");
            return StatusCode(500, "An error occurred listing threads.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public record MarkReviewRequest(string ReviewerId);

public record CompleteSubmissionReviewRequest(
    string Status,
    string ReviewerId,
    string? ReviewNotes,
    List<string> AcceptedSections,
    List<string> RejectedSections);

public record CreateThreadRequest(
    string PatientId,
    string? PatientName,
    string Subject,
    string Category,
    string? AssignedProviderId,
    string? AssignedProviderName);

public record AddMessageRequest(
    string SenderType,
    string? SenderId,
    string? SenderName,
    string Body);

public record MarkReadRequest(string ReaderType);
