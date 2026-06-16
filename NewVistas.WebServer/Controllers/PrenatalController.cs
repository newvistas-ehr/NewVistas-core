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
/// Prenatal / OB API — IHS Prenatal Care Module (File #90680.01) and
/// RPMS Women's Health pregnancy tracking (BJPNAPI.m, BWGRVL.m).
///
/// Covers pregnancy records, prenatal visits, risk assessment,
/// problem tracking, delivery, and postpartum follow-up.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/prenatal")]
[Produces("application/json")]
public class PrenatalController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PrenatalController> _logger;

    public PrenatalController(IGrainFactory grainFactory, ILogger<PrenatalController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Pregnancy Records ───────────────────────────────────────────────────

    /// <summary>List all pregnancies for a patient (newest first).</summary>
    [HttpGet("pregnancies")]
    [ProducesResponseType(typeof(List<PregnancyIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PregnancyIndexEntry>>> GetPregnancies(string patientId)
    {
        try
        {
            List<PregnancyIndexEntry> results = await GetWorkflow(patientId).GetPregnanciesAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pregnancies for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving pregnancies");
        }
    }

    /// <summary>Get the active (ongoing) pregnancy, if any.</summary>
    [HttpGet("pregnancies/active")]
    [ProducesResponseType(typeof(PregnancyIndexEntry), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PregnancyIndexEntry>> GetActivePregnancy(string patientId)
    {
        try
        {
            PregnancyIndexEntry? entry = await GetWorkflow(patientId).GetActivePregnancyAsync();
            if (entry == null)
                return NotFound(new { Message = "No active pregnancy found" });
            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active pregnancy for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the active pregnancy");
        }
    }

    /// <summary>Get the full state of a single pregnancy.</summary>
    [HttpGet("pregnancies/{pregnancyId}")]
    [ProducesResponseType(typeof(PregnancyState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PregnancyState>> GetPregnancy(string patientId, string pregnancyId)
    {
        try
        {
            PregnancyState state = await GetWorkflow(patientId).GetPregnancyAsync(pregnancyId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Pregnancy '{pregnancyId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while retrieving the pregnancy");
        }
    }

    /// <summary>Create a new pregnancy record.</summary>
    [HttpPost("pregnancies")]
    [ProducesResponseType(typeof(PrenatalResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePregnancy(
        string patientId,
        [FromBody] CreatePregnancyRequest request)
    {
        try
        {
            string pregnancyId = await GetWorkflow(patientId).CreatePregnancyAsync(
                request.LastMenstrualPeriod, request.EddByLmp, request.EddByUltrasound,
                request.DefinitiveEdd,
                request.Gravida, request.Para, request.Abortions, request.Living,
                request.RiskLevel, request.RiskFactors,
                request.ProviderId, request.ProviderName,
                request.LocationId, request.LocationName,
                request.Notes);

            _logger.LogInformation(
                "Created pregnancy {PregnancyId} (G{Gravida}P{Para}) for patient {PatientId}",
                pregnancyId, request.Gravida, request.Para, patientId);

            return Created(
                $"api/patient/{patientId}/prenatal/pregnancies/{pregnancyId}",
                new PrenatalResponse { Id = pregnancyId, Message = "Pregnancy created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pregnancy for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the pregnancy");
        }
    }

    /// <summary>Update risk assessment for a pregnancy.</summary>
    [HttpPost("pregnancies/{pregnancyId}/risk")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRisk(
        string patientId, string pregnancyId,
        [FromBody] UpdatePregnancyRiskRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdatePregnancyRiskAsync(
                pregnancyId, request.RiskLevel, request.RiskFactors);
            return Ok(new { PregnancyId = pregnancyId, Message = "Risk assessment updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating risk for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while updating risk assessment");
        }
    }

    /// <summary>Add a prenatal problem to a pregnancy.</summary>
    [HttpPost("pregnancies/{pregnancyId}/problems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddProblem(
        string patientId, string pregnancyId,
        [FromBody] AddPrenatalProblemRequest request)
    {
        try
        {
            PrenatalProblemEntry problem = new()
            {
                ProblemId   = $"PROB-{Guid.NewGuid():N}",
                Description = request.Description,
                Priority    = request.Priority,
                Scope       = request.Scope,
                IsActive    = true,
                Notes       = request.Notes,
                EntryDate   = DateTime.UtcNow,
            };
            await GetWorkflow(patientId).AddPrenatalProblemAsync(pregnancyId, problem);
            return Ok(new { ProblemId = problem.ProblemId, Message = "Problem added" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding problem to pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while adding the problem");
        }
    }

    /// <summary>Resolve a prenatal problem.</summary>
    [HttpPost("pregnancies/{pregnancyId}/problems/{problemId}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveProblem(
        string patientId, string pregnancyId, string problemId)
    {
        try
        {
            await GetWorkflow(patientId).ResolvePrenatalProblemAsync(pregnancyId, problemId);
            return Ok(new { ProblemId = problemId, Message = "Problem resolved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving problem {ProblemId} on pregnancy {PregnancyId}", problemId, pregnancyId);
            return StatusCode(500, "An error occurred while resolving the problem");
        }
    }

    /// <summary>Record delivery information.</summary>
    [HttpPost("pregnancies/{pregnancyId}/delivery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordDelivery(
        string patientId, string pregnancyId,
        [FromBody] RecordDeliveryRequest request)
    {
        try
        {
            DeliveryInfo delivery = new()
            {
                DeliveryDate                  = request.DeliveryDate,
                DeliveryMethod                = request.DeliveryMethod,
                GestationalAgeAtDeliveryWeeks = request.GestationalAgeAtDeliveryWeeks,
                BirthWeightGrams              = request.BirthWeightGrams,
                Apgar1Min                     = request.Apgar1Min,
                Apgar5Min                     = request.Apgar5Min,
                Presentation                  = request.Presentation,
                AnesthesiaType                = request.AnesthesiaType,
                PerinealStatus                = request.PerinealStatus,
                EstimatedBloodLossMl          = request.EstimatedBloodLossMl,
                PlacentaDelivery              = request.PlacentaDelivery,
                InfantSex                     = request.InfantSex,
                Complications                 = request.Complications,
                Notes                         = request.Notes,
            };
            await GetWorkflow(patientId).RecordDeliveryAsync(pregnancyId, delivery, request.Outcome);
            return Ok(new { PregnancyId = pregnancyId, Message = "Delivery recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording delivery for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while recording delivery");
        }
    }

    /// <summary>Record postpartum follow-up.</summary>
    [HttpPost("pregnancies/{pregnancyId}/postpartum")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPostpartum(
        string patientId, string pregnancyId,
        [FromBody] RecordPostpartumRequest request)
    {
        try
        {
            PostpartumInfo postpartum = new()
            {
                PostpartumVisitDate      = request.PostpartumVisitDate,
                BreastfeedingStatus      = request.BreastfeedingStatus,
                ContraceptiveMethod      = request.ContraceptiveMethod,
                DepressionScreeningResult = request.DepressionScreeningResult,
                EpdsScore                = request.EpdsScore,
                Complications            = request.Complications,
                Notes                    = request.Notes,
            };
            await GetWorkflow(patientId).RecordPostpartumAsync(pregnancyId, postpartum);
            return Ok(new { PregnancyId = pregnancyId, Message = "Postpartum recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording postpartum for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while recording postpartum");
        }
    }

    /// <summary>Update pregnancy status.</summary>
    [HttpPost("pregnancies/{pregnancyId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        string patientId, string pregnancyId,
        [FromBody] UpdatePregnancyStatusRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdatePregnancyStatusAsync(pregnancyId, request.Status);
            return Ok(new { PregnancyId = pregnancyId, Message = "Status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while updating the status");
        }
    }

    /// <summary>Update definitive EDD.</summary>
    [HttpPost("pregnancies/{pregnancyId}/edd")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEdd(
        string patientId, string pregnancyId,
        [FromBody] UpdateEddRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdatePregnancyEddAsync(
                pregnancyId, request.EddByUltrasound, request.DefinitiveEdd);
            return Ok(new { PregnancyId = pregnancyId, Message = "EDD updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EDD for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while updating the EDD");
        }
    }

    // ── Prenatal Visits ─────────────────────────────────────────────────────

    /// <summary>List all prenatal visits for a pregnancy (newest first).</summary>
    [HttpGet("pregnancies/{pregnancyId}/visits")]
    [ProducesResponseType(typeof(List<PrenatalVisitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PrenatalVisitIndexEntry>>> GetVisits(
        string patientId, string pregnancyId)
    {
        try
        {
            List<PrenatalVisitIndexEntry> results =
                await GetWorkflow(patientId).GetPrenatalVisitsAsync(pregnancyId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prenatal visits for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while retrieving visits");
        }
    }

    /// <summary>Get the full state of a single prenatal visit.</summary>
    [HttpGet("visits/{visitId}")]
    [ProducesResponseType(typeof(PrenatalVisitState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PrenatalVisitState>> GetVisit(
        string patientId, string visitId)
    {
        try
        {
            PrenatalVisitState state = await GetWorkflow(patientId).GetPrenatalVisitAsync(visitId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"Visit '{visitId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prenatal visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred while retrieving the visit");
        }
    }

    /// <summary>Create a new prenatal visit.</summary>
    [HttpPost("pregnancies/{pregnancyId}/visits")]
    [ProducesResponseType(typeof(PrenatalResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVisit(
        string patientId, string pregnancyId,
        [FromBody] CreatePrenatalVisitRequest request)
    {
        try
        {
            string visitId = await GetWorkflow(patientId).CreatePrenatalVisitAsync(
                pregnancyId, request.VisitDate,
                request.GestationalAgeWeeks, request.GestationalAgeDays,
                request.Weight,
                request.BloodPressureSystolic, request.BloodPressureDiastolic,
                request.FundalHeightCm, request.FetalHeartRate,
                request.FetalPresentation, request.FetalMovement,
                request.UrineProtein, request.UrineGlucose, request.Edema,
                request.CervicalDilationCm, request.CervicalEffacementPercent, request.FetalStation,
                request.ProviderId, request.ProviderName,
                request.Notes, request.NextVisitDate);

            _logger.LogInformation(
                "Created prenatal visit {VisitId} for pregnancy {PregnancyId}",
                visitId, pregnancyId);

            return Created(
                $"api/patient/{patientId}/prenatal/visits/{visitId}",
                new PrenatalResponse { Id = visitId, Message = "Prenatal visit created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating prenatal visit for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while creating the visit");
        }
    }

    /// <summary>Get the visit count for a pregnancy.</summary>
    [HttpGet("pregnancies/{pregnancyId}/visits/count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetVisitCount(string patientId, string pregnancyId)
    {
        try
        {
            int count = await GetWorkflow(patientId).GetPrenatalVisitCountAsync(pregnancyId);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visit count for pregnancy {PregnancyId}", pregnancyId);
            return StatusCode(500, "An error occurred while retrieving the visit count");
        }
    }
}

// ── Request / Response DTOs ─────────────────────────────────────────────────

public record CreatePregnancyRequest
{
    public DateTime? LastMenstrualPeriod { get; init; }
    public DateTime? EddByLmp { get; init; }
    public DateTime? EddByUltrasound { get; init; }
    public DateTime DefinitiveEdd { get; init; }
    public int Gravida { get; init; }
    public int Para { get; init; }
    public int Abortions { get; init; }
    public int Living { get; init; }
    public PregnancyRiskLevel RiskLevel { get; init; }
    public List<string>? RiskFactors { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? Notes { get; init; }
}

public record UpdatePregnancyRiskRequest
{
    public PregnancyRiskLevel RiskLevel { get; init; }
    public List<string> RiskFactors { get; init; } = new();
}

public record AddPrenatalProblemRequest
{
    public string Description { get; init; } = string.Empty;
    public PrenatalProblemPriority Priority { get; init; }
    public PrenatalProblemScope Scope { get; init; }
    public string? Notes { get; init; }
}

public record RecordDeliveryRequest
{
    public DateTime? DeliveryDate { get; init; }
    public DeliveryMethod DeliveryMethod { get; init; }
    public int? GestationalAgeAtDeliveryWeeks { get; init; }
    public int? BirthWeightGrams { get; init; }
    public int? Apgar1Min { get; init; }
    public int? Apgar5Min { get; init; }
    public FetalPresentation Presentation { get; init; }
    public string? AnesthesiaType { get; init; }
    public string? PerinealStatus { get; init; }
    public int? EstimatedBloodLossMl { get; init; }
    public string? PlacentaDelivery { get; init; }
    public string? InfantSex { get; init; }
    public string? Complications { get; init; }
    public string? Notes { get; init; }
    public PregnancyOutcome Outcome { get; init; }
}

public record RecordPostpartumRequest
{
    public DateTime? PostpartumVisitDate { get; init; }
    public string? BreastfeedingStatus { get; init; }
    public string? ContraceptiveMethod { get; init; }
    public string? DepressionScreeningResult { get; init; }
    public int? EpdsScore { get; init; }
    public string? Complications { get; init; }
    public string? Notes { get; init; }
}

public record UpdatePregnancyStatusRequest
{
    public PregnancyStatus Status { get; init; }
}

public record UpdateEddRequest
{
    public DateTime? EddByUltrasound { get; init; }
    public DateTime DefinitiveEdd { get; init; }
}

public record CreatePrenatalVisitRequest
{
    public DateTime VisitDate { get; init; }
    public int GestationalAgeWeeks { get; init; }
    public int GestationalAgeDays { get; init; }
    public decimal? Weight { get; init; }
    public int? BloodPressureSystolic { get; init; }
    public int? BloodPressureDiastolic { get; init; }
    public decimal? FundalHeightCm { get; init; }
    public int? FetalHeartRate { get; init; }
    public FetalPresentation FetalPresentation { get; init; }
    public bool? FetalMovement { get; init; }
    public string? UrineProtein { get; init; }
    public string? UrineGlucose { get; init; }
    public string? Edema { get; init; }
    public decimal? CervicalDilationCm { get; init; }
    public int? CervicalEffacementPercent { get; init; }
    public int? FetalStation { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? Notes { get; init; }
    public DateTime? NextVisitDate { get; init; }
}

public class PrenatalResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
