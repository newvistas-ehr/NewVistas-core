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
/// REST API for Geriatrics and Extended Care: GEC/MDS assessments and
/// Community Living Center admissions.
/// VistA GEC File #25.1. MDS.m, GECCLC.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GECController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<GECController> _logger;

    public GECController(IGrainFactory grainFactory, ILogger<GECController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IGECAssessmentGrain Assessment(string id)
        => _grainFactory.GetGrain<IGECAssessmentGrain>(Uri.UnescapeDataString(id));

    private IGECAssessmentIndexGrain AssessmentIndex(string patientId)
        => _grainFactory.GetGrain<IGECAssessmentIndexGrain>($"GEC-ASSESS-IDX:{patientId}");

    private ICLCAdmissionGrain Admission(string id)
        => _grainFactory.GetGrain<ICLCAdmissionGrain>(Uri.UnescapeDataString(id));

    private ICLCAdmissionIndexGrain AdmissionIndex()
        => _grainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX");

    private static GECAssessmentIndexEntry BuildAssessmentIndex(GECAssessmentState s) => new()
    {
        AssessmentId = s.AssessmentId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        AssessmentType = s.AssessmentType,
        AssessmentDate = s.AssessmentDate,
        Status = s.Status,
        RUGCategory = s.RUGCategory,
        ADLTotalScore = s.ADLTotalScore,
        LevelOfCare = s.LevelOfCare
    };

    private static CLCAdmissionIndexEntry BuildAdmissionIndex(CLCAdmissionState s) => new()
    {
        AdmissionId = s.AdmissionId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        AdmitDate = s.AdmitDate,
        LevelOfCare = s.LevelOfCare,
        Status = s.Status,
        Ward = s.Ward,
        BedRoom = s.BedRoom,
        AttendingPhysician = s.AttendingPhysician,
        AnticipatedDischargeDate = s.AnticipatedDischargeDate
    };

    // ── GEC Assessments ───────────────────────────────────────────────────────

    [HttpPost("assessments")]
    public async Task<IActionResult> CreateAssessment([FromBody] CreateGECAssessmentDto dto)
    {
        try
        {
            string assessmentId = $"GEC-ASSESS:{Guid.NewGuid()}";
            await Assessment(assessmentId).CreateAssessmentAsync(
                dto.PatientId, dto.PatientName, dto.AssessmentType,
                dto.AssessmentDate, dto.PeriodStart, dto.PeriodEnd,
                dto.LevelOfCare, dto.CompletedBy, dto.CompletedByTitle);
            GECAssessmentState state = await Assessment(assessmentId).GetAssessmentAsync();
            await AssessmentIndex(dto.PatientId).UpsertAssessmentAsync(BuildAssessmentIndex(state));
            return Created($"/api/gec/assessments/{Uri.EscapeDataString(assessmentId)}", new { assessmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GEC assessment for patient {PatientId}", dto.PatientId);
            return StatusCode(500, "Error creating assessment.");
        }
    }

    [HttpGet("assessments/{assessmentId}")]
    public async Task<IActionResult> GetAssessment(string assessmentId)
    {
        try
        {
            return Ok(await Assessment(assessmentId).GetAssessmentAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error retrieving assessment.");
        }
    }

    [HttpGet("assessments/patient/{patientId}")]
    public async Task<IActionResult> GetAssessmentsByPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await AssessmentIndex(id).GetAllAssessmentsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessments for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving assessments.");
        }
    }

    [HttpGet("assessments/patient/{patientId}/latest")]
    public async Task<IActionResult> GetLatestAssessment(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            GECAssessmentIndexEntry? latest = await AssessmentIndex(id).GetLatestAssessmentAsync();
            if (latest is null) return NotFound();
            return Ok(latest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest assessment for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving latest assessment.");
        }
    }

    [HttpPost("assessments/{assessmentId}/adl")]
    public async Task<IActionResult> RecordADLScores(string assessmentId, [FromBody] RecordADLScoresDto dto)
    {
        try
        {
            await Assessment(assessmentId).RecordADLScoresAsync(
                dto.BedMobility, dto.Transfer, dto.Walking, dto.Dressing,
                dto.Eating, dto.ToiletUse, dto.PersonalHygiene);
            GECAssessmentState state = await Assessment(assessmentId).GetAssessmentAsync();
            await AssessmentIndex(state.PatientId).UpsertAssessmentAsync(BuildAssessmentIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording ADL scores for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error recording ADL scores.");
        }
    }

    [HttpPost("assessments/{assessmentId}/cognitive")]
    public async Task<IActionResult> RecordCognitiveMood(string assessmentId, [FromBody] RecordCognitiveMoodDto dto)
    {
        try
        {
            await Assessment(assessmentId).RecordCognitiveMoodAsync(dto.BIMSScore, dto.PHQ9Score);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording cognitive/mood for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error recording cognitive/mood.");
        }
    }

    [HttpPost("assessments/{assessmentId}/clinical")]
    public async Task<IActionResult> RecordClinicalIndicators(string assessmentId, [FromBody] RecordClinicalIndicatorsDto dto)
    {
        try
        {
            await Assessment(assessmentId).RecordClinicalIndicatorsAsync(
                dto.PainPresent, dto.PainFrequency, dto.PressureUlcerCount,
                dto.FallsLast30Days, dto.NutritionConcern, dto.BehaviorSymptoms);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording clinical indicators for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error recording clinical indicators.");
        }
    }

    [HttpPost("assessments/{assessmentId}/rug")]
    public async Task<IActionResult> SetRUGCategory(string assessmentId, [FromBody] SetRUGCategoryDto dto)
    {
        try
        {
            await Assessment(assessmentId).SetRUGCategoryAsync(dto.RUGCategory);
            GECAssessmentState state = await Assessment(assessmentId).GetAssessmentAsync();
            await AssessmentIndex(state.PatientId).UpsertAssessmentAsync(BuildAssessmentIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting RUG category for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error setting RUG category.");
        }
    }

    [HttpPost("assessments/{assessmentId}/submit")]
    public async Task<IActionResult> SubmitAssessment(string assessmentId, [FromBody] SubmitAssessmentDto dto)
    {
        try
        {
            await Assessment(assessmentId).SubmitAssessmentAsync(dto.SubmittedBy);
            GECAssessmentState state = await Assessment(assessmentId).GetAssessmentAsync();
            await AssessmentIndex(state.PatientId).UpsertAssessmentAsync(BuildAssessmentIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting assessment {AssessmentId}", assessmentId);
            return StatusCode(500, "Error submitting assessment.");
        }
    }

    // ── CLC Admissions ────────────────────────────────────────────────────────

    [HttpPost("admissions")]
    public async Task<IActionResult> AdmitPatient([FromBody] AdmitCLCPatientDto dto)
    {
        try
        {
            string admissionId = $"CLC-ADMIT:{Guid.NewGuid()}";
            await Admission(admissionId).AdmitPatientAsync(
                dto.PatientId, dto.PatientName, dto.PatientDOB,
                dto.AdmitDate, dto.AdmitSource, dto.LevelOfCare,
                dto.Ward, dto.BedRoom, dto.AttendingPhysician,
                dto.PrimaryDiagnosis, dto.ReferringFacility,
                dto.AnticipatedDischargeDate, dto.Notes);
            CLCAdmissionState state = await Admission(admissionId).GetAdmissionAsync();
            await AdmissionIndex().UpsertAdmissionAsync(BuildAdmissionIndex(state));
            return Created($"/api/gec/admissions/{Uri.EscapeDataString(admissionId)}", new { admissionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error admitting patient {PatientId} to CLC", dto.PatientId);
            return StatusCode(500, "Error admitting patient.");
        }
    }

    [HttpGet("admissions/{admissionId}")]
    public async Task<IActionResult> GetAdmission(string admissionId)
    {
        try
        {
            return Ok(await Admission(admissionId).GetAdmissionAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admission {AdmissionId}", admissionId);
            return StatusCode(500, "Error retrieving admission.");
        }
    }

    [HttpGet("admissions/census")]
    public async Task<IActionResult> GetActiveCensus()
    {
        try
        {
            return Ok(await AdmissionIndex().GetActiveCensusAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CLC census");
            return StatusCode(500, "Error retrieving census.");
        }
    }

    [HttpGet("admissions/census/ward/{ward}")]
    public async Task<IActionResult> GetCensusByWard(string ward)
    {
        try
        {
            return Ok(await AdmissionIndex().GetAdmissionsByWardAsync(Uri.UnescapeDataString(ward)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving census for ward {Ward}", ward);
            return StatusCode(500, "Error retrieving ward census.");
        }
    }

    [HttpGet("admissions/census/levelofcare/{level}")]
    public async Task<IActionResult> GetCensusByLevelOfCare(GECLevelOfCare level)
    {
        try
        {
            return Ok(await AdmissionIndex().GetAdmissionsByLevelOfCareAsync(level));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving census for level of care {Level}", level);
            return StatusCode(500, "Error retrieving census.");
        }
    }

    [HttpGet("admissions/discharges/upcoming")]
    public async Task<IActionResult> GetUpcomingDischarges([FromQuery] int withinDays = 7)
    {
        try
        {
            return Ok(await AdmissionIndex().GetAnticipatedDischargesAsync(withinDays));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming discharges");
            return StatusCode(500, "Error retrieving upcoming discharges.");
        }
    }

    [HttpPost("admissions/{admissionId}/levelofcare")]
    public async Task<IActionResult> UpdateLevelOfCare(string admissionId, [FromBody] UpdateLevelOfCareDto dto)
    {
        try
        {
            await Admission(admissionId).UpdateLevelOfCareAsync(dto.LevelOfCare);
            CLCAdmissionState state = await Admission(admissionId).GetAdmissionAsync();
            await AdmissionIndex().UpsertAdmissionAsync(BuildAdmissionIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating level of care for admission {AdmissionId}", admissionId);
            return StatusCode(500, "Error updating level of care.");
        }
    }

    [HttpPost("admissions/{admissionId}/bed")]
    public async Task<IActionResult> UpdateBedAssignment(string admissionId, [FromBody] UpdateBedAssignmentDto dto)
    {
        try
        {
            await Admission(admissionId).UpdateBedAssignmentAsync(dto.Ward, dto.BedRoom);
            CLCAdmissionState state = await Admission(admissionId).GetAdmissionAsync();
            await AdmissionIndex().UpsertAdmissionAsync(BuildAdmissionIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bed assignment for admission {AdmissionId}", admissionId);
            return StatusCode(500, "Error updating bed assignment.");
        }
    }

    [HttpPost("admissions/{admissionId}/discharge")]
    public async Task<IActionResult> DischargePatient(string admissionId, [FromBody] DischargeCLCPatientDto dto)
    {
        try
        {
            await Admission(admissionId).DischargePatientAsync(dto.DischargeDestination, dto.DischargeNotes);
            CLCAdmissionState state = await Admission(admissionId).GetAdmissionAsync();
            await AdmissionIndex().UpsertAdmissionAsync(BuildAdmissionIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging patient from admission {AdmissionId}", admissionId);
            return StatusCode(500, "Error discharging patient.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<CLCAdmissionIndexEntry> census = await AdmissionIndex().GetActiveCensusAsync();
            List<CLCAdmissionIndexEntry> upcoming = await AdmissionIndex().GetAnticipatedDischargesAsync(7);

            var dashboard = new
            {
                TotalCensusCurrent = census.Count,
                ActivePatients = census.Count(a => a.Status == CLCAdmissionStatus.Active),
                OnLeave = census.Count(a => a.Status == CLCAdmissionStatus.OnLeave),
                SkilledNursing = census.Count(a => a.LevelOfCare == GECLevelOfCare.SkilledNursing),
                Hospice = census.Count(a => a.LevelOfCare == GECLevelOfCare.Hospice),
                Rehabilitation = census.Count(a => a.LevelOfCare == GECLevelOfCare.SubAcuteRehabilitation),
                DementiaCare = census.Count(a => a.LevelOfCare == GECLevelOfCare.DementiaCare),
                AnticipatedDischargesNext7Days = upcoming.Count
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating GEC dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CreateGECAssessmentDto(
    string PatientId,
    string PatientName,
    GECAssessmentType AssessmentType,
    DateTime AssessmentDate,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    GECLevelOfCare LevelOfCare,
    string CompletedBy,
    string CompletedByTitle);

public record RecordADLScoresDto(
    int BedMobility,
    int Transfer,
    int Walking,
    int Dressing,
    int Eating,
    int ToiletUse,
    int PersonalHygiene);

public record RecordCognitiveMoodDto(int? BIMSScore, int? PHQ9Score);

public record RecordClinicalIndicatorsDto(
    bool PainPresent,
    string PainFrequency,
    int PressureUlcerCount,
    int FallsLast30Days,
    bool NutritionConcern,
    bool BehaviorSymptoms);

public record SetRUGCategoryDto(GECRUGCategory RUGCategory);

public record SubmitAssessmentDto(string SubmittedBy);

public record AdmitCLCPatientDto(
    string PatientId,
    string PatientName,
    DateTime? PatientDOB,
    DateTime AdmitDate,
    CLCAdmitSource AdmitSource,
    GECLevelOfCare LevelOfCare,
    string Ward,
    string BedRoom,
    string AttendingPhysician,
    string PrimaryDiagnosis,
    string ReferringFacility,
    DateTime? AnticipatedDischargeDate,
    string Notes);

public record UpdateLevelOfCareDto(GECLevelOfCare LevelOfCare);

public record UpdateBedAssignmentDto(string Ward, string BedRoom);

public record DischargeCLCPatientDto(CLCDischargeDestination DischargeDestination, string DischargeNotes);
