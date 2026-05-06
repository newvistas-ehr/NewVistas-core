// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Home-Based Primary Care (HBPC) and Home Health Care (HHC) visits.
/// Non-telehealth, in-home medical care. VistA File #750.
/// HBPC.m, HHCV.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HomeHealthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HomeHealthController> _logger;

    public HomeHealthController(IGrainFactory grainFactory, ILogger<HomeHealthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IHBPCPatientGrain HBPCPatient(string patientId)
        => _grainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

    private IHBPCRegistryGrain HBPCRegistry()
        => _grainFactory.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY");

    private IHHCVisitGrain Visit(string visitId)
        => _grainFactory.GetGrain<IHHCVisitGrain>(Uri.UnescapeDataString(visitId));

    private IHHCVisitIndexGrain VisitIndex(string patientId)
        => _grainFactory.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{patientId}");

    private static HBPCRegistryEntry BuildRegistryEntry(HBPCPatientState s) => new()
    {
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        EnrollmentDate = s.EnrollmentDate,
        LevelOfCare = s.LevelOfCare,
        ProgramStatus = s.ProgramStatus,
        PrimaryDiagnosis = s.PrimaryDiagnosis,
        LastVisitDate = s.LastVisitDate,
        NextScheduledVisit = s.NextScheduledVisit,
        TotalVisitsThisYear = s.TotalVisitsThisYear
    };

    private static HHCVisitIndexEntry BuildVisitIndex(HHCVisitState s) => new()
    {
        VisitId = s.VisitId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        VisitDate = s.VisitDate,
        Discipline = s.Discipline,
        VisitType = s.VisitType,
        Status = s.Status,
        ClinicianName = s.ClinicianName,
        DurationMinutes = s.DurationMinutes
    };

    // ── HBPC Patient Enrollment ────────────────────────────────────────────────

    [HttpPost("patients/enroll")]
    public async Task<IActionResult> EnrollPatient([FromBody] EnrollHBPCPatientDto dto)
    {
        try
        {
            string patientId = Uri.UnescapeDataString(dto.PatientId.Trim());
            await HBPCPatient(patientId).EnrollPatientAsync(
                patientId, dto.PatientName, dto.EnrollmentDate,
                dto.LevelOfCare, dto.PrimaryDiagnosis,
                dto.PrimaryCaregiver, dto.HomeAddress);
            HBPCPatientState state = await HBPCPatient(patientId).GetPatientAsync();
            await HBPCRegistry().UpsertPatientAsync(BuildRegistryEntry(state));
            return Created($"/api/homehealth/patients/{Uri.EscapeDataString(patientId)}", new { patientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling patient {PatientId} in HBPC", dto.PatientId);
            return StatusCode(500, "Error enrolling patient.");
        }
    }

    [HttpGet("patients/{patientId}")]
    public async Task<IActionResult> GetPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await HBPCPatient(id).GetPatientAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HBPC patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving patient.");
        }
    }

    [HttpPost("patients/{patientId}/levelofcare")]
    public async Task<IActionResult> UpdateLevelOfCare(string patientId, [FromBody] UpdateHBPCLevelOfCareDto dto)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            await HBPCPatient(id).UpdateLevelOfCareAsync(dto.LevelOfCare);
            HBPCPatientState state = await HBPCPatient(id).GetPatientAsync();
            await HBPCRegistry().UpsertPatientAsync(BuildRegistryEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating level of care for patient {PatientId}", patientId);
            return StatusCode(500, "Error updating level of care.");
        }
    }

    [HttpPost("patients/{patientId}/suspend")]
    public async Task<IActionResult> SuspendEnrollment(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            await HBPCPatient(id).SuspendEnrollmentAsync();
            HBPCPatientState state = await HBPCPatient(id).GetPatientAsync();
            await HBPCRegistry().UpsertPatientAsync(BuildRegistryEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending enrollment for patient {PatientId}", patientId);
            return StatusCode(500, "Error suspending enrollment.");
        }
    }

    [HttpPost("patients/{patientId}/reactivate")]
    public async Task<IActionResult> ReactivateEnrollment(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            await HBPCPatient(id).ReactivateEnrollmentAsync();
            HBPCPatientState state = await HBPCPatient(id).GetPatientAsync();
            await HBPCRegistry().UpsertPatientAsync(BuildRegistryEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating enrollment for patient {PatientId}", patientId);
            return StatusCode(500, "Error reactivating enrollment.");
        }
    }

    [HttpPost("patients/{patientId}/discharge")]
    public async Task<IActionResult> DischargePatient(string patientId, [FromBody] DischargeHBPCPatientDto dto)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            await HBPCPatient(id).DischargePatientAsync(dto.Reason, dto.DischargeNotes);
            HBPCPatientState state = await HBPCPatient(id).GetPatientAsync();
            await HBPCRegistry().UpsertPatientAsync(BuildRegistryEntry(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging HBPC patient {PatientId}", patientId);
            return StatusCode(500, "Error discharging patient.");
        }
    }

    // ── HBPC Registry ─────────────────────────────────────────────────────────

    [HttpGet("registry")]
    public async Task<IActionResult> GetAllPatients()
    {
        try
        {
            return Ok(await HBPCRegistry().GetAllPatientsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HBPC registry");
            return StatusCode(500, "Error retrieving registry.");
        }
    }

    [HttpGet("registry/active")]
    public async Task<IActionResult> GetActivePatients()
    {
        try
        {
            return Ok(await HBPCRegistry().GetActivePatientsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active HBPC patients");
            return StatusCode(500, "Error retrieving active patients.");
        }
    }

    [HttpGet("registry/levelofcare/{level}")]
    public async Task<IActionResult> GetPatientsByLevelOfCare(HBPCLevelOfCare level)
    {
        try
        {
            return Ok(await HBPCRegistry().GetPatientsByLevelOfCareAsync(level));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HBPC patients by level of care {Level}", level);
            return StatusCode(500, "Error retrieving patients.");
        }
    }

    [HttpGet("registry/upcoming-visits")]
    public async Task<IActionResult> GetPatientsWithUpcomingVisits([FromQuery] int withinDays = 7)
    {
        try
        {
            return Ok(await HBPCRegistry().GetPatientsWithUpcomingVisitsAsync(withinDays));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patients with upcoming visits");
            return StatusCode(500, "Error retrieving patients.");
        }
    }

    [HttpGet("registry/no-recent-visit")]
    public async Task<IActionResult> GetPatientsWithNoRecentVisit([FromQuery] int daysSinceLastVisit = 30)
    {
        try
        {
            return Ok(await HBPCRegistry().GetPatientsWithNoRecentVisitAsync(daysSinceLastVisit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patients with no recent visit");
            return StatusCode(500, "Error retrieving patients.");
        }
    }

    // ── HHC Visits ────────────────────────────────────────────────────────────

    [HttpPost("visits/schedule")]
    public async Task<IActionResult> ScheduleVisit([FromBody] ScheduleHHCVisitDto dto)
    {
        try
        {
            string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
            string patientId = Uri.UnescapeDataString(dto.PatientId.Trim());
            await Visit(visitId).ScheduleVisitAsync(
                patientId, dto.PatientName, dto.VisitDate,
                dto.Discipline, dto.VisitType,
                dto.ClinicianId, dto.ClinicianName, dto.Notes);
            HHCVisitState state = await Visit(visitId).GetVisitAsync();
            await VisitIndex(patientId).UpsertVisitAsync(BuildVisitIndex(state));
            return Created($"/api/homehealth/visits/{Uri.EscapeDataString(visitId)}", new { visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling HHC visit for patient {PatientId}", dto.PatientId);
            return StatusCode(500, "Error scheduling visit.");
        }
    }

    [HttpGet("visits/{visitId}")]
    public async Task<IActionResult> GetVisit(string visitId)
    {
        try
        {
            return Ok(await Visit(visitId).GetVisitAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visit {VisitId}", visitId);
            return StatusCode(500, "Error retrieving visit.");
        }
    }

    [HttpGet("visits/patient/{patientId}")]
    public async Task<IActionResult> GetVisitsByPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await VisitIndex(id).GetAllVisitsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visits for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving visits.");
        }
    }

    [HttpGet("visits/patient/{patientId}/upcoming")]
    public async Task<IActionResult> GetUpcomingVisits(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await VisitIndex(id).GetUpcomingVisitsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming visits for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving upcoming visits.");
        }
    }

    [HttpPost("visits/{visitId}/complete")]
    public async Task<IActionResult> CompleteVisit(string visitId, [FromBody] CompleteHHCVisitDto dto)
    {
        try
        {
            await Visit(visitId).CompleteVisitAsync(
                dto.DurationMinutes, dto.VitalSigns, dto.Interventions,
                dto.PatientResponse, dto.GoalsProgress, dto.NextVisitDate, dto.Notes);
            HHCVisitState state = await Visit(visitId).GetVisitAsync();
            await VisitIndex(state.PatientId).UpsertVisitAsync(BuildVisitIndex(state));
            // Update HBPC patient record with last visit
            await HBPCPatient(state.PatientId).RecordVisitAsync(state.VisitDate, state.NextVisitDate);
            HBPCPatientState patientState = await HBPCPatient(state.PatientId).GetPatientAsync();
            await HBPCRegistry().UpsertPatientAsync(BuildRegistryEntry(patientState));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing visit {VisitId}", visitId);
            return StatusCode(500, "Error completing visit.");
        }
    }

    [HttpPost("visits/{visitId}/cancel")]
    public async Task<IActionResult> CancelVisit(string visitId, [FromBody] CancelHHCVisitDto dto)
    {
        try
        {
            await Visit(visitId).CancelVisitAsync(dto.CancellationReason);
            HHCVisitState state = await Visit(visitId).GetVisitAsync();
            await VisitIndex(state.PatientId).UpsertVisitAsync(BuildVisitIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling visit {VisitId}", visitId);
            return StatusCode(500, "Error cancelling visit.");
        }
    }

    [HttpPost("visits/{visitId}/no-answer")]
    public async Task<IActionResult> MarkNoAnswer(string visitId)
    {
        try
        {
            await Visit(visitId).MarkNoAnswerAsync();
            HHCVisitState state = await Visit(visitId).GetVisitAsync();
            await VisitIndex(state.PatientId).UpsertVisitAsync(BuildVisitIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking visit {VisitId} as no answer", visitId);
            return StatusCode(500, "Error updating visit.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<HBPCRegistryEntry> allPatients = await HBPCRegistry().GetAllPatientsAsync();
            List<HBPCRegistryEntry> upcoming = await HBPCRegistry().GetPatientsWithUpcomingVisitsAsync(7);
            List<HBPCRegistryEntry> noRecent = await HBPCRegistry().GetPatientsWithNoRecentVisitAsync(30);

            var dashboard = new
            {
                TotalEnrolled = allPatients.Count,
                ActivePatients = allPatients.Count(p => p.ProgramStatus == HBPCProgramStatus.Active),
                SuspendedPatients = allPatients.Count(p => p.ProgramStatus == HBPCProgramStatus.Suspended),
                BasicCare = allPatients.Count(p => p.LevelOfCare == HBPCLevelOfCare.Basic && p.ProgramStatus == HBPCProgramStatus.Active),
                EnhancedCare = allPatients.Count(p => p.LevelOfCare == HBPCLevelOfCare.Enhanced && p.ProgramStatus == HBPCProgramStatus.Active),
                PalliativeCare = allPatients.Count(p => p.LevelOfCare == HBPCLevelOfCare.Palliative && p.ProgramStatus == HBPCProgramStatus.Active),
                PatientsWithUpcomingVisitNext7Days = upcoming.Count,
                PatientsWithNoVisitLast30Days = noRecent.Count
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating home health dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record EnrollHBPCPatientDto(
    string PatientId,
    string PatientName,
    DateTime EnrollmentDate,
    HBPCLevelOfCare LevelOfCare,
    string PrimaryDiagnosis,
    string PrimaryCaregiver,
    string HomeAddress);

public record UpdateHBPCLevelOfCareDto(HBPCLevelOfCare LevelOfCare);

public record DischargeHBPCPatientDto(HBPCDischargeReason Reason, string DischargeNotes);

public record ScheduleHHCVisitDto(
    string PatientId,
    string PatientName,
    DateTime VisitDate,
    HHCVisitDiscipline Discipline,
    HHCVisitType VisitType,
    string ClinicianId,
    string ClinicianName,
    string Notes);

public record CompleteHHCVisitDto(
    int DurationMinutes,
    string VitalSigns,
    List<string> Interventions,
    string PatientResponse,
    string GoalsProgress,
    DateTime? NextVisitDate,
    string Notes);

public record CancelHHCVisitDto(string CancellationReason);
