// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.PatientPortal.Controllers;

/// <summary>
/// Patient-scoped health information submissions.
/// All endpoints derive patientId from the JWT — no patientId in URLs.
/// §170.315(e)(3) — Patient Health Information Capture.
/// </summary>
[ApiController]
[Route("api/my/submissions")]
[Authorize]
public class MySubmissionsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MySubmissionsController> _logger;

    public MySubmissionsController(IGrainFactory grainFactory, ILogger<MySubmissionsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private string GetPatientId()
        => User.FindFirstValue("patient_id")
            ?? throw new InvalidOperationException("patient_id claim not found.");

    /// <summary>Get all my submissions.</summary>
    [HttpGet]
    public async Task<ActionResult> GetMySubmissions()
    {
        try
        {
            string patientId = GetPatientId();
            var indexGrain = _grainFactory.GetGrain<IPatientSubmissionIndexGrain>($"PATIENT-SUB-IDX:{patientId}");
            List<PatientSubmissionSummary> submissions = await indexGrain.GetAllSubmissionsAsync();
            return Ok(submissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting submissions");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get a specific submission (only if it belongs to me).</summary>
    [HttpGet("{submissionId}")]
    public async Task<ActionResult> GetSubmission(string submissionId)
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientSubmissionGrain>($"PATIENT-SUB:{submissionId}");
            PatientSubmissionState submission = await grain.GetSubmissionAsync();

            // Enforce ownership — patient can only see their own submissions
            if (submission.PatientId != patientId)
                return Forbid();

            return Ok(submission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting submission {Id}", submissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Submit new health information.</summary>
    [HttpPost]
    public async Task<ActionResult> CreateSubmission([FromBody] PatientSubmissionState submission)
    {
        try
        {
            string patientId = GetPatientId();

            // Force the patientId from the JWT — ignore whatever the client sent
            submission.PatientId = patientId;
            submission.SubmissionId = $"SUB-{Guid.NewGuid()}";
            submission.SubmittedDate = DateTime.UtcNow;
            submission.Status = "submitted";

            var grain = _grainFactory.GetGrain<IPatientSubmissionGrain>($"PATIENT-SUB:{submission.SubmissionId}");
            await grain.CreateSubmissionAsync(submission);

            // Count sections for the summary
            int sectionCount = 0;
            if (submission.Demographics != null) sectionCount++;
            if (submission.HealthConcerns?.Count > 0) sectionCount++;
            if (submission.Medications?.Count > 0) sectionCount++;
            if (submission.Allergies?.Count > 0) sectionCount++;
            if (submission.SocialHistory != null) sectionCount++;
            if (submission.FamilyHistory?.Count > 0) sectionCount++;
            if (submission.AdvanceDirective != null) sectionCount++;
            if (submission.HealthGoals?.Count > 0) sectionCount++;

            var summary = new PatientSubmissionSummary
            {
                SubmissionId = submission.SubmissionId,
                PatientId = patientId,
                PatientName = submission.PatientName,
                SubmittedDate = submission.SubmittedDate,
                Status = "submitted",
                SectionCount = sectionCount
            };

            // Add to patient index and system queue
            var indexGrain = _grainFactory.GetGrain<IPatientSubmissionIndexGrain>($"PATIENT-SUB-IDX:{patientId}");
            await indexGrain.AddSubmissionAsync(summary);

            var queueGrain = _grainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            await queueGrain.AddSubmissionAsync(summary);

            _logger.LogInformation("Patient {PatientId} created submission {SubmissionId}", patientId, submission.SubmissionId);
            return Created("", new { submission.SubmissionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating submission");
            return StatusCode(500, "An error occurred.");
        }
    }
}
