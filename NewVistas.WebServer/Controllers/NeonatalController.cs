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
/// Neonatal / Newborn Nursery API — extends the OB module (NEONATAL_CARE).
///
/// Patient-scoped operations (a newborn's neonatal chart) route through
/// <see cref="IPatientWorkflowGrain"/> keyed by the <b>mother's</b> patient id — the newborn is
/// registered from one of the mother's pregnancies and linked back to it (supports multiples).
/// Because the newborn ids are global but the workflow grain is keyed by the mother, every
/// patient-scoped route is prefixed with <c>{motherPatientId}/</c> so a workflow grain is always
/// in hand. The facility-wide nursery census is NOT on the workflow grain and calls the singleton
/// <see cref="INewbornNurseryGrain"/> directly.
///
/// Writes and reads are open (matching the OB module this extends).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NeonatalController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<NeonatalController> _logger;

    public NeonatalController(IGrainFactory grainFactory, ILogger<NeonatalController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private INewbornNurseryGrain GetNursery()
        => _grainFactory.GetGrain<INewbornNurseryGrain>("NEONATE-NURSERY:DEFAULT");

    // ─── Newborn Registration / Reads ────────────────────────────────────────

    /// <summary>Registers a newborn delivered from one of the mother's pregnancies. Returns the newborn id.</summary>
    [HttpPost("{motherPatientId}/pregnancies/{pregnancyId}/newborns")]
    [ProducesResponseType(typeof(RegisterNewbornResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterNewbornResponse>> RegisterNewborn(
        string motherPatientId, string pregnancyId, [FromBody] RegisterNewbornRequest request)
    {
        try
        {
            string newbornId = await GetWorkflow(motherPatientId).RegisterNewbornFromDeliveryAsync(
                pregnancyId,
                request.Name,
                request.Sex,
                request.BirthDateTime,
                request.GestationalAgeWeeks,
                request.GestationalAgeDays,
                request.DeliveryMethod,
                request.BirthWeightGrams,
                request.LengthCm,
                request.HeadCircumferenceCm,
                request.Apgar1Min,
                request.Apgar5Min,
                request.Apgar10Min,
                request.MultipleBirthOrder,
                request.MultipleBirthTotal,
                request.AttendingProviderId,
                request.AttendingProviderName,
                request.BirthLocationName);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}",
                new RegisterNewbornResponse { NewbornId = newbornId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering newborn from pregnancy {PregnancyId} for mother {MotherPatientId}",
                pregnancyId, motherPatientId);
            return StatusCode(500, "An error occurred while registering the newborn");
        }
    }

    /// <summary>Returns all newborns delivered from this mother's pregnancies (newest first).</summary>
    [HttpGet("{motherPatientId}/newborns")]
    [ProducesResponseType(typeof(List<NewbornState>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NewbornState>>> GetNewbornsForMother(string motherPatientId)
    {
        try
        {
            List<NewbornState> newborns = await GetWorkflow(motherPatientId).GetNewbornsForMotherAsync();
            return Ok(newborns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving newborns for mother {MotherPatientId}", motherPatientId);
            return StatusCode(500, "An error occurred while retrieving the newborns");
        }
    }

    /// <summary>Returns the newborns delivered from a given pregnancy.</summary>
    [HttpGet("{motherPatientId}/pregnancies/{pregnancyId}/newborns")]
    [ProducesResponseType(typeof(List<NewbornState>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NewbornState>>> GetNewbornsForPregnancy(
        string motherPatientId, string pregnancyId)
    {
        try
        {
            List<NewbornState> newborns = await GetWorkflow(motherPatientId).GetNewbornsForPregnancyAsync(pregnancyId);
            return Ok(newborns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving newborns for pregnancy {PregnancyId} for mother {MotherPatientId}",
                pregnancyId, motherPatientId);
            return StatusCode(500, "An error occurred while retrieving the newborns for the pregnancy");
        }
    }

    /// <summary>Returns a single newborn record.</summary>
    [HttpGet("{motherPatientId}/newborns/{newbornId}")]
    [ProducesResponseType(typeof(NewbornState), StatusCodes.Status200OK)]
    public async Task<ActionResult<NewbornState>> GetNewborn(string motherPatientId, string newbornId)
    {
        try
        {
            NewbornState newborn = await GetWorkflow(motherPatientId).GetNewbornAsync(newbornId);
            return Ok(newborn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while retrieving the newborn");
        }
    }

    // ─── Exam / Screening / Measurements ─────────────────────────────────────

    /// <summary>Records the newborn physical examination.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/exam")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordExam(
        string motherPatientId, string newbornId, [FromBody] RecordNewbornExamRequest request)
    {
        try
        {
            var exam = new NewbornExam
            {
                General = request.General,
                Heent = request.Heent,
                Cardiac = request.Cardiac,
                Respiratory = request.Respiratory,
                Abdomen = request.Abdomen,
                Genitourinary = request.Genitourinary,
                Musculoskeletal = request.Musculoskeletal,
                Neurologic = request.Neurologic,
                Skin = request.Skin,
                Impression = request.Impression,
                ExaminerName = request.ExaminerName,
                ExamDate = request.ExamDate
            };
            await GetWorkflow(motherPatientId).RecordNewbornExamAsync(newbornId, exam);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording exam for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while recording the newborn exam");
        }
    }

    /// <summary>Records a newborn screen result.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/screenings")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordScreening(
        string motherPatientId, string newbornId, [FromBody] RecordNewbornScreeningRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).RecordNewbornScreeningAsync(
                newbornId, request.ScreeningType, request.Result, request.ValueText,
                request.PerformedDate, request.PerformedBy, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording screening for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while recording the newborn screening");
        }
    }

    /// <summary>Adds an interval measurement (daily weight, feeding, bilirubin) during the birth stay.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/measurements")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMeasurement(
        string motherPatientId, string newbornId, [FromBody] AddNewbornMeasurementRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).AddNewbornMeasurementAsync(
                newbornId, request.MeasuredAt, request.WeightGrams, request.FeedingType,
                request.BilirubinMgDl, request.FeedingNotes, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding measurement for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while adding the newborn measurement");
        }
    }

    // ─── Nursery Level / Transfer / Discharge ────────────────────────────────

    /// <summary>Sets the nursery level of care (AAP levels I–IV) for the newborn.</summary>
    [HttpPut("{motherPatientId}/newborns/{newbornId}/nursery-level")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetNurseryLevel(
        string motherPatientId, string newbornId, [FromBody] SetNurseryLevelRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).SetNewbornNurseryLevelAsync(newbornId, request.Level, request.Reason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting nursery level for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while setting the nursery level");
        }
    }

    /// <summary>Transfers the newborn to another location (e.g. a higher level of care).</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TransferNewborn(
        string motherPatientId, string newbornId, [FromBody] TransferNewbornRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).TransferNewbornAsync(newbornId, request.ToLocation, request.Reason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while transferring the newborn");
        }
    }

    /// <summary>Discharges the newborn from the nursery.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/discharge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DischargeNewborn(
        string motherPatientId, string newbornId, [FromBody] DischargeNewbornRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).DischargeNewbornAsync(
                newbornId, request.DischargeDateTime, request.DischargeWeightGrams,
                request.DischargeFeeding, request.Disposition, request.FollowUpPlan, request.CarSeatTestPassed);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while discharging the newborn");
        }
    }

    // ─── NICU depth (Phase 2): respiratory / phototherapy / problems / nutrition / procedures ──

    /// <summary>Records a respiratory-support change for a newborn (closes the prior open episode).</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/respiratory-support")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordRespiratorySupport(
        string motherPatientId, string newbornId, [FromBody] NewbornRespiratorySupportRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).RecordNewbornRespiratorySupportAsync(
                newbornId, request.SupportType, request.FiO2Percent, request.Settings,
                request.RecordedAt ?? DateTime.UtcNow, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording respiratory support for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while recording the respiratory support");
        }
    }

    /// <summary>Starts a phototherapy episode for hyperbilirubinemia.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/phototherapy/start")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> StartPhototherapy(
        string motherPatientId, string newbornId, [FromBody] StartNewbornPhototherapyRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).StartNewbornPhototherapyAsync(
                newbornId, request.Intensity, request.Indication, request.BilirubinAtStartMgDl,
                request.StartedAt ?? DateTime.UtcNow, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting phototherapy for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while starting the phototherapy");
        }
    }

    /// <summary>Ends the open phototherapy episode for a newborn.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/phototherapy/end")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> EndPhototherapy(
        string motherPatientId, string newbornId, [FromBody] EndNewbornPhototherapyRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).EndNewbornPhototherapyAsync(
                newbornId, request.EndedAt ?? DateTime.UtcNow, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending phototherapy for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while ending the phototherapy");
        }
    }

    /// <summary>Adds an active neonatal problem. Returns the problem id.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/problems")]
    [ProducesResponseType(typeof(NewbornProblemResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<NewbornProblemResponse>> AddProblem(
        string motherPatientId, string newbornId, [FromBody] AddNewbornProblemRequest request)
    {
        try
        {
            string problemId = await GetWorkflow(motherPatientId).AddNewbornProblemAsync(
                newbornId, request.Problem, request.Icd10Code, request.OnsetDate, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}",
                new NewbornProblemResponse { ProblemId = problemId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding problem for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while adding the newborn problem");
        }
    }

    /// <summary>Resolves an active neonatal problem.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/problems/{problemId}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolveProblem(string motherPatientId, string newbornId, string problemId)
    {
        try
        {
            await GetWorkflow(motherPatientId).ResolveNewbornProblemAsync(newbornId, problemId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving problem {ProblemId} for newborn {NewbornId} for mother {MotherPatientId}",
                problemId, newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while resolving the newborn problem");
        }
    }

    /// <summary>Records a neonatal nutrition / fluid entry.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/nutrition")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordNutrition(
        string motherPatientId, string newbornId, [FromBody] NewbornNutritionRequest request)
    {
        try
        {
            await GetWorkflow(motherPatientId).RecordNewbornNutritionAsync(
                newbornId, request.RecordedAt ?? DateTime.UtcNow, request.Route,
                request.TotalFluidMlPerKgPerDay, request.Detail, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording nutrition for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while recording the newborn nutrition");
        }
    }

    /// <summary>Records a NICU bedside procedure. Returns the procedure id.</summary>
    [HttpPost("{motherPatientId}/newborns/{newbornId}/procedures")]
    [ProducesResponseType(typeof(NewbornProcedureResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<NewbornProcedureResponse>> RecordProcedure(
        string motherPatientId, string newbornId, [FromBody] NewbornProcedureRequest request)
    {
        try
        {
            string procedureId = await GetWorkflow(motherPatientId).RecordNewbornProcedureAsync(
                newbornId, request.ProcedureType, request.PerformedAt ?? DateTime.UtcNow,
                request.PerformedBy, request.Notes);
            return Created($"/api/neonatal/{motherPatientId}/newborns/{newbornId}",
                new NewbornProcedureResponse { ProcedureId = procedureId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording procedure for newborn {NewbornId} for mother {MotherPatientId}",
                newbornId, motherPatientId);
            return StatusCode(500, "An error occurred while recording the newborn procedure");
        }
    }

    // ─── Facility-wide Nursery Census (singleton nursery grain) ──────────────

    /// <summary>
    /// Returns the nursery census/board. Returns the Active census by default;
    /// pass <c>?all=true</c> for every newborn (any status).
    /// </summary>
    [HttpGet("nursery")]
    [ProducesResponseType(typeof(List<NewbornNurseryEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NewbornNurseryEntry>>> GetNurseryCensus([FromQuery] bool all = false)
    {
        try
        {
            List<NewbornNurseryEntry> census = all
                ? await GetNursery().GetAllAsync()
                : await GetNursery().GetActiveAsync();
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nursery census (all={All})", all);
            return StatusCode(500, "An error occurred while retrieving the nursery census");
        }
    }

    /// <summary>Returns the nursery census filtered to a specific level of care.</summary>
    [HttpGet("nursery/level/{level}")]
    [ProducesResponseType(typeof(List<NewbornNurseryEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NewbornNurseryEntry>>> GetNurseryByLevel(NurseryLevelOfCare level)
    {
        try
        {
            List<NewbornNurseryEntry> census = await GetNursery().GetByLevelAsync(level);
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nursery census for level {Level}", level);
            return StatusCode(500, "An error occurred while retrieving the nursery census by level");
        }
    }

    /// <summary>Returns nursery census entries that still have universal screens pending (the nursery to-do).</summary>
    [HttpGet("nursery/pending-screens")]
    [ProducesResponseType(typeof(List<NewbornNurseryEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NewbornNurseryEntry>>> GetNurseryPendingScreens()
    {
        try
        {
            List<NewbornNurseryEntry> census = await GetNursery().GetWithPendingScreensAsync();
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nursery census with pending screens");
            return StatusCode(500, "An error occurred while retrieving the pending-screens worklist");
        }
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────────────

public record RegisterNewbornRequest
{
    public string Name { get; init; } = string.Empty;
    public NewbornSex Sex { get; init; }
    public DateTime BirthDateTime { get; init; }
    public int GestationalAgeWeeks { get; init; }
    public int GestationalAgeDays { get; init; }
    public DeliveryMethod DeliveryMethod { get; init; }
    public int? BirthWeightGrams { get; init; }
    public decimal? LengthCm { get; init; }
    public decimal? HeadCircumferenceCm { get; init; }
    public int? Apgar1Min { get; init; }
    public int? Apgar5Min { get; init; }
    public int? Apgar10Min { get; init; }
    public int MultipleBirthOrder { get; init; } = 1;
    public int MultipleBirthTotal { get; init; } = 1;
    public string AttendingProviderId { get; init; } = string.Empty;
    public string AttendingProviderName { get; init; } = string.Empty;
    public string BirthLocationName { get; init; } = string.Empty;
}

public record RegisterNewbornResponse
{
    public string NewbornId { get; init; } = string.Empty;
}

public record RecordNewbornExamRequest
{
    public string General { get; init; } = string.Empty;
    public string Heent { get; init; } = string.Empty;
    public string Cardiac { get; init; } = string.Empty;
    public string Respiratory { get; init; } = string.Empty;
    public string Abdomen { get; init; } = string.Empty;
    public string Genitourinary { get; init; } = string.Empty;
    public string Musculoskeletal { get; init; } = string.Empty;
    public string Neurologic { get; init; } = string.Empty;
    public string Skin { get; init; } = string.Empty;
    public string Impression { get; init; } = string.Empty;
    public string ExaminerName { get; init; } = string.Empty;
    public DateTime? ExamDate { get; init; }
}

public record RecordNewbornScreeningRequest
{
    public NewbornScreeningType ScreeningType { get; init; }
    public NewbornScreeningResult Result { get; init; }
    public string ValueText { get; init; } = string.Empty;
    public DateTime? PerformedDate { get; init; }
    public string PerformedBy { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public record AddNewbornMeasurementRequest
{
    public DateTime MeasuredAt { get; init; }
    public int? WeightGrams { get; init; }
    public NewbornFeedingType FeedingType { get; init; }
    public decimal? BilirubinMgDl { get; init; }
    public string FeedingNotes { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public record SetNurseryLevelRequest
{
    public NurseryLevelOfCare Level { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record TransferNewbornRequest
{
    public string ToLocation { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record DischargeNewbornRequest
{
    public DateTime DischargeDateTime { get; init; }
    public int? DischargeWeightGrams { get; init; }
    public NewbornFeedingType DischargeFeeding { get; init; }
    public string Disposition { get; init; } = string.Empty;
    public string FollowUpPlan { get; init; } = string.Empty;
    public bool CarSeatTestPassed { get; init; }
}

// ─── NICU depth (Phase 2) DTOs ───────────────────────────────────────────────

public record NewbornRespiratorySupportRequest
{
    public RespiratorySupportType SupportType { get; init; }
    public int? FiO2Percent { get; init; }
    public string Settings { get; init; } = string.Empty;
    public DateTime? RecordedAt { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public record StartNewbornPhototherapyRequest
{
    public PhototherapyIntensity Intensity { get; init; }
    public string Indication { get; init; } = string.Empty;
    public decimal? BilirubinAtStartMgDl { get; init; }
    public DateTime? StartedAt { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public record EndNewbornPhototherapyRequest
{
    public DateTime? EndedAt { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public record AddNewbornProblemRequest
{
    public string Problem { get; init; } = string.Empty;
    public string Icd10Code { get; init; } = string.Empty;
    public DateTime? OnsetDate { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public record NewbornProblemResponse
{
    public string ProblemId { get; init; } = string.Empty;
}

public record NewbornNutritionRequest
{
    public DateTime? RecordedAt { get; init; }
    public NeonatalNutritionRoute Route { get; init; }
    public int? TotalFluidMlPerKgPerDay { get; init; }
    public string Detail { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public record NewbornProcedureRequest
{
    public NeonatalProcedureType ProcedureType { get; init; }
    public DateTime? PerformedAt { get; init; }
    public string PerformedBy { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public record NewbornProcedureResponse
{
    public string ProcedureId { get; init; } = string.Empty;
}
