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
/// Event Capture API — workload/encounter capture for VA workload reporting.
/// Based on VistA Event Capture (EC) package, Files #721 and #724.
/// MUMPS routines: ECPEC.m (coordinator), ECPEEN.m (encounter entry), ECPEWL.m (workload list).
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class EventCaptureController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EventCaptureController> _logger;

    public EventCaptureController(IGrainFactory grainFactory, ILogger<EventCaptureController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IDssUnitIndexGrain DssUnitIndex()
        => _grainFactory.GetGrain<IDssUnitIndexGrain>("EC-DSS-IDX");

    private IDssUnitGrain DssUnit(string dssUnitId)
        => _grainFactory.GetGrain<IDssUnitGrain>(dssUnitId);

    private IEventCaptureEncounterIndexGrain EncounterIndex()
        => _grainFactory.GetGrain<IEventCaptureEncounterIndexGrain>("EC-ENCOUNTER-IDX");

    // ── Patient Encounter Endpoints ───────────────────────────────────────────

    /// <summary>List Event Capture encounters for a patient (newest first).</summary>
    [HttpGet("api/patient/{patientId}/eventcapture")]
    [ProducesResponseType(typeof(List<EventCaptureIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventCaptureIndexEntry>>> GetEncounters(
        string patientId,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            List<EventCaptureIndexEntry> encounters =
                await GetWorkflow(decoded).GetEventCaptureEncountersAsync(maxResults);
            return Ok(encounters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Event Capture encounters for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving encounters");
        }
    }

    /// <summary>Get a single Event Capture encounter by ID.</summary>
    [HttpGet("api/patient/{patientId}/eventcapture/{encounterId}")]
    [ProducesResponseType(typeof(EventCaptureEncounterState), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventCaptureEncounterState>> GetEncounter(
        string patientId,
        string encounterId)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            EventCaptureEncounterState state =
                await GetWorkflow(decoded).GetEventCaptureEncounterAsync(encounterId);
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Event Capture encounter {EncounterId}", encounterId);
            return StatusCode(500, "An error occurred while retrieving the encounter");
        }
    }

    /// <summary>Create a new Event Capture workload encounter.</summary>
    [HttpPost("api/patient/{patientId}/eventcapture")]
    [ProducesResponseType(typeof(CreateEncounterResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEncounter(
        string patientId,
        [FromBody] CreateEncounterRequest request)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            string encounterId = await GetWorkflow(decoded).CreateEventCaptureEncounterAsync(
                request.EncounterDateTime,
                request.DssUnitId,
                request.DssUnitName,
                request.DssUnitCode,
                request.ClinicId,
                request.ClinicName,
                request.LocationId,
                request.LocationName,
                request.PrimaryProviderId,
                request.PrimaryProviderName,
                request.AttendingProviderId,
                request.AttendingProviderName,
                request.EncounterType,
                request.PatientCategory,
                request.PrimaryStopCode,
                request.CreditStopCode,
                request.Comments);

            return Created(
                $"api/patient/{patientId}/eventcapture/{encounterId}",
                new CreateEncounterResponse(encounterId, "Encounter created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Event Capture encounter for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the encounter");
        }
    }

    /// <summary>Add a CPT procedure to an existing encounter.</summary>
    [HttpPost("api/patient/{patientId}/eventcapture/{encounterId}/procedures")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddProcedure(
        string patientId,
        string encounterId,
        [FromBody] AddProcedureRequest request)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            await GetWorkflow(decoded).AddEcProcedureAsync(
                encounterId,
                request.CptCode,
                request.ProcedureDescription,
                request.Quantity,
                request.ProviderId,
                request.ProviderName,
                request.ModifierCode);
            return Ok(new { Message = "Procedure added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding procedure to encounter {EncounterId}", encounterId);
            return StatusCode(500, "An error occurred while adding the procedure");
        }
    }

    /// <summary>Add a diagnosis to an existing encounter.</summary>
    [HttpPost("api/patient/{patientId}/eventcapture/{encounterId}/diagnoses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDiagnosis(
        string patientId,
        string encounterId,
        [FromBody] AddDiagnosisRequest request)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            await GetWorkflow(decoded).AddEcDiagnosisAsync(
                encounterId,
                request.Icd10Code,
                request.Description,
                request.IsPrimary);
            return Ok(new { Message = "Diagnosis added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding diagnosis to encounter {EncounterId}", encounterId);
            return StatusCode(500, "An error occurred while adding the diagnosis");
        }
    }

    /// <summary>Complete (check out) an Event Capture encounter.</summary>
    [HttpPost("api/patient/{patientId}/eventcapture/{encounterId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteEncounter(
        string patientId,
        string encounterId,
        [FromBody] CompleteEncounterRequest request)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            await GetWorkflow(decoded).CompleteEventCaptureEncounterAsync(
                encounterId,
                request.CheckOutDateTime,
                request.VisitLengthMinutes);
            return Ok(new { EncounterId = encounterId, Message = "Encounter completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing encounter {EncounterId}", encounterId);
            return StatusCode(500, "An error occurred while completing the encounter");
        }
    }

    /// <summary>Soft-delete an Event Capture encounter.</summary>
    [HttpDelete("api/patient/{patientId}/eventcapture/{encounterId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteEncounter(
        string patientId,
        string encounterId,
        [FromBody] DeleteEncounterRequest request)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(patientId.Trim());
            await GetWorkflow(decoded).DeleteEventCaptureEncounterAsync(
                encounterId,
                request.DeletedByProviderId,
                request.DeletedByProviderName,
                request.Reason);
            return Ok(new { EncounterId = encounterId, Message = "Encounter deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting encounter {EncounterId}", encounterId);
            return StatusCode(500, "An error occurred while deleting the encounter");
        }
    }

    // ── Cross-patient workload query ──────────────────────────────────────────

    /// <summary>
    /// Search encounters across patients (workload reporting).
    /// Supports filtering by DSS unit, provider, date range, and status.
    /// Corresponds to VistA ECPEWL workload list.
    /// </summary>
    [HttpGet("api/eventcapture/encounters")]
    [ProducesResponseType(typeof(List<EventCaptureIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventCaptureIndexEntry>>> SearchEncounters(
        [FromQuery] string? patientId,
        [FromQuery] string? dssUnitId,
        [FromQuery] string? providerId,
        [FromQuery] EcEncounterStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            List<EventCaptureIndexEntry> results = await EncounterIndex().SearchAsync(
                patientId, dssUnitId, providerId, status, fromDate, toDate, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Event Capture encounters");
            return StatusCode(500, "An error occurred while searching encounters");
        }
    }

    // ── DSS Unit Endpoints ────────────────────────────────────────────────────

    /// <summary>List DSS units, optionally filtered by active status or search text.</summary>
    [HttpGet("api/eventcapture/dss-units")]
    [ProducesResponseType(typeof(List<DssUnitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DssUnitIndexEntry>>> GetDssUnits(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            List<DssUnitIndexEntry> units = await DssUnitIndex().SearchAsync(search, activeOnly, maxResults);
            return Ok(units);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DSS units");
            return StatusCode(500, "An error occurred while retrieving DSS units");
        }
    }

    /// <summary>Get a single DSS unit by ID.</summary>
    [HttpGet("api/eventcapture/dss-units/{dssUnitId}")]
    [ProducesResponseType(typeof(DssUnitState), StatusCodes.Status200OK)]
    public async Task<ActionResult<DssUnitState>> GetDssUnit(string dssUnitId)
    {
        try
        {
            DssUnitState state = await DssUnit(dssUnitId).GetUnitAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DSS unit {DssUnitId}", dssUnitId);
            return StatusCode(500, "An error occurred while retrieving the DSS unit");
        }
    }

    /// <summary>Create or update a DSS unit definition.</summary>
    [HttpPost("api/eventcapture/dss-units")]
    [ProducesResponseType(typeof(CreateDssUnitResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDssUnit([FromBody] CreateDssUnitRequest request)
    {
        try
        {
            string dssUnitId = $"EC-DSS-UNIT:{Guid.NewGuid()}";

            await DssUnit(dssUnitId).UpsertAsync(
                request.UnitName,
                request.UnitCode,
                request.DivisionId,
                request.DivisionName,
                request.PrimaryStopCode,
                request.CreditStopCode,
                request.TreatmentCode,
                request.Description,
                true);

            // Update the index
            DssUnitState state = await DssUnit(dssUnitId).GetUnitAsync();
            await DssUnitIndex().AddOrUpdateAsync(new DssUnitIndexEntry
            {
                DssUnitId = dssUnitId,
                UnitName = state.UnitName,
                UnitCode = state.UnitCode,
                DivisionName = state.DivisionName,
                PrimaryStopCode = state.PrimaryStopCode,
                IsActive = true,
            });

            return Created(
                $"api/eventcapture/dss-units/{dssUnitId}",
                new CreateDssUnitResponse(dssUnitId, "DSS unit created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating DSS unit {UnitName}", request.UnitName);
            return StatusCode(500, "An error occurred while creating the DSS unit");
        }
    }

    /// <summary>Deactivate a DSS unit.</summary>
    [HttpPost("api/eventcapture/dss-units/{dssUnitId}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateDssUnit(string dssUnitId)
    {
        try
        {
            await DssUnit(dssUnitId).DeactivateAsync();

            DssUnitState state = await DssUnit(dssUnitId).GetUnitAsync();
            await DssUnitIndex().AddOrUpdateAsync(new DssUnitIndexEntry
            {
                DssUnitId = dssUnitId,
                UnitName = state.UnitName,
                UnitCode = state.UnitCode,
                DivisionName = state.DivisionName,
                PrimaryStopCode = state.PrimaryStopCode,
                IsActive = false,
            });

            return Ok(new { DssUnitId = dssUnitId, Message = "DSS unit deactivated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating DSS unit {DssUnitId}", dssUnitId);
            return StatusCode(500, "An error occurred while deactivating the DSS unit");
        }
    }

    // ── Request / Response DTOs ───────────────────────────────────────────────

    public record CreateEncounterRequest(
        DateTime EncounterDateTime,
        string DssUnitId,
        string DssUnitName,
        string? DssUnitCode,
        string? ClinicId,
        string? ClinicName,
        string? LocationId,
        string? LocationName,
        string PrimaryProviderId,
        string PrimaryProviderName,
        string? AttendingProviderId,
        string? AttendingProviderName,
        EcEncounterType EncounterType,
        EcPatientCategory PatientCategory,
        string? PrimaryStopCode,
        string? CreditStopCode,
        string? Comments);

    public record CreateEncounterResponse(string EncounterId, string Message);

    public record AddProcedureRequest(
        string CptCode,
        string ProcedureDescription,
        int Quantity,
        string ProviderId,
        string ProviderName,
        string? ModifierCode);

    public record AddDiagnosisRequest(
        string Icd10Code,
        string Description,
        bool IsPrimary);

    public record CompleteEncounterRequest(
        DateTime CheckOutDateTime,
        int? VisitLengthMinutes);

    public record DeleteEncounterRequest(
        string DeletedByProviderId,
        string DeletedByProviderName,
        string? Reason);

    public record CreateDssUnitRequest(
        string UnitName,
        string UnitCode,
        string? DivisionId,
        string? DivisionName,
        string? PrimaryStopCode,
        string? CreditStopCode,
        string? TreatmentCode,
        string? Description);

    public record CreateDssUnitResponse(string DssUnitId, string Message);
}
