// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Blind Rehabilitation API — visual impairment assessment, BR admissions, and outpatient training.
/// VistA File #782 (Blind Rehabilitation) — ANRV.m, ANRUTIL.m, ANRVAD.m, ANRVOP.m
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class BlindRehabilitationController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BlindRehabilitationController> _logger;

    public BlindRehabilitationController(IGrainFactory grainFactory, ILogger<BlindRehabilitationController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Patient BR Record ────────────────────────────────────────────────────

    [HttpGet("api/patient/{patientId}/blind-rehab")]
    [ProducesResponseType(typeof(BRPatientState), StatusCodes.Status200OK)]
    public async Task<ActionResult<BRPatientState>> GetBRPatient(string patientId)
    {
        try
        {
            BRPatientState record = await GetWorkflow(patientId).GetBRPatientAsync();
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blind rehab record for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the blind rehabilitation record");
        }
    }

    [HttpPut("api/patient/{patientId}/blind-rehab/visual-acuity")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordVisualAcuity(string patientId, [FromBody] RecordVisualAcuityRequest request)
    {
        try
        {
            VisualField vfRight = Enum.TryParse<VisualField>(request.VisualFieldRight, out VisualField vfr) ? vfr : VisualField.NotTested;
            VisualField vfLeft  = Enum.TryParse<VisualField>(request.VisualFieldLeft,  out VisualField vfl) ? vfl : VisualField.NotTested;

            await GetWorkflow(patientId).RecordVisualAcuityAsync(
                request.RightEyeDistance, request.LeftEyeDistance,
                request.BestCorrectedRight, request.BestCorrectedLeft,
                vfRight, vfLeft,
                request.ContrastSensitivity,
                request.ExamDate, request.ExaminerId, request.ExaminerName,
                request.Notes);

            _logger.LogInformation("Recorded visual acuity for patient {PatientId}", patientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording visual acuity for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording visual acuity");
        }
    }

    [HttpPut("api/patient/{patientId}/blind-rehab/diagnosis")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateDiagnosis(string patientId, [FromBody] UpdateBRDiagnosisRequest request)
    {
        try
        {
            BROnsetType onsetType = Enum.TryParse<BROnsetType>(request.OnsetType, out BROnsetType ot) ? ot : BROnsetType.Unknown;

            await GetWorkflow(patientId).UpdateBRDiagnosisAsync(
                request.PrimaryDiagnosis, request.SecondaryDiagnosis,
                onsetType, request.OnsetDate,
                request.ServiceConnected, request.ServiceConnectedPercentage,
                request.Icd10Code, request.Notes);

            _logger.LogInformation("Updated BR diagnosis for patient {PatientId}", patientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating BR diagnosis for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating the diagnosis");
        }
    }

    [HttpPost("api/patient/{patientId}/blind-rehab/devices")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddDevice(string patientId, [FromBody] AddBRDeviceRequest request)
    {
        try
        {
            BRDeviceEntry device = new()
            {
                DeviceName = request.DeviceName,
                Category = request.Category,
                SerialNumber = request.SerialNumber,
                IssuedDate = request.IssuedDate,
                IssuedBy = request.IssuedBy,
                Notes = request.Notes
            };
            await GetWorkflow(patientId).AddBRDeviceAsync(device);
            _logger.LogInformation("Added BR device {Device} for patient {PatientId}", request.DeviceName, patientId);
            return Created();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding BR device for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding the device");
        }
    }

    [HttpPost("api/patient/{patientId}/blind-rehab/goals")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddTrainingGoal(string patientId, [FromBody] AddBRGoalRequest request)
    {
        try
        {
            BRTrainingArea area = Enum.TryParse<BRTrainingArea>(request.Area, out BRTrainingArea a) ? a : BRTrainingArea.ActivitiesOfDailyLiving;
            await GetWorkflow(patientId).AddBRTrainingGoalAsync(request.Goal, area);
            _logger.LogInformation("Added BR training goal for patient {PatientId}", patientId);
            return Created();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding BR training goal for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding the training goal");
        }
    }

    [HttpPut("api/patient/{patientId}/blind-rehab/eligibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateEligibility(string patientId, [FromBody] UpdateBREligibilityRequest request)
    {
        try
        {
            BREligibilityStatus eligibility = Enum.TryParse<BREligibilityStatus>(request.Eligibility, out BREligibilityStatus e) ? e : BREligibilityStatus.Unknown;
            await GetWorkflow(patientId).UpdateBREligibilityAsync(eligibility, request.Reason);
            _logger.LogInformation("Updated BR eligibility for patient {PatientId} to {Eligibility}", patientId, request.Eligibility);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating BR eligibility for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating eligibility");
        }
    }

    // ─── Admissions ───────────────────────────────────────────────────────────

    [HttpGet("api/patient/{patientId}/blind-rehab/admissions")]
    [ProducesResponseType(typeof(List<BRAdmissionIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BRAdmissionIndexEntry>>> GetAdmissions(string patientId)
    {
        try
        {
            List<BRAdmissionIndexEntry> admissions = await GetWorkflow(patientId).GetBRAdmissionsAsync();
            return Ok(admissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving BR admissions for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving admissions");
        }
    }

    [HttpGet("api/blind-rehab/admissions/{admitId}")]
    [ProducesResponseType(typeof(BRAdmissionState), StatusCodes.Status200OK)]
    public async Task<ActionResult<BRAdmissionState>> GetAdmission(string admitId)
    {
        try
        {
            IBRAdmissionGrain grain = _grainFactory.GetGrain<IBRAdmissionGrain>(admitId);
            BRAdmissionState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving BR admission {AdmitId}", admitId);
            return StatusCode(500, "An error occurred while retrieving the admission");
        }
    }

    [HttpPost("api/patient/{patientId}/blind-rehab/admissions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAdmission(string patientId, [FromBody] CreateBRAdmissionRequest request)
    {
        try
        {
            BRAdmissionPriority priority = Enum.TryParse<BRAdmissionPriority>(request.Priority, out BRAdmissionPriority p) ? p : BRAdmissionPriority.Routine;
            List<BRTrainingArea> areas = request.ProgramAreas
                .Select(a => Enum.TryParse<BRTrainingArea>(a, out BRTrainingArea ta) ? ta : BRTrainingArea.ActivitiesOfDailyLiving)
                .ToList();

            string admitId = await GetWorkflow(patientId).CreateBRAdmissionAsync(
                request.CenterId, request.CenterName, request.AdmitDate,
                request.PlannedDischargeDate, areas, priority,
                request.ReferringProviderId, request.ReferringProviderName,
                request.Goals, request.Notes);

            _logger.LogInformation("Created BR admission {AdmitId} for patient {PatientId}", admitId, patientId);
            return Created($"api/blind-rehab/admissions/{admitId}", new { admitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating BR admission for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the admission");
        }
    }

    [HttpPost("api/blind-rehab/admissions/{admitId}/progress-notes")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAdmissionProgressNote(string admitId, [FromBody] AddBRProgressNoteRequest request)
    {
        try
        {
            IBRAdmissionGrain grain = _grainFactory.GetGrain<IBRAdmissionGrain>(admitId);
            await grain.AddProgressNoteAsync(request.Note, request.AuthorId, request.AuthorName);
            return Created();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding progress note to BR admission {AdmitId}", admitId);
            return StatusCode(500, "An error occurred while adding the progress note");
        }
    }

    [HttpPost("api/blind-rehab/admissions/{admitId}/discharge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DischargeAdmission(string admitId, [FromBody] DischargeBRAdmissionRequest request)
    {
        try
        {
            BRDischargeDisposition disposition = Enum.TryParse<BRDischargeDisposition>(request.Disposition, out BRDischargeDisposition d)
                ? d : BRDischargeDisposition.CompletedProgram;
            List<BRTrainingArea> areasCompleted = request.AreasCompleted
                .Select(a => Enum.TryParse<BRTrainingArea>(a, out BRTrainingArea ta) ? ta : BRTrainingArea.ActivitiesOfDailyLiving)
                .ToList();

            IBRAdmissionGrain grain = _grainFactory.GetGrain<IBRAdmissionGrain>(admitId);
            await grain.DischargeAsync(request.DischargeDate, disposition, request.DischargeSummary, areasCompleted, request.FollowUpPlan);
            _logger.LogInformation("Discharged BR admission {AdmitId}", admitId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging BR admission {AdmitId}", admitId);
            return StatusCode(500, "An error occurred while recording the discharge");
        }
    }

    // ─── Outpatient Visits ────────────────────────────────────────────────────

    [HttpGet("api/patient/{patientId}/blind-rehab/visits")]
    [ProducesResponseType(typeof(List<BROutpatientVisitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BROutpatientVisitIndexEntry>>> GetVisits(string patientId)
    {
        try
        {
            List<BROutpatientVisitIndexEntry> visits = await GetWorkflow(patientId).GetBROutpatientVisitsAsync();
            return Ok(visits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving BR visits for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving visits");
        }
    }

    [HttpGet("api/blind-rehab/visits/{visitId}")]
    [ProducesResponseType(typeof(BROutpatientVisitState), StatusCodes.Status200OK)]
    public async Task<ActionResult<BROutpatientVisitState>> GetVisit(string visitId)
    {
        try
        {
            IBROutpatientVisitGrain grain = _grainFactory.GetGrain<IBROutpatientVisitGrain>(visitId);
            BROutpatientVisitState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving BR visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred while retrieving the visit");
        }
    }

    [HttpPost("api/patient/{patientId}/blind-rehab/visits")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> ScheduleVisit(string patientId, [FromBody] ScheduleBRVisitRequest request)
    {
        try
        {
            BRTrainingArea area = Enum.TryParse<BRTrainingArea>(request.TrainingArea, out BRTrainingArea ta) ? ta : BRTrainingArea.ActivitiesOfDailyLiving;

            string visitId = await GetWorkflow(patientId).ScheduleBROutpatientVisitAsync(
                request.VisitDate, area,
                request.TherapistId, request.TherapistName,
                request.Location, request.DurationMinutes,
                request.SessionNotes, request.SkillsAddressed ?? new List<string>());

            _logger.LogInformation("Scheduled BR visit {VisitId} for patient {PatientId}", visitId, patientId);
            return Created($"api/blind-rehab/visits/{visitId}", new { visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling BR visit for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while scheduling the visit");
        }
    }

    [HttpPost("api/blind-rehab/visits/{visitId}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CompleteVisit(string visitId, [FromBody] CompleteBRVisitRequest request)
    {
        try
        {
            BRVisitOutcome outcome = Enum.TryParse<BRVisitOutcome>(request.Outcome, out BRVisitOutcome o) ? o : BRVisitOutcome.ProgressMade;
            IBROutpatientVisitGrain grain = _grainFactory.GetGrain<IBROutpatientVisitGrain>(visitId);
            await grain.CompleteAsync(request.OutcomeSummary, outcome);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing BR visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred while completing the visit");
        }
    }

    // ─── Training Centers ─────────────────────────────────────────────────────

    [HttpGet("api/blind-rehab/centers")]
    [ProducesResponseType(typeof(List<BRCenterIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BRCenterIndexEntry>>> GetCenters([FromQuery] bool acceptingOnly = false)
    {
        try
        {
            IBRCenterIndexGrain indexGrain = _grainFactory.GetGrain<IBRCenterIndexGrain>("BR-CENTER-IDX");
            await indexGrain.SeedDefaultsAsync();
            List<BRCenterIndexEntry> centers = acceptingOnly
                ? await indexGrain.GetAcceptingAsync()
                : await indexGrain.GetAllAsync();
            return Ok(centers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving BR centers");
            return StatusCode(500, "An error occurred while retrieving training centers");
        }
    }

    [HttpGet("api/blind-rehab/centers/{centerId}")]
    [ProducesResponseType(typeof(BRCenterState), StatusCodes.Status200OK)]
    public async Task<ActionResult<BRCenterState>> GetCenter(string centerId)
    {
        try
        {
            IBRCenterGrain grain = _grainFactory.GetGrain<IBRCenterGrain>(centerId);
            BRCenterState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving BR center {CenterId}", centerId);
            return StatusCode(500, "An error occurred while retrieving the training center");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record RecordVisualAcuityRequest(
    string RightEyeDistance,
    string LeftEyeDistance,
    string BestCorrectedRight,
    string BestCorrectedLeft,
    string VisualFieldRight,
    string VisualFieldLeft,
    string? ContrastSensitivity,
    DateTime ExamDate,
    string ExaminerId,
    string ExaminerName,
    string? Notes);

public record UpdateBRDiagnosisRequest(
    string PrimaryDiagnosis,
    string? SecondaryDiagnosis,
    string OnsetType,
    DateTime? OnsetDate,
    bool ServiceConnected,
    int? ServiceConnectedPercentage,
    string? Icd10Code,
    string? Notes);

public record AddBRDeviceRequest(
    string DeviceName,
    string Category,
    string? SerialNumber,
    DateTime IssuedDate,
    string IssuedBy,
    string? Notes);

public record AddBRGoalRequest(string Goal, string Area);

public record UpdateBREligibilityRequest(string Eligibility, string? Reason);

public record CreateBRAdmissionRequest(
    string CenterId,
    string CenterName,
    DateTime AdmitDate,
    DateTime? PlannedDischargeDate,
    List<string> ProgramAreas,
    string Priority,
    string ReferringProviderId,
    string ReferringProviderName,
    string? Goals,
    string? Notes);

public record AddBRProgressNoteRequest(string Note, string AuthorId, string AuthorName);

public record DischargeBRAdmissionRequest(
    DateTime DischargeDate,
    string Disposition,
    string DischargeSummary,
    List<string> AreasCompleted,
    string? FollowUpPlan);

public record ScheduleBRVisitRequest(
    DateTime VisitDate,
    string TrainingArea,
    string TherapistId,
    string TherapistName,
    string Location,
    int DurationMinutes,
    string? SessionNotes,
    List<string>? SkillsAddressed);

public record CompleteBRVisitRequest(string OutcomeSummary, string Outcome);
