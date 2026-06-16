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
/// REST API for Quality Management: occurrence screening, peer reviews, and root cause analysis.
/// VistA File #680 (OCCURRENCE SCREEN). PXRM.m, QMEVNT.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class QualityManagementController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<QualityManagementController> _logger;

    public QualityManagementController(IGrainFactory grainFactory, ILogger<QualityManagementController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IQMIncidentIndexGrain IncidentIndex()
        => _grainFactory.GetGrain<IQMIncidentIndexGrain>("QM-INCIDENT-IDX");

    private IQMIncidentGrain Incident(string incidentId)
        => _grainFactory.GetGrain<IQMIncidentGrain>(Uri.UnescapeDataString(incidentId));

    private IQMReviewIndexGrain ReviewIndex()
        => _grainFactory.GetGrain<IQMReviewIndexGrain>("QM-REVIEW-IDX");

    private IQMReviewGrain Review(string reviewId)
        => _grainFactory.GetGrain<IQMReviewGrain>(Uri.UnescapeDataString(reviewId));

    private static QMIncidentIndexEntry BuildIndexEntry(QMIncidentState s) => new()
    {
        IncidentId = s.IncidentId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        OccurrenceDate = s.OccurrenceDate,
        Category = s.Category,
        Severity = s.Severity,
        Status = s.Status,
        Location = s.Location,
        WardUnit = s.WardUnit,
        ReviewCount = s.ReviewIds.Count
    };

    private static QMReviewIndexEntry BuildReviewIndexEntry(QMReviewState s) => new()
    {
        ReviewId = s.ReviewId,
        IncidentId = s.IncidentId,
        ReviewType = s.ReviewType,
        Status = s.Status,
        ReviewerName = s.ReviewerName,
        AssignedTo = s.AssignedTo,
        DueDate = s.DueDate,
        CompletedDate = s.CompletedDate,
        ActionItemCount = s.ActionItems.Count
    };

    // ── Incidents ─────────────────────────────────────────────────────────────

    [HttpGet("incidents")]
    public async Task<IActionResult> GetAllIncidents()
    {
        try
        {
            return Ok(await IncidentIndex().GetAllIncidentsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all incidents");
            return StatusCode(500, "Error retrieving incidents.");
        }
    }

    [HttpGet("incidents/severity/{severity}")]
    public async Task<IActionResult> GetIncidentsBySeverity(OccurrenceSeverity severity)
    {
        try
        {
            return Ok(await IncidentIndex().GetIncidentsBySeverityAsync(severity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incidents by severity {Severity}", severity);
            return StatusCode(500, "Error retrieving incidents.");
        }
    }

    [HttpGet("incidents/status/{status}")]
    public async Task<IActionResult> GetIncidentsByStatus(IncidentStatus status)
    {
        try
        {
            return Ok(await IncidentIndex().GetIncidentsByStatusAsync(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incidents by status {Status}", status);
            return StatusCode(500, "Error retrieving incidents.");
        }
    }

    [HttpGet("incidents/patient/{patientId}")]
    public async Task<IActionResult> GetIncidentsByPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await IncidentIndex().GetIncidentsByPatientAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incidents for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving incidents.");
        }
    }

    [HttpGet("incidents/{incidentId}")]
    public async Task<IActionResult> GetIncident(string incidentId)
    {
        try
        {
            return Ok(await Incident(incidentId).GetIncidentAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incident {IncidentId}", incidentId);
            return StatusCode(500, "Error retrieving incident.");
        }
    }

    [HttpPost("incidents")]
    public async Task<IActionResult> ReportIncident([FromBody] ReportIncidentRequest request)
    {
        try
        {
            string incidentId = $"QM-INCIDENT:{Guid.NewGuid()}";
            await Incident(incidentId).ReportIncidentAsync(
                request.PatientId,
                request.PatientName,
                request.OccurrenceDate,
                request.Category,
                request.Description,
                request.Location,
                request.WardUnit,
                request.Severity,
                request.ReportedBy,
                request.ReportedByTitle,
                request.ImmediateAction,
                request.DiagnosisAtTime,
                request.ProcedureAtTime,
                request.MedicationInvolved,
                request.EquipmentInvolved);
            QMIncidentState state = await Incident(incidentId).GetIncidentAsync();
            await IncidentIndex().UpsertIncidentAsync(BuildIndexEntry(state));
            return Created($"/api/qualitymanagement/incidents/{Uri.EscapeDataString(incidentId)}", new { incidentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting incident for patient {PatientId}", request.PatientId);
            return StatusCode(500, "Error reporting incident.");
        }
    }

    [HttpPost("incidents/{incidentId}/outcome")]
    public async Task<IActionResult> UpdateOutcome(string incidentId, [FromBody] UpdateOutcomeRequest request)
    {
        try
        {
            await Incident(incidentId).UpdateOutcomeAsync(
                request.OutcomeDescription, request.PatientNotified, request.FamilyNotified);
            QMIncidentState state = await Incident(incidentId).GetIncidentAsync();
            await IncidentIndex().UpsertIncidentAsync(BuildIndexEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating outcome for incident {IncidentId}", incidentId);
            return StatusCode(500, "Error updating incident outcome.");
        }
    }

    [HttpPost("incidents/{incidentId}/close")]
    public async Task<IActionResult> CloseIncident(string incidentId)
    {
        try
        {
            await Incident(incidentId).CloseIncidentAsync();
            QMIncidentState state = await Incident(incidentId).GetIncidentAsync();
            await IncidentIndex().UpsertIncidentAsync(BuildIndexEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing incident {IncidentId}", incidentId);
            return StatusCode(500, "Error closing incident.");
        }
    }

    // ── Reviews ───────────────────────────────────────────────────────────────

    [HttpGet("reviews")]
    public async Task<IActionResult> GetAllReviews()
    {
        try
        {
            return Ok(await ReviewIndex().GetAllReviewsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all reviews");
            return StatusCode(500, "Error retrieving reviews.");
        }
    }

    [HttpGet("reviews/pending")]
    public async Task<IActionResult> GetPendingReviews()
    {
        try
        {
            return Ok(await ReviewIndex().GetPendingReviewsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending reviews");
            return StatusCode(500, "Error retrieving pending reviews.");
        }
    }

    [HttpGet("reviews/incident/{incidentId}")]
    public async Task<IActionResult> GetReviewsForIncident(string incidentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(incidentId);
            return Ok(await ReviewIndex().GetReviewsForIncidentAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for incident {IncidentId}", incidentId);
            return StatusCode(500, "Error retrieving reviews.");
        }
    }

    [HttpGet("reviews/{reviewId}")]
    public async Task<IActionResult> GetReview(string reviewId)
    {
        try
        {
            return Ok(await Review(reviewId).GetReviewAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review {ReviewId}", reviewId);
            return StatusCode(500, "Error retrieving review.");
        }
    }

    [HttpPost("reviews")]
    public async Task<IActionResult> AssignReview([FromBody] AssignReviewRequest request)
    {
        try
        {
            string reviewId = $"QM-REVIEW:{Guid.NewGuid()}";
            string incidentId = Uri.UnescapeDataString(request.IncidentId);
            await Review(reviewId).AssignReviewAsync(
                incidentId,
                request.ReviewType,
                request.AssignedTo,
                request.ReviewerName,
                request.ReviewerTitle,
                request.DueDate,
                request.Confidential);
            // Link review to incident and update incident status
            await Incident(incidentId).AddReviewToIncidentAsync(reviewId, request.ReviewType);
            QMIncidentState incidentState = await Incident(incidentId).GetIncidentAsync();
            await IncidentIndex().UpsertIncidentAsync(BuildIndexEntry(incidentState));
            QMReviewState reviewState = await Review(reviewId).GetReviewAsync();
            await ReviewIndex().UpsertReviewAsync(BuildReviewIndexEntry(reviewState));
            return Created($"/api/qualitymanagement/reviews/{Uri.EscapeDataString(reviewId)}", new { reviewId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning review for incident {IncidentId}", request.IncidentId);
            return StatusCode(500, "Error assigning review.");
        }
    }

    [HttpPost("reviews/{reviewId}/findings")]
    public async Task<IActionResult> RecordFindings(string reviewId, [FromBody] RecordFindingsRequest request)
    {
        try
        {
            await Review(reviewId).StartReviewAsync();
            await Review(reviewId).RecordFindingsAsync(
                request.Summary,
                request.PrimaryFinding,
                request.ContributingFactors,
                request.RootCause,
                request.SystemFailures,
                request.HumanFactors,
                request.EnvironmentalFactors);
            QMReviewState state = await Review(reviewId).GetReviewAsync();
            await ReviewIndex().UpsertReviewAsync(BuildReviewIndexEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording findings for review {ReviewId}", reviewId);
            return StatusCode(500, "Error recording findings.");
        }
    }

    [HttpPost("reviews/{reviewId}/complete")]
    public async Task<IActionResult> CompleteReview(string reviewId, [FromBody] CompleteReviewRequest request)
    {
        try
        {
            await Review(reviewId).CompleteReviewAsync(request.FinalConclusion, request.LessonsLearned);
            QMReviewState reviewState = await Review(reviewId).GetReviewAsync();
            await ReviewIndex().UpsertReviewAsync(BuildReviewIndexEntry(reviewState));
            // Update the linked incident's root cause flag
            string incidentId = reviewState.IncidentId;
            if (!string.IsNullOrEmpty(incidentId) && !string.IsNullOrEmpty(reviewState.RootCause))
            {
                await Incident(incidentId).SetRootCauseIdentifiedAsync(true, reviewState.FinalConclusion);
                QMIncidentState incidentState = await Incident(incidentId).GetIncidentAsync();
                await IncidentIndex().UpsertIncidentAsync(BuildIndexEntry(incidentState));
            }
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing review {ReviewId}", reviewId);
            return StatusCode(500, "Error completing review.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<QMIncidentIndexEntry> incidents = await IncidentIndex().GetAllIncidentsAsync();
            List<QMReviewIndexEntry> reviews = await ReviewIndex().GetAllReviewsAsync();
            DateTime now = DateTime.UtcNow;

            var dashboard = new
            {
                TotalIncidents = incidents.Count,
                OpenIncidents = incidents.Count(i => i.Status is IncidentStatus.Reported
                    or IncidentStatus.UnderReview or IncidentStatus.PeerReviewAssigned
                    or IncidentStatus.RCAInProgress),
                SevereHarmOrDeath = incidents.Count(i => i.Severity is OccurrenceSeverity.SevereHarm
                    or OccurrenceSeverity.Death),
                NearMisses = incidents.Count(i => i.Severity == OccurrenceSeverity.NearMiss),
                TotalReviews = reviews.Count,
                PendingReviews = reviews.Count(r => r.Status is QMReviewStatus.Pending
                    or QMReviewStatus.InProgress),
                OverdueReviews = reviews.Count(r => r.DueDate < now
                    && r.Status is QMReviewStatus.Pending or QMReviewStatus.InProgress),
                CompletedReviews = reviews.Count(r => r.Status is QMReviewStatus.Completed
                    or QMReviewStatus.Approved)
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QM dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public record ReportIncidentRequest(
    string PatientId,
    string PatientName,
    DateTime OccurrenceDate,
    OccurrenceCategory Category,
    string Description,
    string Location,
    string WardUnit,
    OccurrenceSeverity Severity,
    string ReportedBy,
    string ReportedByTitle,
    string ImmediateAction,
    string DiagnosisAtTime,
    string ProcedureAtTime,
    string MedicationInvolved,
    string EquipmentInvolved);

public record UpdateOutcomeRequest(
    string OutcomeDescription,
    bool PatientNotified,
    bool FamilyNotified);

public record AssignReviewRequest(
    string IncidentId,
    QMReviewType ReviewType,
    string AssignedTo,
    string ReviewerName,
    string ReviewerTitle,
    DateTime DueDate,
    bool Confidential);

public record RecordFindingsRequest(
    string Summary,
    ReviewFinding PrimaryFinding,
    List<string> ContributingFactors,
    string RootCause,
    List<string> SystemFailures,
    string HumanFactors,
    string EnvironmentalFactors);

public record CompleteReviewRequest(
    string FinalConclusion,
    string LessonsLearned);
