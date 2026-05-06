// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/clinicalregistries")]
public class ClinicalCaseRegistriesController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ClinicalCaseRegistriesController> _logger;

    public ClinicalCaseRegistriesController(IGrainFactory grainFactory, ILogger<ClinicalCaseRegistriesController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IClinicalRegistrySiteIndexGrain SiteIndex =>
        _grainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>("CCR-SITE-IDX");

    private IClinicalRegistryIndexGrain GetRegistryIndex(RegistryType type) =>
        _grainFactory.GetGrain<IClinicalRegistryIndexGrain>($"CCR-IDX:{type}");

    private IClinicalRegistryEntryGrain GetEntryGrain(RegistryType type, string patientId) =>
        _grainFactory.GetGrain<IClinicalRegistryEntryGrain>($"CCR:{type}:{patientId}");

    private IPatientRegistryListGrain GetPatientList(string patientId) =>
        _grainFactory.GetGrain<IPatientRegistryListGrain>($"CCR-PAT:{patientId}");

    // ── Registry Queries ──────────────────────────────────────────────────────

    [HttpGet("{type}/entries")]
    public async Task<IActionResult> GetAllEntries(RegistryType type)
    {
        try
        {
            List<CCREntrySummary> result = await GetRegistryIndex(type).GetAllEntriesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {RegistryType} registry entries", type);
            return StatusCode(500, "An error occurred retrieving registry entries.");
        }
    }

    [HttpGet("{type}/entries/active")]
    public async Task<IActionResult> GetActiveEntries(RegistryType type)
    {
        try
        {
            List<CCREntrySummary> result = await GetRegistryIndex(type).GetActiveEntriesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active {RegistryType} registry entries", type);
            return StatusCode(500, "An error occurred retrieving active registry entries.");
        }
    }

    [HttpGet("{type}/patients/{patientId}")]
    public async Task<IActionResult> GetEntry(RegistryType type, string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            ClinicalRegistryEntryState result = await GetEntryGrain(type, decodedId).GetEntryAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {RegistryType} entry for patient {PatientId}", type, patientId);
            return StatusCode(500, "An error occurred retrieving the registry entry.");
        }
    }

    [HttpGet("patients/{patientId}/registries")]
    public async Task<IActionResult> GetPatientRegistries(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            List<PatientRegistryEnrollmentEntry> result = await GetPatientList(decodedId).GetAllEnrollmentsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving registries for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving patient registry memberships.");
        }
    }

    [HttpGet("site/recent")]
    public async Task<IActionResult> GetRecentEnrollments([FromQuery] int count = 20)
    {
        try
        {
            List<CCREntrySummary> result = await SiteIndex.GetRecentEnrollmentsAsync(count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent CCR enrollments");
            return StatusCode(500, "An error occurred retrieving recent enrollments.");
        }
    }

    // ── Enrollment Lifecycle ──────────────────────────────────────────────────

    [HttpPost("{type}/patients/{patientId}/enroll")]
    public async Task<IActionResult> EnrollPatient(RegistryType type, string patientId, [FromBody] CreateCCREnrollmentRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IClinicalRegistryEntryGrain entry = GetEntryGrain(type, decodedId);
            await entry.EnrollPatientAsync(
                decodedId, req.PatientName, req.DateOfBirth, type,
                req.EnrolledById, req.EnrolledByName,
                req.SiteId, req.SiteName,
                req.PrimaryProviderId, req.PrimaryProviderName, req.Notes);

            ClinicalRegistryEntryState state = await entry.GetEntryAsync();

            CCREntrySummary summary = new()
            {
                PatientId = decodedId,
                PatientName = req.PatientName,
                RegistryType = type,
                Status = CCREnrollmentStatus.Active,
                EnrollmentDate = state.EnrollmentDate,
                SiteId = req.SiteId,
                PrimaryProviderName = req.PrimaryProviderName,
                LastModifiedDate = DateTime.UtcNow,
            };

            await GetRegistryIndex(type).UpsertEntryAsync(summary);
            await SiteIndex.UpsertEntryAsync(summary);
            await GetPatientList(decodedId).UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
            {
                RegistryType = type,
                Status = CCREnrollmentStatus.Active,
                EnrollmentDate = state.EnrollmentDate,
                LastModifiedDate = DateTime.UtcNow,
                PrimaryProviderName = req.PrimaryProviderName,
            });

            return Created(
                $"/api/clinicalregistries/{type}/patients/{Uri.EscapeDataString(patientId)}",
                new { patientId = decodedId, registryType = type });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling patient {PatientId} in {RegistryType} registry", patientId, type);
            return StatusCode(500, "An error occurred enrolling the patient.");
        }
    }

    [HttpPost("{type}/patients/{patientId}/status")]
    public async Task<IActionResult> UpdateStatus(RegistryType type, string patientId, [FromBody] CCRUpdateStatusRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IClinicalRegistryEntryGrain entry = GetEntryGrain(type, decodedId);
            await entry.UpdateEnrollmentStatusAsync(req.Status, req.DeactivationDate, req.Reason);

            ClinicalRegistryEntryState state = await entry.GetEntryAsync();
            CCREntrySummary summary = new()
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                RegistryType = type,
                Status = state.EnrollmentStatus,
                EnrollmentDate = state.EnrollmentDate,
                SiteId = state.SiteId,
                PrimaryProviderName = state.PrimaryProviderName,
                LastModifiedDate = DateTime.UtcNow,
            };

            await GetRegistryIndex(type).UpsertEntryAsync(summary);
            await SiteIndex.UpsertEntryAsync(summary);
            await GetPatientList(decodedId).UpsertEnrollmentAsync(new PatientRegistryEnrollmentEntry
            {
                RegistryType = type,
                Status = state.EnrollmentStatus,
                EnrollmentDate = state.EnrollmentDate,
                LastModifiedDate = DateTime.UtcNow,
                PrimaryProviderName = state.PrimaryProviderName,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for {RegistryType} patient {PatientId}", type, patientId);
            return StatusCode(500, "An error occurred updating the enrollment status.");
        }
    }

    [HttpPost("{type}/patients/{patientId}/hiv")]
    public async Task<IActionResult> UpdateHIVData(RegistryType type, string patientId, [FromBody] CCRUpdateHIVRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            await GetEntryGrain(type, decodedId).UpdateHIVDataAsync(
                req.Stage, req.CD4Count, req.CD4Date,
                req.ViralLoadCopies, req.ViralLoadDate,
                req.IsVirallySuppressed, req.ARTStartDate, req.ARTRegimen);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating HIV data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating HIV data.");
        }
    }

    [HttpPost("{type}/patients/{patientId}/hepc")]
    public async Task<IActionResult> UpdateHepCData(RegistryType type, string patientId, [FromBody] CCRUpdateHepCRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            await GetEntryGrain(type, decodedId).UpdateHepCDataAsync(
                req.Genotype, req.FibrosisScore, req.TxStatus,
                req.TxStartDate, req.TxEndDate,
                req.SVRAchieved, req.SVRDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating HepC data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating HepC data.");
        }
    }

    [HttpPost("{type}/patients/{patientId}/diabetes")]
    public async Task<IActionResult> UpdateDiabetesData(RegistryType type, string patientId, [FromBody] CCRUpdateDiabetesRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            await GetEntryGrain(type, decodedId).UpdateDiabetesDataAsync(
                req.DiabetesType, req.HbA1cPct, req.HbA1cDate,
                req.IsInsulinDependent, req.Complications);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating diabetes data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating diabetes data.");
        }
    }

    /// <summary>Update enriched diabetes data: labs, BP, medications, exams, education.</summary>
    [HttpPost("{type}/patients/{patientId}/diabetes/enriched")]
    public async Task<IActionResult> UpdateDiabetesEnrichedData(RegistryType type, string patientId,
        [FromBody] CCRUpdateDiabetesEnrichedRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            await GetEntryGrain(type, decodedId).UpdateDiabetesEnrichedDataAsync(
                req.LdlMgDl, req.LdlDate,
                req.MicroalbuminMgL, req.MicroalbuminDate,
                req.BpSystolic, req.BpDiastolic, req.BpDate,
                req.Medications, req.Exams, req.Education);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating enriched diabetes data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating enriched diabetes data.");
        }
    }

    /// <summary>Update asthma-specific registry data.</summary>
    [HttpPost("{type}/patients/{patientId}/asthma")]
    public async Task<IActionResult> UpdateAsthmaData(RegistryType type, string patientId,
        [FromBody] CCRUpdateAsthmaRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            await GetEntryGrain(type, decodedId).UpdateAsthmaDataAsync(
                req.DiagnosisDate, req.Severity, req.ControlLevel,
                req.SpirometryDate, req.Fev1PredictedPct, req.Fev1FvcRatio,
                req.PeakFlowLPerMin, req.PeakFlowPersonalBest,
                req.ControllerMedication, req.RescueMedication,
                req.HasAsthmaActionPlan, req.AsthmaTriggers,
                req.AsthmaEdVisitsLast12Months);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asthma data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating asthma data.");
        }
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateCCREnrollmentRequest(
    string PatientName,
    DateTime? DateOfBirth,
    string EnrolledById,
    string EnrolledByName,
    string SiteId,
    string SiteName,
    string PrimaryProviderId,
    string PrimaryProviderName,
    string? Notes);

