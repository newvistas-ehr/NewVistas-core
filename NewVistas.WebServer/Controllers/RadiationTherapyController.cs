// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RadiationTherapyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<RadiationTherapyController> _logger;

    public RadiationTherapyController(IGrainFactory grainFactory, ILogger<RadiationTherapyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(Uri.UnescapeDataString(patientId));

    // ── Course Queries ────────────────────────────────────────────────────────

    [HttpGet("{patientId}/courses")]
    public async Task<IActionResult> GetAllCourses(string patientId)
    {
        try
        {
            List<RtCourseIndexEntry> result = await GetWorkflow(patientId).GetRtCoursesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving RT courses for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving radiation therapy courses.");
        }
    }

    [HttpGet("{patientId}/courses/active")]
    public async Task<IActionResult> GetActiveCourses(string patientId)
    {
        try
        {
            List<RtCourseIndexEntry> result = await GetWorkflow(patientId).GetActiveRtCoursesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active RT courses for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving active radiation therapy courses.");
        }
    }

    [HttpGet("{patientId}/courses/{courseId}")]
    public async Task<IActionResult> GetCourse(string patientId, string courseId)
    {
        try
        {
            RtCourseState result = await GetWorkflow(patientId)
                .GetRtCourseAsync(Uri.UnescapeDataString(courseId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving RT course {CourseId} for {PatientId}", courseId, patientId);
            return StatusCode(500, "An error occurred retrieving the radiation therapy course.");
        }
    }

    // ── Course Lifecycle ──────────────────────────────────────────────────────

    [HttpPost("{patientId}/courses")]
    public async Task<IActionResult> CreateCourse(string patientId, [FromBody] CreateRtCourseRequest req)
    {
        try
        {
            string courseId = await GetWorkflow(patientId).CreateRtCourseAsync(
                req.CourseName, req.DiagnosisCode, req.DiagnosisText,
                req.TreatmentSite, req.Laterality, req.Intent, req.Modality,
                req.PrescribedDoseCgy, req.FractionsPlanned, req.DosePerFractionCgy,
                req.BeamEnergy,
                req.OncologistId, req.OncologistName,
                req.PhysicistId, req.PhysicistName,
                req.DosimetristId, req.DosimetristName,
                req.TreatmentMachineId, req.TreatmentMachineName,
                req.PlanningNotes);
            return Created($"/api/radiationtherapy/{Uri.EscapeDataString(patientId)}/courses/{Uri.EscapeDataString(courseId)}", new { courseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RT course for {PatientId}", patientId);
            return StatusCode(500, "An error occurred creating the radiation therapy course.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/simulate")]
    public async Task<IActionResult> RecordSimulation(string patientId, string courseId, [FromBody] RtSimulationRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordRtSimulationAsync(
                Uri.UnescapeDataString(courseId), req.SimulationDate, req.PlanningNotes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording simulation for RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred recording the simulation.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/start")]
    public async Task<IActionResult> StartCourse(string patientId, string courseId, [FromBody] RtStartRequest req)
    {
        try
        {
            await GetWorkflow(patientId).StartRtCourseAsync(
                Uri.UnescapeDataString(courseId), req.TreatmentStartDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred starting the radiation therapy course.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/complete")]
    public async Task<IActionResult> CompleteCourse(string patientId, string courseId, [FromBody] RtCompleteRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CompleteRtCourseAsync(
                Uri.UnescapeDataString(courseId), req.CompletionDate, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred completing the radiation therapy course.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/discontinue")]
    public async Task<IActionResult> DiscontinueCourse(string patientId, string courseId, [FromBody] RtDiscontinueRequest req)
    {
        try
        {
            await GetWorkflow(patientId).DiscontinueRtCourseAsync(
                Uri.UnescapeDataString(courseId), req.DiscontinuationDate, req.Reason, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred discontinuing the radiation therapy course.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/hold")]
    public async Task<IActionResult> PlaceOnHold(string patientId, string courseId, [FromBody] RtHoldRequest req)
    {
        try
        {
            await GetWorkflow(patientId).PlaceRtCourseOnHoldAsync(
                Uri.UnescapeDataString(courseId), req.Reason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing RT course {CourseId} on hold", courseId);
            return StatusCode(500, "An error occurred placing the course on hold.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/resume")]
    public async Task<IActionResult> ResumeCourse(string patientId, string courseId)
    {
        try
        {
            await GetWorkflow(patientId).ResumeRtCourseAsync(Uri.UnescapeDataString(courseId));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred resuming the radiation therapy course.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/boost")]
    public async Task<IActionResult> SetBoost(string patientId, string courseId, [FromBody] RtBoostRequest req)
    {
        try
        {
            await GetWorkflow(patientId).SetRtBoostAsync(
                Uri.UnescapeDataString(courseId), req.BoostSite, req.BoostDoseCgy, req.BoostFractionsPlanned);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting boost for RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred setting boost details.");
        }
    }

    // ── Fractions ─────────────────────────────────────────────────────────────

    [HttpGet("{patientId}/courses/{courseId}/fractions")]
    public async Task<IActionResult> GetFractions(string patientId, string courseId)
    {
        try
        {
            List<RtTreatmentIndexEntry> result = await GetWorkflow(patientId)
                .GetRtCourseTreatmentsAsync(Uri.UnescapeDataString(courseId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fractions for RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred retrieving treatment fractions.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/fractions")]
    public async Task<IActionResult> RecordFraction(string patientId, string courseId, [FromBody] RecordRtFractionRequest req)
    {
        try
        {
            string treatmentId = await GetWorkflow(patientId).RecordRtFractionAsync(
                Uri.UnescapeDataString(courseId),
                req.FractionNumber, req.TreatmentDate, req.DoseDeliveredCgy,
                req.TreatmentDurationMin, req.MachineId, req.MachineName,
                req.TechnicianId, req.TechnicianName,
                req.SetupVerified, req.SetupMethod, req.SetupDeviationMm,
                req.Interrupted, req.InterruptionReason, req.Notes);
            return Created(string.Empty, new { treatmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording fraction for RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred recording the treatment fraction.");
        }
    }

    [HttpPost("{patientId}/courses/{courseId}/fractions/skip")]
    public async Task<IActionResult> RecordSkippedFraction(string patientId, string courseId, [FromBody] RecordRtSkipRequest req)
    {
        try
        {
            string treatmentId = await GetWorkflow(patientId).RecordRtSkippedFractionAsync(
                Uri.UnescapeDataString(courseId),
                req.FractionNumber, req.ScheduledDate, req.Status, req.SkipReason);
            return Created(string.Empty, new { treatmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording skipped fraction for RT course {CourseId}", courseId);
            return StatusCode(500, "An error occurred recording the skipped fraction.");
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateRtCourseRequest(
    string CourseName,
    string DiagnosisCode,
    string DiagnosisText,
    string TreatmentSite,
    RtLaterality Laterality,
    RtIntent Intent,
    RtModality Modality,
    int PrescribedDoseCgy,
    int FractionsPlanned,
    int DosePerFractionCgy,
    string? BeamEnergy,
    string? OncologistId,
    string? OncologistName,
    string? PhysicistId,
    string? PhysicistName,
    string? DosimetristId,
    string? DosimetristName,
    string? TreatmentMachineId,
    string? TreatmentMachineName,
    string? PlanningNotes);

public record RtSimulationRequest(DateTime SimulationDate, string? PlanningNotes);
public record RtStartRequest(DateTime TreatmentStartDate);
public record RtCompleteRequest(DateTime CompletionDate, string? Notes);
public record RtDiscontinueRequest(DateTime DiscontinuationDate, string Reason, string? Notes);
public record RtHoldRequest(string? Reason);

public record RtBoostRequest(
    string BoostSite,
    int BoostDoseCgy,
    int BoostFractionsPlanned);

public record RecordRtFractionRequest(
    int FractionNumber,
    DateTime TreatmentDate,
    int DoseDeliveredCgy,
    int? TreatmentDurationMin,
    string? MachineId,
    string? MachineName,
    string? TechnicianId,
    string? TechnicianName,
    bool SetupVerified,
    string? SetupMethod,
    decimal? SetupDeviationMm,
    bool Interrupted,
    string? InterruptionReason,
    string? Notes);

public record RecordRtSkipRequest(
    int FractionNumber,
    DateTime ScheduledDate,
    RtFractionStatus Status,
    string? SkipReason);
