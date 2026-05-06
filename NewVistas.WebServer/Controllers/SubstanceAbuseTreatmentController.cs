// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Substance Abuse Treatment API — RPMS CDMIS (Chemical Dependency MIS)
/// File #9002170-9002174.
///
/// Covers treatment episodes, MAT (Medication-Assisted Treatment),
/// treatment visits, UDS tracking, and discharge management.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/satreatment")]
[Produces("application/json")]
public class SubstanceAbuseTreatmentController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SubstanceAbuseTreatmentController> _logger;

    public SubstanceAbuseTreatmentController(IGrainFactory grainFactory, ILogger<SubstanceAbuseTreatmentController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Treatment Episodes ───────────────────────────────────────────────────

    /// <summary>List all treatment episodes for a patient (newest first).</summary>
    [HttpGet("episodes")]
    [ProducesResponseType(typeof(List<SATreatmentEpisodeIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SATreatmentEpisodeIndexEntry>>> GetEpisodes(string patientId)
    {
        try
        {
            List<SATreatmentEpisodeIndexEntry> results = await GetWorkflow(patientId).GetSATreatmentEpisodesAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SA treatment episodes for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving treatment episodes");
        }
    }

    /// <summary>Get the active (ongoing) treatment episode, if any.</summary>
    [HttpGet("episodes/active")]
    [ProducesResponseType(typeof(SATreatmentEpisodeIndexEntry), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SATreatmentEpisodeIndexEntry>> GetActiveEpisode(string patientId)
    {
        try
        {
            SATreatmentEpisodeIndexEntry? entry = await GetWorkflow(patientId).GetActiveSATreatmentAsync();
            if (entry == null)
                return NotFound(new { Message = "No active treatment episode found" });
            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active SA treatment episode for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the active treatment episode");
        }
    }

    /// <summary>Get the full state of a single treatment episode.</summary>
    [HttpGet("episodes/{episodeId}")]
    [ProducesResponseType(typeof(SATreatmentEpisodeState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SATreatmentEpisodeState>> GetEpisode(string patientId, string episodeId)
    {
        try
        {
            SATreatmentEpisodeState state = await GetWorkflow(patientId).GetSATreatmentEpisodeAsync(episodeId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Treatment episode '{episodeId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SA treatment episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while retrieving the treatment episode");
        }
    }

    /// <summary>Create a new treatment episode.</summary>
    [HttpPost("episodes")]
    [ProducesResponseType(typeof(SATreatmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEpisode(
        string patientId,
        [FromBody] CreateSATreatmentEpisodeRequest request)
    {
        try
        {
            string episodeId = await GetWorkflow(patientId).CreateSATreatmentEpisodeAsync(
                request.Modality, request.PrimarySubstance,
                request.SecondarySubstances,
                request.IntakeDate,
                request.LastUseDate, request.SobrietyDate,
                request.ProgramName, request.TreatmentGoals,
                request.ProviderId, request.ProviderName,
                request.LocationId, request.LocationName,
                request.Notes);

            _logger.LogInformation(
                "Created SA treatment episode {EpisodeId} ({Modality}/{Substance}) for patient {PatientId}",
                episodeId, request.Modality, request.PrimarySubstance, patientId);

            return Created(
                $"api/patient/{patientId}/satreatment/episodes/{episodeId}",
                new SATreatmentResponse { Id = episodeId, Message = "Treatment episode created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SA treatment episode for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the treatment episode");
        }
    }

    // ── MAT (Medication-Assisted Treatment) ──────────────────────────────────

    /// <summary>Add a MAT entry to a treatment episode.</summary>
    [HttpPost("episodes/{episodeId}/mat")]
    [ProducesResponseType(typeof(SATreatmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMATEntry(
        string patientId, string episodeId,
        [FromBody] AddMATRequest request)
    {
        try
        {
            MATEntry entry = new()
            {
                EntryId = $"MAT-{Guid.NewGuid():N}",
                Medication = request.Medication,
                Dosage = request.Dosage,
                StartDate = request.StartDate,
                PrescriberId = request.PrescriberId,
                PrescriberName = request.PrescriberName,
                IsActive = true,
                Notes = request.Notes,
            };
            await GetWorkflow(patientId).AddSAMATEntryAsync(episodeId, entry);

            _logger.LogInformation(
                "Added MAT entry {EntryId} ({Medication}) to episode {EpisodeId}",
                entry.EntryId, request.Medication, episodeId);

            return Created(
                $"api/patient/{patientId}/satreatment/episodes/{episodeId}",
                new SATreatmentResponse { Id = entry.EntryId, Message = "MAT entry added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding MAT entry to episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while adding the MAT entry");
        }
    }

    /// <summary>Stop a MAT entry (set end date).</summary>
    [HttpPost("episodes/{episodeId}/mat/{entryId}/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> StopMATEntry(
        string patientId, string episodeId, string entryId,
        [FromBody] StopMATRequest request)
    {
        try
        {
            await GetWorkflow(patientId).StopSAMATEntryAsync(episodeId, entryId, request.EndDate);
            return Ok(new { EntryId = entryId, Message = "MAT entry stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MAT entry {EntryId} on episode {EpisodeId}", entryId, episodeId);
            return StatusCode(500, "An error occurred while stopping the MAT entry");
        }
    }

    // ── Treatment Goals ──────────────────────────────────────────────────────

    /// <summary>Add a treatment goal to an episode.</summary>
    [HttpPost("episodes/{episodeId}/goals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddGoal(
        string patientId, string episodeId,
        [FromBody] AddGoalRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AddSATreatmentGoalAsync(episodeId, request.Goal);
            return Ok(new { EpisodeId = episodeId, Message = "Treatment goal added" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding treatment goal to episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while adding the treatment goal");
        }
    }

    // ── Discharge / Reopen ───────────────────────────────────────────────────

    /// <summary>Discharge a treatment episode.</summary>
    [HttpPost("episodes/{episodeId}/discharge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DischargeEpisode(
        string patientId, string episodeId,
        [FromBody] DischargeRequest request)
    {
        try
        {
            await GetWorkflow(patientId).DischargeSATreatmentAsync(
                episodeId, request.DischargeDate, request.Disposition, request.Notes);
            return Ok(new { EpisodeId = episodeId, Message = "Treatment episode discharged" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging SA treatment episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while discharging the treatment episode");
        }
    }

    /// <summary>Reopen a discharged treatment episode.</summary>
    [HttpPost("episodes/{episodeId}/reopen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReopenEpisode(
        string patientId, string episodeId,
        [FromBody] ReopenRequest request)
    {
        try
        {
            await GetWorkflow(patientId).ReopenSATreatmentAsync(episodeId, request.Notes);
            return Ok(new { EpisodeId = episodeId, Message = "Treatment episode reopened" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reopening SA treatment episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while reopening the treatment episode");
        }
    }

    // ── Treatment Visits ─────────────────────────────────────────────────────

    /// <summary>List all visits for a treatment episode (newest first).</summary>
    [HttpGet("episodes/{episodeId}/visits")]
    [ProducesResponseType(typeof(List<SAVisitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SAVisitIndexEntry>>> GetVisits(
        string patientId, string episodeId)
    {
        try
        {
            List<SAVisitIndexEntry> results =
                await GetWorkflow(patientId).GetSAVisitsAsync(episodeId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SA visits for episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while retrieving visits");
        }
    }

    /// <summary>Get the full state of a single treatment visit.</summary>
    [HttpGet("visits/{visitId}")]
    [ProducesResponseType(typeof(SAVisitState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SAVisitState>> GetVisit(
        string patientId, string visitId)
    {
        try
        {
            SAVisitState state = await GetWorkflow(patientId).GetSAVisitAsync(visitId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Visit '{visitId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SA visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred while retrieving the visit");
        }
    }

    /// <summary>Create a new treatment visit.</summary>
    [HttpPost("episodes/{episodeId}/visits")]
    [ProducesResponseType(typeof(SATreatmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVisit(
        string patientId, string episodeId,
        [FromBody] CreateSAVisitRequest request)
    {
        try
        {
            string visitId = await GetWorkflow(patientId).CreateSAVisitAsync(
                episodeId, request.VisitDate,
                request.VisitType, request.DurationMinutes,
                request.UdsResult, request.UdsSubstancesDetected,
                request.DaysSinceLastUse, request.CravingLevel,
                request.ProviderId, request.ProviderName,
                request.Notes);

            _logger.LogInformation(
                "Created SA visit {VisitId} for episode {EpisodeId}",
                visitId, episodeId);

            return Created(
                $"api/patient/{patientId}/satreatment/visits/{visitId}",
                new SATreatmentResponse { Id = visitId, Message = "Treatment visit created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SA visit for episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while creating the visit");
        }
    }

    /// <summary>Get the visit count for a treatment episode.</summary>
    [HttpGet("episodes/{episodeId}/visits/count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetVisitCount(string patientId, string episodeId)
    {
        try
        {
            int count = await GetWorkflow(patientId).GetSAVisitCountAsync(episodeId);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visit count for episode {EpisodeId}", episodeId);
            return StatusCode(500, "An error occurred while retrieving the visit count");
        }
    }
}

// ── Request / Response DTOs ─────────────────────────────────────────────────

public record CreateSATreatmentEpisodeRequest
{
    public SATreatmentModality Modality { get; init; }
    public SubstanceType PrimarySubstance { get; init; }
    public List<SubstanceType>? SecondarySubstances { get; init; }
    public DateTime IntakeDate { get; init; }
    public DateTime? LastUseDate { get; init; }
    public DateTime? SobrietyDate { get; init; }
    public string? ProgramName { get; init; }
    public List<string>? TreatmentGoals { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? Notes { get; init; }
}

public record AddMATRequest
{
    public MATMedication Medication { get; init; }
    public string? Dosage { get; init; }
    public DateTime StartDate { get; init; }
    public string? PrescriberId { get; init; }
    public string? PrescriberName { get; init; }
    public string? Notes { get; init; }
}

public record StopMATRequest
{
    public DateTime EndDate { get; init; }
}

public record AddGoalRequest
{
    public string Goal { get; init; } = string.Empty;
}

public record DischargeRequest
{
    public DateTime DischargeDate { get; init; }
    public SADischargeDisposition Disposition { get; init; }
    public string? Notes { get; init; }
}

public record ReopenRequest
{
    public string? Notes { get; init; }
}

public record CreateSAVisitRequest
{
    public DateTime VisitDate { get; init; }
    public SAVisitType VisitType { get; init; }
    public int? DurationMinutes { get; init; }
    public string? UdsResult { get; init; }
    public List<string>? UdsSubstancesDetected { get; init; }
    public int? DaysSinceLastUse { get; init; }
    public int? CravingLevel { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? Notes { get; init; }
}

public class SATreatmentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
