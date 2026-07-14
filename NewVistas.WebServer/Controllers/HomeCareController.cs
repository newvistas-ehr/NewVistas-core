// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Home-Based Care (HBPC) API — consumed by in-home mobile clients (Android / iPad).
///
/// Patient-scoped operations (episodes, plans, visits, assessments) route through
/// <see cref="IPatientWorkflowGrain"/>. Because the workflow grain is keyed by patient but the
/// episode / plan / visit / assessment ids are global, every patient-scoped route is prefixed
/// with <c>{patientId}/</c> so a workflow grain is always in hand. Facility-wide reads (the daily
/// visit schedule and caseload census) are NOT on the workflow grain and call the singleton
/// census / visit-index grains directly.
///
/// Writes require the HBHC MANAGER security key (enforced grain-side); reads are open.
///
/// VistA Files: #750 (Home Based Primary Care), #750.1 (Visits). MUMPS: HBPC.m, HBVISIT.m, HBH workload.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HomeCareController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HomeCareController> _logger;

    public HomeCareController(IGrainFactory grainFactory, ILogger<HomeCareController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IHomeCareCensusGrain GetCensus()
        => _grainFactory.GetGrain<IHomeCareCensusGrain>("HHC-CENSUS:DEFAULT");

    private IHomeVisitIndexGrain GetVisitIndex()
        => _grainFactory.GetGrain<IHomeVisitIndexGrain>("HHC-VISIT-INDEX");

    // ─── Episodes ────────────────────────────────────────────────────────────

    /// <summary>Admits the patient to a home-care program (HBPC) and opens an episode.</summary>
    [HttpPost("{patientId}/episodes")]
    [ProducesResponseType(typeof(AdmitHomeCareResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdmitHomeCareResponse>> AdmitToHomeCare(
        string patientId, [FromBody] AdmitHomeCareRequest request)
    {
        try
        {
            string episodeId = await GetWorkflow(patientId).AdmitToHomeCareAsync(
                request.ProgramType,
                request.AdmissionDate,
                request.AdmissionSource,
                request.ReferringProviderId,
                request.ReferringProviderName,
                request.PrimaryDiagnosisCode,
                request.PrimaryDiagnosisText,
                request.LevelOfCare,
                request.ClinicalNeedNarrative,
                request.PrimaryCaregiver,
                request.HomeAddress,
                request.DeliveryModel);
            return Created($"/api/homecare/{patientId}/episodes/{episodeId}",
                new AdmitHomeCareResponse { EpisodeId = episodeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error admitting patient {PatientId} to home care", patientId);
            return StatusCode(500, "An error occurred while admitting the patient to home care");
        }
    }

    /// <summary>Returns all home-care episodes (any status) for the patient.</summary>
    [HttpGet("{patientId}/episodes")]
    [ProducesResponseType(typeof(List<HomeCareCensusEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeCareCensusEntry>>> GetEpisodesForPatient(string patientId)
    {
        try
        {
            List<HomeCareCensusEntry> episodes = await GetWorkflow(patientId).GetHomeCareEpisodesForPatientAsync();
            return Ok(episodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-care episodes for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving home-care episodes");
        }
    }

    /// <summary>Returns the current Active home-care episode for the patient, or null.</summary>
    [HttpGet("{patientId}/episodes/active")]
    [ProducesResponseType(typeof(HomeCareEpisodeState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeCareEpisodeState?>> GetActiveEpisode(string patientId)
    {
        try
        {
            HomeCareEpisodeState? episode = await GetWorkflow(patientId).GetActiveHomeCareEpisodeAsync();
            return Ok(episode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active home-care episode for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the active home-care episode");
        }
    }

    /// <summary>Returns a single home-care episode.</summary>
    [HttpGet("{patientId}/episodes/{episodeId}")]
    [ProducesResponseType(typeof(HomeCareEpisodeState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeCareEpisodeState>> GetEpisode(string patientId, string episodeId)
    {
        try
        {
            HomeCareEpisodeState episode = await GetWorkflow(patientId).GetHomeCareEpisodeAsync(episodeId);
            return Ok(episode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-care episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while retrieving the home-care episode");
        }
    }

    // ─── Delivery model (who delivers): hospital vs agency; Hospital-at-Home ──

    /// <summary>Sets who delivers an episode (HospitalAtHome episodes are forced to HospitalProvided).</summary>
    [HttpPut("{patientId}/episodes/{episodeId}/delivery-model")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetDeliveryModel(
        string patientId, string episodeId, [FromBody] SetDeliveryModelRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetHomeCareDeliveryModelAsync(episodeId, request.DeliveryModel);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting delivery model for episode {EpisodeId} (patient {PatientId})", episodeId, patientId);
            return StatusCode(500, "An error occurred while setting the delivery model");
        }
    }

    /// <summary>Links an episode to a delivering home-health agency (switches it to ExternalAgency).</summary>
    [HttpPut("{patientId}/episodes/{episodeId}/agency")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkAgency(
        string patientId, string episodeId, [FromBody] LinkAgencyRequest request)
    {
        try
        {
            await GetWorkflow(patientId).LinkHomeCareAgencyAsync(
                episodeId, request.AgencyId, request.CoordinatorProviderId, request.CoordinatorName, request.ExternalReferralId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking agency {AgencyId} to episode {EpisodeId} (patient {PatientId})", request.AgencyId, episodeId, patientId);
            return StatusCode(500, "An error occurred while linking the agency");
        }
    }

    /// <summary>Records a coordinated-care milestone on an agency-delivered episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/agency/milestones")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAgencyMilestone(
        string patientId, string episodeId, [FromBody] AgencyMilestoneRequest request)
    {
        try
        {
            string milestoneId = await GetWorkflow(patientId).AddAgencyCareMilestoneAsync(
                episodeId, request.Type, request.Date, request.Note, request.RecordedById, request.RecordedByName);
            return Created($"/api/homecare/{patientId}/episodes/{episodeId}/agency/milestones/{milestoneId}",
                new { MilestoneId = milestoneId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding agency milestone to episode {EpisodeId} (patient {PatientId})", episodeId, patientId);
            return StatusCode(500, "An error occurred while adding the milestone");
        }
    }

    /// <summary>Sets the Hospital-at-Home acute-substitution context (the freed-bed source-admission link).</summary>
    [HttpPut("{patientId}/episodes/{episodeId}/hospital-at-home")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetHospitalAtHome(
        string patientId, string episodeId, [FromBody] HospitalAtHomeRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetHospitalAtHomeContextAsync(
                episodeId, request.SourceAdmissionId, request.SourceFacilityId, request.SourceFacilityName,
                request.SourceUnitId, request.SourceBedId, request.SubstitutionStartDate, request.ClinicalRationale);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hospital-at-Home context for episode {EpisodeId} (patient {PatientId})", episodeId, patientId);
            return StatusCode(500, "An error occurred while setting the Hospital-at-Home context");
        }
    }

    // ─── Care Team ───────────────────────────────────────────────────────────

    /// <summary>Assigns (upserts) an interdisciplinary team member to an episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/team")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AssignTeamMember(
        string patientId, string episodeId, [FromBody] AssignTeamMemberRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AssignHomeCareTeamMemberAsync(
                episodeId, request.ProviderId, request.Name, request.Discipline, request.RoleTitle, request.IsPrimary);
            return Created($"/api/homecare/{patientId}/episodes/{episodeId}/team/{request.ProviderId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning team member {ProviderId} to episode {EpisodeId} for patient {PatientId}",
                request.ProviderId, episodeId, patientId);
            return StatusCode(500, "An error occurred while assigning the team member");
        }
    }

    /// <summary>Removes a team member from an episode.</summary>
    [HttpDelete("{patientId}/episodes/{episodeId}/team/{providerId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTeamMember(string patientId, string episodeId, string providerId)
    {
        try
        {
            await GetWorkflow(patientId).RemoveHomeCareTeamMemberAsync(episodeId, providerId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing team member {ProviderId} from episode {EpisodeId} for patient {PatientId}",
                providerId, episodeId, patientId);
            return StatusCode(500, "An error occurred while removing the team member");
        }
    }

    // ─── Episode Lifecycle / Edits ───────────────────────────────────────────

    /// <summary>Updates the level of care for an episode.</summary>
    [HttpPut("{patientId}/episodes/{episodeId}/level")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateLevelOfCare(
        string patientId, string episodeId, [FromBody] UpdateLevelOfCareRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateHomeCareLevelOfCareAsync(episodeId, request.LevelOfCare);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating level of care for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while updating the level of care");
        }
    }

    /// <summary>Adds a secondary diagnosis to an episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/secondary-diagnoses")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddSecondaryDiagnosis(
        string patientId, string episodeId, [FromBody] AddSecondaryDiagnosisRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AddHomeCareSecondaryDiagnosisAsync(episodeId, request.Diagnosis);
            return Created($"/api/homecare/{patientId}/episodes/{episodeId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding secondary diagnosis to episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while adding the secondary diagnosis");
        }
    }

    /// <summary>Puts an episode on hold.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/hold")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutEpisodeOnHold(
        string patientId, string episodeId, [FromBody] HoldEpisodeRequest request)
    {
        try
        {
            await GetWorkflow(patientId).PutHomeCareEpisodeOnHoldAsync(episodeId, request.Reason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting episode {EpisodeId} on hold for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while putting the episode on hold");
        }
    }

    /// <summary>Reactivates an on-hold episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReactivateEpisode(string patientId, string episodeId)
    {
        try
        {
            await GetWorkflow(patientId).ReactivateHomeCareEpisodeAsync(episodeId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while reactivating the episode");
        }
    }

    /// <summary>Discharges the patient from a home-care episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/discharge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DischargeFromHomeCare(
        string patientId, string episodeId, [FromBody] HomeCareDischargeRequest request)
    {
        try
        {
            await GetWorkflow(patientId).DischargeFromHomeCareAsync(
                episodeId, request.DischargeDate, request.Reason, request.Notes);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while discharging the patient from home care");
        }
    }

    // ─── Plan of Care ────────────────────────────────────────────────────────

    /// <summary>Creates the interdisciplinary plan of care for an episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/plan")]
    [ProducesResponseType(typeof(CreatePlanResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatePlanResponse>> CreatePlan(
        string patientId, string episodeId, [FromBody] CreatePlanRequest request)
    {
        try
        {
            string planId = await GetWorkflow(patientId).CreateHomeCarePlanAsync(
                episodeId, request.EstablishedById, request.EstablishedByName);
            return Created($"/api/homecare/{patientId}/plans/{planId}",
                new CreatePlanResponse { PlanId = planId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan of care for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while creating the plan of care");
        }
    }

    /// <summary>Returns a single plan of care.</summary>
    [HttpGet("{patientId}/plans/{planId}")]
    [ProducesResponseType(typeof(HomeCarePlanState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeCarePlanState>> GetPlan(string patientId, string planId)
    {
        try
        {
            HomeCarePlanState plan = await GetWorkflow(patientId).GetHomeCarePlanAsync(planId);
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving plan {PlanId} for patient {PatientId}", planId, patientId);
            return StatusCode(500, "An error occurred while retrieving the plan of care");
        }
    }

    /// <summary>Adds a problem (with goals and interventions) to a plan of care.</summary>
    [HttpPost("{patientId}/plans/{planId}/problems")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddPlanProblem(
        string patientId, string planId, [FromBody] AddPlanProblemRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AddHomeCarePlanProblemAsync(
                planId, request.Problem, request.RelatedTo, request.Goals, request.Interventions, request.ResponsibleDiscipline);
            return Created($"/api/homecare/{patientId}/plans/{planId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding problem to plan {PlanId} for patient {PatientId}", planId, patientId);
            return StatusCode(500, "An error occurred while adding the plan problem");
        }
    }

    /// <summary>Resolves a problem on a plan of care.</summary>
    [HttpPost("{patientId}/plans/{planId}/problems/{problemId}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolvePlanProblem(string patientId, string planId, string problemId)
    {
        try
        {
            await GetWorkflow(patientId).ResolveHomeCarePlanProblemAsync(planId, problemId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving problem {ProblemId} on plan {PlanId} for patient {PatientId}",
                problemId, planId, patientId);
            return StatusCode(500, "An error occurred while resolving the plan problem");
        }
    }

    /// <summary>Records a review of a plan of care.</summary>
    [HttpPost("{patientId}/plans/{planId}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReviewPlan(
        string patientId, string planId, [FromBody] HomeCareReviewPlanRequest request)
    {
        try
        {
            await GetWorkflow(patientId).ReviewHomeCarePlanAsync(planId, request.ReviewDate, request.NextReviewDue);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing plan {PlanId} for patient {PatientId}", planId, patientId);
            return StatusCode(500, "An error occurred while reviewing the plan of care");
        }
    }

    // ─── Home Visits ─────────────────────────────────────────────────────────

    /// <summary>Schedules a home visit for an episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/visits")]
    [ProducesResponseType(typeof(ScheduleVisitResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ScheduleVisitResponse>> ScheduleVisit(
        string patientId, string episodeId, [FromBody] ScheduleVisitRequest request)
    {
        try
        {
            string visitId = await GetWorkflow(patientId).ScheduleHomeVisitAsync(
                episodeId, request.Discipline, request.VisitType, request.ScheduledDateTime,
                request.ClinicianId, request.ClinicianName, request.Reason);
            return Created($"/api/homecare/{patientId}/visits/{visitId}",
                new ScheduleVisitResponse { VisitId = visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling visit for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while scheduling the home visit");
        }
    }

    /// <summary>Returns all home visits for an episode.</summary>
    [HttpGet("{patientId}/episodes/{episodeId}/visits")]
    [ProducesResponseType(typeof(List<HomeVisitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeVisitIndexEntry>>> GetVisitsForEpisode(string patientId, string episodeId)
    {
        try
        {
            List<HomeVisitIndexEntry> visits = await GetWorkflow(patientId).GetHomeVisitsForEpisodeAsync(episodeId);
            return Ok(visits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visits for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while retrieving the home visits");
        }
    }

    /// <summary>Returns a single home visit.</summary>
    [HttpGet("{patientId}/visits/{visitId}")]
    [ProducesResponseType(typeof(HomeVisitState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeVisitState>> GetVisit(string patientId, string visitId)
    {
        try
        {
            HomeVisitState visit = await GetWorkflow(patientId).GetHomeVisitAsync(visitId);
            return Ok(visit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred while retrieving the home visit");
        }
    }

    /// <summary>Marks a scheduled home visit as started (clinician en route / arrived).</summary>
    [HttpPost("{patientId}/visits/{visitId}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StartVisit(string patientId, string visitId)
    {
        try
        {
            await GetWorkflow(patientId).StartHomeVisitAsync(visitId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred while starting the home visit");
        }
    }

    /// <summary>Completes a home visit, recording duration, vitals, interventions and summary.</summary>
    [HttpPost("{patientId}/visits/{visitId}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CompleteVisit(
        string patientId, string visitId, [FromBody] CompleteVisitRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CompleteHomeVisitAsync(
                visitId, request.DurationMinutes, request.VitalSigns, request.Interventions,
                request.Summary, request.NoteId, request.NextVisitDate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred while completing the home visit");
        }
    }

    /// <summary>Cancels (or marks missed / no-show) a home visit.</summary>
    [HttpPost("{patientId}/visits/{visitId}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelVisit(
        string patientId, string visitId, [FromBody] CancelVisitRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CancelHomeVisitAsync(visitId, request.Status, request.Reason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred while cancelling the home visit");
        }
    }

    // ─── Assessments ─────────────────────────────────────────────────────────

    /// <summary>Records an HBPC comprehensive assessment for an episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/assessments")]
    [ProducesResponseType(typeof(RecordAssessmentResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecordAssessmentResponse>> RecordAssessment(
        string patientId, string episodeId, [FromBody] RecordAssessmentRequest request)
    {
        try
        {
            string assessmentId = await GetWorkflow(patientId).RecordHomeCareAssessmentAsync(
                episodeId, request.AssessorId, request.AssessorName, request.AssessmentDate, request.Assessment);
            return Created($"/api/homecare/{patientId}/assessments/{assessmentId}",
                new RecordAssessmentResponse { AssessmentId = assessmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording assessment for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while recording the assessment");
        }
    }

    /// <summary>Returns all assessments for an episode.</summary>
    [HttpGet("{patientId}/episodes/{episodeId}/assessments")]
    [ProducesResponseType(typeof(List<HomeCareAssessmentState>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeCareAssessmentState>>> GetAssessmentsForEpisode(
        string patientId, string episodeId)
    {
        try
        {
            List<HomeCareAssessmentState> assessments =
                await GetWorkflow(patientId).GetHomeCareAssessmentsForEpisodeAsync(episodeId);
            return Ok(assessments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessments for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while retrieving the assessments");
        }
    }

    /// <summary>Returns a single assessment.</summary>
    [HttpGet("{patientId}/assessments/{assessmentId}")]
    [ProducesResponseType(typeof(HomeCareAssessmentState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeCareAssessmentState>> GetAssessment(string patientId, string assessmentId)
    {
        try
        {
            HomeCareAssessmentState assessment = await GetWorkflow(patientId).GetHomeCareAssessmentAsync(assessmentId);
            return Ok(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessment {AssessmentId} for patient {PatientId}",
                assessmentId, patientId);
            return StatusCode(500, "An error occurred while retrieving the assessment");
        }
    }

    // ─── Home Health — Medicare skilled (Phase 2 / HOME_HEALTH_MEDICARE) ─────
    // Layers Medicare skilled home-health onto the HBPC episode/visit grains:
    // eligibility gates → certification periods → OASIS → PDGM grouping → EVV →
    // NOA + claims billing. Consumed by the in-home mobile app. Writes require the
    // HBHC MANAGER security key (enforced grain-side); the billing read is open.

    /// <summary>Sets the Medicare eligibility gates (homebound + skilled need) on an episode.</summary>
    [HttpPut("{patientId}/episodes/{episodeId}/eligibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetEligibility(
        string patientId, string episodeId, [FromBody] HomeHealthEligibilityRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetHomeCareEligibilityAsync(
                episodeId, request.IsHomebound, request.HomeboundJustification, request.SkilledNeed);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Medicare eligibility for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while setting the home-health eligibility");
        }
    }

    /// <summary>Opens (or recertifies) a 60-day certification period for the episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/certify")]
    [ProducesResponseType(typeof(CertifyEpisodeResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CertifyEpisodeResponse>> CertifyEpisode(
        string patientId, string episodeId, [FromBody] CertifyEpisodeRequest request)
    {
        try
        {
            string certificationPeriodId = await GetWorkflow(patientId).CertifyHomeCareEpisodeAsync(
                episodeId, request.CertifyingProviderId, request.CertifyingProviderName,
                request.PeriodStart, request.FaceToFaceDate, request.IsRecertification);
            return Created($"/api/homecare/{patientId}/episodes/{episodeId}/certifications/{certificationPeriodId}",
                new CertifyEpisodeResponse { CertificationPeriodId = certificationPeriodId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error certifying episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while certifying the home-health episode");
        }
    }

    /// <summary>Records an OASIS assessment, scrubs it, and returns the assessment id + scrub issues.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/oasis")]
    [ProducesResponseType(typeof(OasisRecordResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OasisRecordResult>> RecordOasis(
        string patientId, string episodeId, [FromBody] RecordOasisRequest request)
    {
        try
        {
            OasisRecordResult result = await GetWorkflow(patientId).RecordOasisAsync(
                episodeId, request.AssessmentType, request.OasisVersion, request.Items,
                request.AssessorId, request.AssessorName, request.AssessmentDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording OASIS for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while recording the OASIS assessment");
        }
    }

    /// <summary>Computes and stores the PDGM case-mix grouping for a 30-day payment period.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/certifications/{certId}/payment-periods/{ppId}/grouping")]
    [ProducesResponseType(typeof(PdgmGroupingResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PdgmGroupingResult>> ComputePdgmGrouping(
        string patientId, string episodeId, string certId, string ppId)
    {
        try
        {
            PdgmGroupingResult result = await GetWorkflow(patientId).ComputePdgmGroupingAsync(episodeId, certId, ppId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing PDGM grouping for payment period {PpId} (cert {CertId}, episode {EpisodeId}) for patient {PatientId}",
                ppId, certId, episodeId, patientId);
            return StatusCode(500, "An error occurred while computing the PDGM grouping");
        }
    }

    /// <summary>EVV check-in for a home visit (time / location / capture method).</summary>
    [HttpPost("{patientId}/visits/{visitId}/check-in")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CheckInVisit(
        string patientId, string visitId, [FromBody] CheckInVisitRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CheckInHomeVisitAsync(visitId, request.Location, request.Method);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking in visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred while checking in the home visit");
        }
    }

    /// <summary>EVV check-out for a home visit.</summary>
    [HttpPost("{patientId}/visits/{visitId}/check-out")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CheckOutVisit(
        string patientId, string visitId, [FromBody] CheckOutVisitRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CheckOutHomeVisitAsync(visitId, request.Location);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out visit {VisitId} for patient {PatientId}", visitId, patientId);
            return StatusCode(500, "An error occurred while checking out the home visit");
        }
    }

    /// <summary>Submits the Medicare Notice of Admission (NOA) for the episode.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/billing/noa")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SubmitNoticeOfAdmission(
        string patientId, string episodeId, [FromBody] SubmitNoaRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SubmitHomeHealthNoticeOfAdmissionAsync(episodeId, request.SubmittedDate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting Notice of Admission for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while submitting the Notice of Admission");
        }
    }

    /// <summary>Generates a Medicare claim for a payment period from its PDGM grouping. Returns the claim id.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/billing/claims")]
    [ProducesResponseType(typeof(GenerateClaimResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<GenerateClaimResponse>> GenerateClaim(
        string patientId, string episodeId, [FromBody] GenerateClaimRequest request)
    {
        try
        {
            string claimId = await GetWorkflow(patientId).GenerateHomeHealthClaimAsync(
                episodeId, request.CertificationPeriodId, request.PaymentPeriodId);
            return Created($"/api/homecare/{patientId}/episodes/{episodeId}/billing/claims/{claimId}",
                new GenerateClaimResponse { ClaimId = claimId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Medicare claim for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while generating the home-health claim");
        }
    }

    /// <summary>Submits a generated Medicare claim.</summary>
    [HttpPost("{patientId}/episodes/{episodeId}/billing/claims/{claimId}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SubmitClaim(
        string patientId, string episodeId, string claimId, [FromBody] SubmitClaimRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SubmitHomeHealthClaimAsync(episodeId, claimId, request.SubmittedDate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting Medicare claim {ClaimId} for episode {EpisodeId} for patient {PatientId}",
                claimId, episodeId, patientId);
            return StatusCode(500, "An error occurred while submitting the home-health claim");
        }
    }

    /// <summary>Returns the episode's Medicare billing record (NOA + claims).</summary>
    [HttpGet("{patientId}/episodes/{episodeId}/billing")]
    [ProducesResponseType(typeof(HomeHealthBillingState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeHealthBillingState>> GetBilling(string patientId, string episodeId)
    {
        try
        {
            HomeHealthBillingState billing = await GetWorkflow(patientId).GetHomeHealthBillingAsync(episodeId);
            return Ok(billing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Medicare billing for episode {EpisodeId} for patient {PatientId}",
                episodeId, patientId);
            return StatusCode(500, "An error occurred while retrieving the home-health billing");
        }
    }

    // ─── Facility-wide Census / Caseload (singleton census grain) ────────────

    /// <summary>
    /// Returns the home-care caseload census. Returns the Active caseload by default;
    /// pass <c>?all=true</c> for every episode (any status).
    /// </summary>
    [HttpGet("census")]
    [ProducesResponseType(typeof(List<HomeCareCensusEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeCareCensusEntry>>> GetCensus([FromQuery] bool all = false)
    {
        try
        {
            List<HomeCareCensusEntry> census = all
                ? await GetCensus().GetAllAsync()
                : await GetCensus().GetActiveAsync();
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-care census (all={All})", all);
            return StatusCode(500, "An error occurred while retrieving the home-care census");
        }
    }

    /// <summary>Returns workload roll-up statistics for the home-care caseload.</summary>
    [HttpGet("census/workload")]
    [ProducesResponseType(typeof(HomeCareWorkloadStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeCareWorkloadStats>> GetWorkloadStats()
    {
        try
        {
            HomeCareWorkloadStats stats = await GetCensus().GetWorkloadStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-care workload stats");
            return StatusCode(500, "An error occurred while retrieving the workload statistics");
        }
    }

    /// <summary>Returns the caseload for a specific home-care provider.</summary>
    [HttpGet("census/provider/{providerId}")]
    [ProducesResponseType(typeof(List<HomeCareCensusEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeCareCensusEntry>>> GetCensusByProvider(string providerId)
    {
        try
        {
            List<HomeCareCensusEntry> census = await GetCensus().GetByProviderAsync(providerId);
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-care census for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving the provider caseload");
        }
    }

    /// <summary>Returns caseload entries with no completed visit in the given number of days (outreach worklist).</summary>
    [HttpGet("census/no-recent-visit/{days}")]
    [ProducesResponseType(typeof(List<HomeCareCensusEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeCareCensusEntry>>> GetCensusWithNoRecentVisit(int days)
    {
        try
        {
            List<HomeCareCensusEntry> census = await GetCensus().GetWithNoRecentVisitAsync(days);
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home-care census with no visit in {Days} days", days);
            return StatusCode(500, "An error occurred while retrieving the no-recent-visit worklist");
        }
    }

    // ─── Facility-wide Visit Schedule (singleton visit-index grain) ──────────

    /// <summary>Returns upcoming home visits within the given number of days (the mobile daily schedule).</summary>
    [HttpGet("visits/upcoming/{days}")]
    [ProducesResponseType(typeof(List<HomeVisitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeVisitIndexEntry>>> GetUpcomingVisits(int days)
    {
        try
        {
            List<HomeVisitIndexEntry> visits = await GetVisitIndex().GetUpcomingVisitsAsync(days);
            return Ok(visits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming home visits within {Days} days", days);
            return StatusCode(500, "An error occurred while retrieving upcoming visits");
        }
    }

    /// <summary>Returns the home-visit schedule for a specific clinician (their daily caseload).</summary>
    [HttpGet("visits/clinician/{clinicianId}")]
    [ProducesResponseType(typeof(List<HomeVisitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HomeVisitIndexEntry>>> GetVisitsByClinician(string clinicianId)
    {
        try
        {
            List<HomeVisitIndexEntry> visits = await GetVisitIndex().GetVisitsByClinicianAsync(clinicianId);
            return Ok(visits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving home visits for clinician {ClinicianId}", clinicianId);
            return StatusCode(500, "An error occurred while retrieving the clinician's visits");
        }
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────────────

public record AdmitHomeCareRequest
{
    public HomeCareProgramType ProgramType { get; init; }
    public DateTime AdmissionDate { get; init; }
    public HomeCareAdmissionSource AdmissionSource { get; init; }
    public string ReferringProviderId { get; init; } = string.Empty;
    public string ReferringProviderName { get; init; } = string.Empty;
    public string PrimaryDiagnosisCode { get; init; } = string.Empty;
    public string PrimaryDiagnosisText { get; init; } = string.Empty;
    public HomeCareLevelOfCare LevelOfCare { get; init; }
    public string ClinicalNeedNarrative { get; init; } = string.Empty;
    public string PrimaryCaregiver { get; init; } = string.Empty;
    public string HomeAddress { get; init; } = string.Empty;
    public HomeCareDeliveryModel DeliveryModel { get; init; } = HomeCareDeliveryModel.HospitalProvided;
}

public record AdmitHomeCareResponse
{
    public string EpisodeId { get; init; } = string.Empty;
}

public record SetDeliveryModelRequest
{
    public HomeCareDeliveryModel DeliveryModel { get; init; }
}

public record LinkAgencyRequest
{
    public string AgencyId { get; init; } = string.Empty;
    public string CoordinatorProviderId { get; init; } = string.Empty;
    public string CoordinatorName { get; init; } = string.Empty;
    public string? ExternalReferralId { get; init; }
}

public record AgencyMilestoneRequest
{
    public AgencyMilestoneType Type { get; init; }
    public DateTime Date { get; init; }
    public string Note { get; init; } = string.Empty;
    public string RecordedById { get; init; } = string.Empty;
    public string RecordedByName { get; init; } = string.Empty;
}

public record HospitalAtHomeRequest
{
    public string SourceAdmissionId { get; init; } = string.Empty;
    public string SourceFacilityId { get; init; } = string.Empty;
    public string SourceFacilityName { get; init; } = string.Empty;
    public string? SourceUnitId { get; init; }
    public string? SourceBedId { get; init; }
    public DateTime? SubstitutionStartDate { get; init; }
    public string ClinicalRationale { get; init; } = string.Empty;
}

public record AssignTeamMemberRequest
{
    public string ProviderId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public HomeCareDiscipline Discipline { get; init; }
    public string RoleTitle { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
}

public record UpdateLevelOfCareRequest
{
    public HomeCareLevelOfCare LevelOfCare { get; init; }
}

public record AddSecondaryDiagnosisRequest
{
    public string Diagnosis { get; init; } = string.Empty;
}

public record HoldEpisodeRequest
{
    public string Reason { get; init; } = string.Empty;
}

public record HomeCareDischargeRequest
{
    public DateTime DischargeDate { get; init; }
    public HomeCareDischargeReason Reason { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public record CreatePlanRequest
{
    public string EstablishedById { get; init; } = string.Empty;
    public string EstablishedByName { get; init; } = string.Empty;
}

public record CreatePlanResponse
{
    public string PlanId { get; init; } = string.Empty;
}

public record AddPlanProblemRequest
{
    public string Problem { get; init; } = string.Empty;
    public string RelatedTo { get; init; } = string.Empty;
    public List<string> Goals { get; init; } = new();
    public List<string> Interventions { get; init; } = new();
    public HomeCareDiscipline ResponsibleDiscipline { get; init; }
}

public record HomeCareReviewPlanRequest
{
    public DateTime ReviewDate { get; init; }
    public DateTime? NextReviewDue { get; init; }
}

public record ScheduleVisitRequest
{
    public HomeCareDiscipline Discipline { get; init; }
    public HomeVisitType VisitType { get; init; }
    public DateTime ScheduledDateTime { get; init; }
    public string ClinicianId { get; init; } = string.Empty;
    public string ClinicianName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record ScheduleVisitResponse
{
    public string VisitId { get; init; } = string.Empty;
}

public record CompleteVisitRequest
{
    public int DurationMinutes { get; init; }
    public string VitalSigns { get; init; } = string.Empty;
    public List<string> Interventions { get; init; } = new();
    public string Summary { get; init; } = string.Empty;
    public string NoteId { get; init; } = string.Empty;
    public DateTime? NextVisitDate { get; init; }
}

public record CancelVisitRequest
{
    public HomeVisitStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record RecordAssessmentRequest
{
    public string AssessorId { get; init; } = string.Empty;
    public string AssessorName { get; init; } = string.Empty;
    public DateTime AssessmentDate { get; init; }
    public HbpcComprehensiveAssessment Assessment { get; init; } = new();
}

public record RecordAssessmentResponse
{
    public string AssessmentId { get; init; } = string.Empty;
}

// ─── Home Health — Medicare skilled (Phase 2) DTOs ───────────────────────────

public record HomeHealthEligibilityRequest
{
    public bool IsHomebound { get; init; }
    public string HomeboundJustification { get; init; } = string.Empty;
    public SkilledNeedType SkilledNeed { get; init; }
}

public record CertifyEpisodeRequest
{
    public string CertifyingProviderId { get; init; } = string.Empty;
    public string CertifyingProviderName { get; init; } = string.Empty;
    public DateTime PeriodStart { get; init; }
    public DateTime? FaceToFaceDate { get; init; }
    public bool IsRecertification { get; init; }
}

public record CertifyEpisodeResponse
{
    public string CertificationPeriodId { get; init; } = string.Empty;
}

public record RecordOasisRequest
{
    public HomeCareAssessmentType AssessmentType { get; init; }
    public string OasisVersion { get; init; } = string.Empty;
    /// <summary>OASIS item code → value (e.g. "M1830" → "03").</summary>
    public Dictionary<string, string> Items { get; init; } = new();
    public string AssessorId { get; init; } = string.Empty;
    public string AssessorName { get; init; } = string.Empty;
    public DateTime AssessmentDate { get; init; }
}

public record CheckInVisitRequest
{
    public string Location { get; init; } = string.Empty;
    public EvvMethod Method { get; init; }
}

public record CheckOutVisitRequest
{
    public string Location { get; init; } = string.Empty;
}

public record SubmitNoaRequest
{
    public DateTime SubmittedDate { get; init; }
}

public record GenerateClaimRequest
{
    public string CertificationPeriodId { get; init; } = string.Empty;
    public string PaymentPeriodId { get; init; } = string.Empty;
}

public record GenerateClaimResponse
{
    public string ClaimId { get; init; } = string.Empty;
}

public record SubmitClaimRequest
{
    public DateTime SubmittedDate { get; init; }
}
