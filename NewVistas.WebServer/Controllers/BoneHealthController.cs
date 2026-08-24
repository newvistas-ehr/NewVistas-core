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
/// Per-patient bone health / osteoporosis surface.
///
/// Everything routes through the workflow grain, which owns the feature gate, the audit
/// hooks, and the demographics needed to pick the right diagnostic rule. Reads are open
/// to any authenticated clinician; writes carry the workflow grain's audit attributes.
/// </summary>
[Authorize]
[ApiController]
[Route("api/bonehealth")]
public class BoneHealthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BoneHealthController> _logger;

    public BoneHealthController(IGrainFactory grainFactory, ILogger<BoneHealthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>GET api/bonehealth/{patientId}/snapshot — computed bone-health view.</summary>
    [HttpGet("{patientId}/snapshot")]
    public async Task<IActionResult> GetSnapshot(string patientId)
    {
        try
        {
            BoneHealthSnapshot snapshot = await GetWorkflow(patientId).GetBoneHealthSnapshotAsync();
            return Ok(snapshot);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bone health snapshot for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>GET api/bonehealth/{patientId} — the raw longitudinal record.</summary>
    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetRecord(string patientId)
    {
        try
        {
            BoneHealthState record = await GetWorkflow(patientId).GetBoneHealthRecordAsync();
            return Ok(record);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bone health record for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>POST api/bonehealth/{patientId}/dxa — record a DXA study.</summary>
    [HttpPost("{patientId}/dxa")]
    public async Task<IActionResult> RecordDxa(string patientId, [FromBody] DxaScan scan)
    {
        try
        {
            string id = await GetWorkflow(patientId).RecordDxaScanAsync(scan);
            return Created($"api/bonehealth/{patientId}", new { ScanId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording DXA scan for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>
    /// POST api/bonehealth/{patientId}/turnover-marker — record a bone turnover marker.
    /// Collection conditions (fasting, time of day) should be supplied: without them the
    /// result is recorded but flagged as not comparable with others.
    /// </summary>
    [HttpPost("{patientId}/turnover-marker")]
    public async Task<IActionResult> RecordTurnoverMarker(string patientId, [FromBody] BoneTurnoverMarkerResult result)
    {
        try
        {
            string id = await GetWorkflow(patientId).RecordBoneTurnoverMarkerAsync(result);
            return Created($"api/bonehealth/{patientId}", new { ResultId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording turnover marker for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>POST api/bonehealth/{patientId}/fracture — record a fracture.</summary>
    [HttpPost("{patientId}/fracture")]
    public async Task<IActionResult> RecordFracture(string patientId, [FromBody] BoneFracture fracture)
    {
        try
        {
            string id = await GetWorkflow(patientId).RecordBoneFractureAsync(fracture);
            return Created($"api/bonehealth/{patientId}", new { FractureId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording fracture for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>POST api/bonehealth/{patientId}/therapy — start a therapy course.</summary>
    [HttpPost("{patientId}/therapy")]
    public async Task<IActionResult> StartTherapy(string patientId, [FromBody] OsteoporosisTherapyCourse course)
    {
        try
        {
            string id = await GetWorkflow(patientId).StartOsteoporosisTherapyAsync(course);
            return Created($"api/bonehealth/{patientId}", new { CourseId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting osteoporosis therapy for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>POST api/bonehealth/{patientId}/therapy/{courseId}/stop — stop a therapy course.</summary>
    [HttpPost("{patientId}/therapy/{courseId}/stop")]
    public async Task<IActionResult> StopTherapy(string patientId, string courseId, [FromBody] StopTherapyRequest request)
    {
        try
        {
            await GetWorkflow(patientId).StopOsteoporosisTherapyAsync(
                courseId, request.StopDate, request.StopReason, request.TransitionedToAgent);
            return Ok(new { Message = "Therapy stopped." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping osteoporosis therapy {CourseId} for {PatientId}", courseId, patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>POST api/bonehealth/{patientId}/frax — record a FRAX assessment.</summary>
    [HttpPost("{patientId}/frax")]
    public async Task<IActionResult> RecordFrax(string patientId, [FromBody] FraxAssessment assessment)
    {
        try
        {
            string id = await GetWorkflow(patientId).RecordFraxAssessmentAsync(assessment);
            return Created($"api/bonehealth/{patientId}", new { AssessmentId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording FRAX assessment for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }

    /// <summary>POST api/bonehealth/{patientId}/secondary-workup — record a secondary-cause workup.</summary>
    [HttpPost("{patientId}/secondary-workup")]
    public async Task<IActionResult> RecordSecondaryWorkup(string patientId, [FromBody] SecondaryCauseWorkup workup)
    {
        try
        {
            string id = await GetWorkflow(patientId).RecordBoneSecondaryWorkupAsync(workup);
            return Created($"api/bonehealth/{patientId}", new { WorkupId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { Error = "This operation requires the PROVIDER security key." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording secondary workup for {PatientId}", patientId);
            return StatusCode(500, new { Error = "An error occurred." });
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

/// <summary>Body for stopping a therapy course.</summary>
public record StopTherapyRequest
{
    /// <summary>Date therapy stopped.</summary>
    public required DateTime StopDate { get; init; }

    /// <summary>Why it was stopped.</summary>
    public string? StopReason { get; init; }

    /// <summary>Follow-on agent, where therapy was transitioned rather than simply ceased.</summary>
    public string? TransitionedToAgent { get; init; }
}