public record CCRUpdateStatusRequest(
    CCREnrollmentStatus Status,
    DateTime? DeactivationDate,
    string? Reason);

public record CCRUpdateHIVRequest(
    HIVStage Stage,
    decimal? CD4Count,
    DateTime? CD4Date,
    decimal? ViralLoadCopies,
    DateTime? ViralLoadDate,
    bool IsVirallySuppressed,
    DateTime? ARTStartDate,
    string? ARTRegimen);

public record CCRUpdateHepCRequest(
    HepCGenotype Genotype,
    decimal? FibrosisScore,
    HepCTreatmentStatus TxStatus,
    DateTime? TxStartDate,
    DateTime? TxEndDate,
    bool SVRAchieved,
    DateTime? SVRDate);

public record CCRUpdateDiabetesRequest(
    DiabetesType DiabetesType,
    decimal? HbA1cPct,
    DateTime? HbA1cDate,
    bool IsInsulinDependent,
    List<string> Complications);

public record CCRUpdateDiabetesEnrichedRequest(
    decimal? LdlMgDl,
    DateTime? LdlDate,
    decimal? MicroalbuminMgL,
    DateTime? MicroalbuminDate,
    int? BpSystolic,
    int? BpDiastolic,
    DateTime? BpDate,
    DiabetesMedicationStatus? Medications,
    DiabetesExamRecord? Exams,
    DiabetesEducationRecord? Education);

public record CCRUpdateAsthmaRequest(
    DateTime? DiagnosisDate,
    AsthmaSeverity? Severity,
    AsthmaControlLevel? ControlLevel,
    DateTime? SpirometryDate,
    decimal? Fev1PredictedPct,
    decimal? Fev1FvcRatio,
    int? PeakFlowLPerMin,
    int? PeakFlowPersonalBest,
    string? ControllerMedication,
    string? RescueMedication,
    bool HasAsthmaActionPlan,
    List<string>? AsthmaTriggers,
    int? AsthmaEdVisitsLast12Months);
