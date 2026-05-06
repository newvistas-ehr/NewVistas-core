// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Spinal Cord Injury / Dysfunction (SCI/D) Registry API.
/// VistA SCI PATIENT file (#154).
/// MUMPS routines: SCIRPAU.m (add/update), SCIRPIV.m (patient inquiry).
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class SCIController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SCIController> _logger;

    public SCIController(IGrainFactory grainFactory, ILogger<SCIController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private ISCIIndexGrain GetIndex()
        => _grainFactory.GetGrain<ISCIIndexGrain>("SCI-INDEX");

    // ─── Registry Index (system-wide) ─────────────────────────────────────────

    /// <summary>List all patients enrolled in the SCI/D registry.</summary>
    [HttpGet("api/sci")]
    [ProducesResponseType(typeof(List<SCIIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SCIIndexEntry>>> GetRegistry(
        [FromQuery] SCIRegistryStatus? status = null)
    {
        try
        {
            List<SCIIndexEntry> entries = status.HasValue
                ? await GetIndex().GetByStatusAsync(status.Value)
                : await GetIndex().GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCI registry");
            return StatusCode(500, "An error occurred while retrieving the SCI registry");
        }
    }

    /// <summary>List SCI/D registry patients by neurological level prefix (e.g., "C" or "T").</summary>
    [HttpGet("api/sci/by-level")]
    [ProducesResponseType(typeof(List<SCIIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SCIIndexEntry>>> GetByNeurologicalLevel(
        [FromQuery] string levelPrefix)
    {
        try
        {
            List<SCIIndexEntry> entries = await GetIndex().GetByNeurologicalLevelAsync(levelPrefix);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCI registry by level {Level}", levelPrefix);
            return StatusCode(500, "An error occurred while retrieving the SCI registry");
        }
    }

    // ─── Patient-specific SCI record ──────────────────────────────────────────

    /// <summary>Get the SCI/D registry record for a patient.</summary>
    [HttpGet("api/patient/{patientId}/sci")]
    [ProducesResponseType(typeof(SCIPatientState), StatusCodes.Status200OK)]
    public async Task<ActionResult<SCIPatientState>> GetSCIPatient(string patientId)
    {
        try
        {
            SCIPatientState record = await GetWorkflow(patientId).GetSCIPatientAsync();
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCI record for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the SCI record");
        }
    }

    /// <summary>Enroll a patient in the SCI/D registry.</summary>
    [HttpPost("api/patient/{patientId}/sci/enroll")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Enroll(string patientId, [FromBody] EnrollSCIRequest request)
    {
        try
        {
            await GetWorkflow(patientId).EnrollInSCIRegistryAsync(
                request.EnrollmentDate,
                request.SCICenter,
                request.DateOfInjuryOnset,
                request.InjuryType,
                request.Etiology,
                request.NeurologicalLevelOfInjury,
                request.AisGrade,
                request.PrimaryDiagnosisCode,
                request.PrimaryDiagnosisDescription,
                request.EnrollingProviderId,
                request.EnrollingProviderName,
                request.PrimaryProviderId,
                request.PrimaryProviderName,
                request.BladderManagement,
                request.BowelProgram,
                request.LocomotionMethod,
                request.LivingSituation,
                request.AssociatedConditions,
                request.Notes);

            _logger.LogInformation(
                "Enrolled patient {PatientId} in SCI registry, NLI={Level} AIS={Grade}",
                patientId, request.NeurologicalLevelOfInjury, request.AisGrade);

            return Created($"api/patient/{patientId}/sci", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling patient {PatientId} in SCI registry", patientId);
            return StatusCode(500, "An error occurred while enrolling in the SCI registry");
        }
    }

    /// <summary>Update the clinical data for an enrolled SCI/D patient.</summary>
    [HttpPut("api/patient/{patientId}/sci")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateClinicalData(string patientId, [FromBody] UpdateSCIRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateSCIPatientAsync(
                request.NeurologicalLevelOfInjury,
                request.AisGrade,
                request.PrimaryDiagnosisCode,
                request.PrimaryDiagnosisDescription,
                request.BladderManagement,
                request.BowelProgram,
                request.LocomotionMethod,
                request.LivingSituation,
                request.AssociatedConditions,
                request.PrimaryProviderId,
                request.PrimaryProviderName,
                request.Notes);

            _logger.LogInformation(
                "Updated SCI clinical data for patient {PatientId}", patientId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SCI data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating the SCI record");
        }
    }

    /// <summary>Update the enrollment status of an SCI/D patient (e.g., Inactive, Deceased).</summary>
    [HttpPut("api/patient/{patientId}/sci/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(string patientId, [FromBody] UpdateSCIStatusRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateSCIStatusAsync(request.Status, request.Notes);

            _logger.LogInformation(
                "Updated SCI status to {Status} for patient {PatientId}", request.Status, patientId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SCI status for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating the SCI status");
        }
    }

    // ─── Annual Encounters ─────────────────────────────────────────────────────

    /// <summary>List all annual encounter records for an SCI/D patient.</summary>
    [HttpGet("api/patient/{patientId}/sci/encounters")]
    [ProducesResponseType(typeof(List<SCIAnnualEncounterRecord>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SCIAnnualEncounterRecord>>> GetEncounters(string patientId)
    {
        try
        {
            List<SCIAnnualEncounterRecord> encounters =
                await GetWorkflow(patientId).GetSCIAnnualEncountersAsync();
            return Ok(encounters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SCI encounters for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving SCI encounters");
        }
    }

    /// <summary>Add an annual review or follow-up encounter for an SCI/D patient.</summary>
    [HttpPost("api/patient/{patientId}/sci/encounters")]
    [ProducesResponseType(typeof(AddSCIEncounterResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddEncounter(string patientId, [FromBody] AddSCIEncounterRequest request)
    {
        try
        {
            string encounterId = await GetWorkflow(patientId).AddSCIAnnualEncounterAsync(
                request.FiscalYear,
                request.EncounterDate,
                request.EncounterType,
                request.AisGrade,
                request.NeurologicalLevel,
                request.HospitalAdmissions,
                request.UrinaryTractInfections,
                request.PressureInjuryCount,
                request.HighestPressureInjuryStage,
                request.BladderManagement,
                request.BowelProgram,
                request.LivingSituation,
                request.EquipmentNeeds,
                request.ProviderId,
                request.ProviderName,
                request.Notes);

            _logger.LogInformation(
                "Added SCI encounter {EncounterId} (FY{FY}) for patient {PatientId}",
                encounterId, request.FiscalYear, patientId);

            return Created(
                $"api/patient/{patientId}/sci/encounters",
                new AddSCIEncounterResponse(encounterId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding SCI encounter for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding the SCI encounter");
        }
    }
}

// ─── Request / Response DTOs ──────────────────────────────────────────────────

public record EnrollSCIRequest(
    DateTime EnrollmentDate,
    string? SCICenter,
    DateTime? DateOfInjuryOnset,
    SCIInjuryType InjuryType,
    SCIEtiology Etiology,
    string NeurologicalLevelOfInjury,
    SCIAisGrade AisGrade,
    string? PrimaryDiagnosisCode,
    string? PrimaryDiagnosisDescription,
    string? EnrollingProviderId,
    string? EnrollingProviderName,
    string? PrimaryProviderId,
    string? PrimaryProviderName,
    SCIBladderManagement? BladderManagement,
    SCIBowelProgram? BowelProgram,
    SCILocomotionMethod? LocomotionMethod,
    SCILivingSituation? LivingSituation,
    List<string>? AssociatedConditions,
    string? Notes);

public record UpdateSCIRequest(
    string NeurologicalLevelOfInjury,
    SCIAisGrade AisGrade,
    string? PrimaryDiagnosisCode,
    string? PrimaryDiagnosisDescription,
    SCIBladderManagement? BladderManagement,
    SCIBowelProgram? BowelProgram,
    SCILocomotionMethod? LocomotionMethod,
    SCILivingSituation? LivingSituation,
    List<string>? AssociatedConditions,
    string? PrimaryProviderId,
    string? PrimaryProviderName,
    string? Notes);

public record UpdateSCIStatusRequest(
    SCIRegistryStatus Status,
    string? Notes);

public record AddSCIEncounterRequest(
    int FiscalYear,
    DateTime EncounterDate,
    SCIEncounterType EncounterType,
    SCIAisGrade AisGrade,
    string NeurologicalLevel,
    int HospitalAdmissions,
    int UrinaryTractInfections,
    int PressureInjuryCount,
    int HighestPressureInjuryStage,
    SCIBladderManagement? BladderManagement,
    SCIBowelProgram? BowelProgram,
    SCILivingSituation? LivingSituation,
    List<string>? EquipmentNeeds,
    string? ProviderId,
    string? ProviderName,
    string? Notes);

public record AddSCIEncounterResponse(string EncounterId);
