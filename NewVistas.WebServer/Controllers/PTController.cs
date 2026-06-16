// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Physical Therapy API — ROM and strength measurement workflows.
/// Each body group evaluation (cervical, shoulder, hand, etc.) is a discrete session.
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/pt")]
[Produces("application/json")]
public class PTController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PTController> _logger;

    public PTController(IGrainFactory grainFactory, ILogger<PTController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPTWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPTWorkflowGrain>(patientId);

    /// <summary>
    /// Returns which body groups have recorded PT data for this patient.
    /// </summary>
    [HttpGet("bodygroups")]
    [ProducesResponseType(typeof(List<BodyGroup>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBodyGroupsWithData(string patientId)
    {
        try
        {
            var groups = await GetWorkflow(patientId).GetBodyGroupsWithDataAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting body groups for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving body groups.");
        }
    }

    /// <summary>
    /// Returns the standard movements for a body group.
    /// </summary>
    [HttpGet("movements/{bodyGroup}")]
    [ProducesResponseType(typeof(List<Movement>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStandardMovements(string patientId, BodyGroup bodyGroup)
    {
        try
        {
            var movements = await GetWorkflow(patientId).GetStandardMovementsAsync(bodyGroup);
            return Ok(movements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movements for {BodyGroup}", bodyGroup);
            return StatusCode(500, "An error occurred while retrieving movements.");
        }
    }

    /// <summary>
    /// Gets the latest N sessions for a body group (default 2 for comparison).
    /// </summary>
    [HttpGet("sessions/{bodyGroup}/latest")]
    [ProducesResponseType(typeof(List<PTSessionState>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatestSessions(
        string patientId, BodyGroup bodyGroup, [FromQuery] int count = 2)
    {
        try
        {
            var sessions = await GetWorkflow(patientId).GetLatestSessionsAsync(bodyGroup, count);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest sessions for {PatientId}/{BodyGroup}", patientId, bodyGroup);
            return StatusCode(500, "An error occurred while retrieving sessions.");
        }
    }

    /// <summary>
    /// Gets session history for a body group with optional date range filtering.
    /// </summary>
    [HttpGet("sessions/{bodyGroup}/history")]
    [ProducesResponseType(typeof(List<PTSessionState>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessionHistory(
        string patientId, BodyGroup bodyGroup,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int maxCount = 50)
    {
        try
        {
            var sessions = await GetWorkflow(patientId).GetSessionHistoryAsync(bodyGroup, from, to, maxCount);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session history for {PatientId}/{BodyGroup}", patientId, bodyGroup);
            return StatusCode(500, "An error occurred while retrieving session history.");
        }
    }

    /// <summary>
    /// Records a complete body group session with ROM and strength measurements.
    /// </summary>
    [HttpPost("sessions/{bodyGroup}")]
    [ProducesResponseType(typeof(RecordSessionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordSession(
        string patientId, BodyGroup bodyGroup, [FromBody] RecordSessionRequest req)
    {
        try
        {
            var romMeasurements = req.RomMeasurements?.Select(r => new RomMeasurement
            {
                Movement = r.Movement,
                ActiveRom = r.ActiveRom,
                PassiveRom = r.PassiveRom,
                PainOnMotion = r.PainOnMotion,
                Comments = r.Comments
            }).ToList() ?? [];

            var strengthMeasurements = req.StrengthMeasurements?.Select(s =>
            {
                var parsed = BodyGroupDefinitions.ParseMmtGrade(s.Grade);
                return new StrengthMeasurement
                {
                    Movement = s.Movement,
                    Grade = parsed?.grade ?? 0m,
                    GradeDisplay = parsed?.display ?? s.Grade,
                    Comments = s.Comments
                };
            }).ToList() ?? [];

            string sessionKey;
            if (!string.IsNullOrEmpty(req.ReferralGrainKey))
            {
                sessionKey = await GetWorkflow(patientId).RecordBodyGroupSessionAsync(
                    bodyGroup,
                    req.SessionDate ?? DateTime.UtcNow,
                    req.TherapistId, req.TherapistName,
                    req.LocationId, req.LocationName,
                    req.Side,
                    romMeasurements,
                    strengthMeasurements,
                    req.Notes,
                    req.ReferralGrainKey);
            }
            else
            {
                sessionKey = await GetWorkflow(patientId).RecordBodyGroupSessionAsync(
                    bodyGroup,
                    req.SessionDate ?? DateTime.UtcNow,
                    req.TherapistId, req.TherapistName,
                    req.LocationId, req.LocationName,
                    req.Side,
                    romMeasurements,
                    strengthMeasurements,
                    req.Notes);
            }

            return Created("", new RecordSessionResponse { SessionKey = sessionKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording PT session for {PatientId}/{BodyGroup}", patientId, bodyGroup);
            return StatusCode(500, "An error occurred while recording the session.");
        }
    }

    /// <summary>
    /// Returns the normal ROM reference ranges for a body group.
    /// </summary>
    [HttpGet("reference/{bodyGroup}")]
    [ProducesResponseType(typeof(List<MovementReference>), StatusCodes.Status200OK)]
    public IActionResult GetReferenceRanges(BodyGroup bodyGroup)
    {
        try
        {
            var movements = BodyGroupDefinitions.GetMovements(bodyGroup);
            var refs = movements.Select(m => new MovementReference
            {
                Movement = m,
                NormalRom = BodyGroupDefinitions.GetNormalRomRange(bodyGroup, m)
            }).ToList();
            return Ok(refs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reference ranges for {BodyGroup}", bodyGroup);
            return StatusCode(500, "An error occurred while retrieving reference ranges.");
        }
    }

    // ── PT Goals ──────────────────────────────────────────────────────

    /// <summary>Adds a therapeutic goal for a body group.</summary>
    [HttpPost("goals/{bodyGroup}")]
    [ProducesResponseType(typeof(AddPTGoalResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddGoal(
        string patientId, BodyGroup bodyGroup, [FromBody] AddPTGoalRequest req)
    {
        try
        {
            string goalId = await GetWorkflow(patientId).AddGoalAsync(bodyGroup, new PTGoal
            {
                GoalType = req.GoalType,
                Movement = req.Movement,
                Side = req.Side,
                Description = req.Description ?? string.Empty,
                TargetValue = req.TargetValue,
                BaselineValue = req.BaselineValue,
                CurrentValue = req.BaselineValue,
                TargetDate = req.TargetDate,
                Notes = req.Notes
            });
            return Created("", new AddPTGoalResponse { GoalId = goalId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding PT goal for {PatientId}/{BodyGroup}", patientId, bodyGroup);
            return StatusCode(500, "An error occurred while adding the goal.");
        }
    }

    /// <summary>Gets all goals for a body group.</summary>
    [HttpGet("goals/{bodyGroup}")]
    [ProducesResponseType(typeof(List<PTGoal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoalsForBodyGroup(string patientId, BodyGroup bodyGroup)
    {
        try
        {
            var goals = await GetWorkflow(patientId).GetGoalsForBodyGroupAsync(bodyGroup);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting goals for {PatientId}/{BodyGroup}", patientId, bodyGroup);
            return StatusCode(500, "An error occurred while retrieving goals.");
        }
    }

    /// <summary>Gets all active goals across all body groups.</summary>
    [HttpGet("goals/active")]
    [ProducesResponseType(typeof(List<PTGoal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActiveGoals(string patientId)
    {
        try
        {
            var goals = await GetWorkflow(patientId).GetAllActiveGoalsAsync();
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active goals for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving active goals.");
        }
    }

    /// <summary>Updates an existing goal.</summary>
    [HttpPut("goals/{bodyGroup}/{goalId}")]
    public async Task<IActionResult> UpdateGoal(
        string patientId, BodyGroup bodyGroup, string goalId, [FromBody] UpdatePTGoalRequest req)
    {
        try
        {
            await GetWorkflow(patientId).UpdateGoalAsync(bodyGroup, goalId, req.Status, req.CurrentValue, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating goal {GoalId} for {PatientId}", goalId, patientId);
            return StatusCode(500, "An error occurred while updating the goal.");
        }
    }

    /// <summary>Records a progress entry for a goal.</summary>
    [HttpPost("goals/{bodyGroup}/{goalId}/progress")]
    public async Task<IActionResult> AddGoalProgress(
        string patientId, BodyGroup bodyGroup, string goalId, [FromBody] AddPTGoalProgressRequest req)
    {
        try
        {
            await GetWorkflow(patientId).AddGoalProgressAsync(bodyGroup, goalId, req.Value, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding progress for goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while recording goal progress.");
        }
    }

    // ── Clinic Exercises ──────────────────────────────────────────────

    /// <summary>Adds an exercise log entry to an existing session.</summary>
    [HttpPost("sessions/{sessionKey}/exercises")]
    public async Task<IActionResult> AddClinicExercise(
        string patientId, string sessionKey, [FromBody] AddClinicExerciseRequest req)
    {
        try
        {
            await GetWorkflow(patientId).AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
            {
                ExerciseName = req.ExerciseName,
                Description = req.Description,
                Category = req.Category,
                BodyGroup = req.BodyGroup,
                Movement = req.Movement,
                Sets = req.Sets,
                Reps = req.Reps,
                WeightLbs = req.WeightLbs,
                DurationSeconds = req.DurationSeconds,
                DistanceFeet = req.DistanceFeet,
                Notes = req.Notes
            });
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding exercise to session {SessionKey}", sessionKey);
            return StatusCode(500, "An error occurred while adding the exercise.");
        }
    }

    // ── PT Referrals ──────────────────────────────────────────────────

    /// <summary>Creates a PT referral for a patient.</summary>
    [HttpPost("referrals")]
    [ProducesResponseType(typeof(CreatePTReferralResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReferral(
        string patientId, [FromBody] CreatePTReferralRequest req)
    {
        try
        {
            string referralKey = await GetWorkflow(patientId).CreateReferralAsync(
                req.PatientName,
                req.ReferringProviderName, req.ReferringProviderId,
                req.ReferringProviderSpecialty, req.ReferringFacilityName,
                req.Diagnosis, req.DiagnosisCode, req.BodyGroups,
                req.ReasonForReferral, req.Precautions,
                req.AuthorizedVisits, req.AuthorizationExpirationDate,
                req.ReferralDate, req.ReceivedDate, req.Notes);
            return Created("", new CreatePTReferralResponse { ReferralKey = referralKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PT referral for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the referral.");
        }
    }

    /// <summary>Gets all referrals for a patient.</summary>
    [HttpGet("referrals")]
    [ProducesResponseType(typeof(List<PTReferralState>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferrals(string patientId)
    {
        try
        {
            var referrals = await GetWorkflow(patientId).GetAllReferralsAsync();
            return Ok(referrals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting referrals for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving referrals.");
        }
    }

    /// <summary>Gets active referrals only.</summary>
    [HttpGet("referrals/active")]
    [ProducesResponseType(typeof(List<PTReferralState>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveReferrals(string patientId)
    {
        try
        {
            var referrals = await GetWorkflow(patientId).GetActiveReferralsAsync();
            return Ok(referrals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active referrals for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving active referrals.");
        }
    }

    /// <summary>Gets a specific referral.</summary>
    [HttpGet("referrals/{referralKey}")]
    [ProducesResponseType(typeof(PTReferralState), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferral(string patientId, string referralKey)
    {
        try
        {
            var referral = await GetWorkflow(patientId).GetReferralAsync(referralKey);
            return Ok(referral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting referral {ReferralKey}", referralKey);
            return StatusCode(500, "An error occurred while retrieving the referral.");
        }
    }

    /// <summary>Updates referral status.</summary>
    [HttpPut("referrals/{referralKey}/status")]
    public async Task<IActionResult> UpdateReferralStatus(
        string patientId, string referralKey, [FromBody] UpdatePTReferralStatusRequest req)
    {
        try
        {
            await GetWorkflow(patientId).UpdateReferralStatusAsync(referralKey, req.Status, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating referral status {ReferralKey}", referralKey);
            return StatusCode(500, "An error occurred while updating the referral status.");
        }
    }

    /// <summary>Updates referral authorization.</summary>
    [HttpPut("referrals/{referralKey}/authorization")]
    public async Task<IActionResult> UpdateReferralAuthorization(
        string patientId, string referralKey, [FromBody] UpdatePTReferralAuthRequest req)
    {
        try
        {
            await GetWorkflow(patientId).UpdateReferralAuthorizationAsync(
                referralKey, req.AuthorizedVisits, req.ExpirationDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating referral authorization {ReferralKey}", referralKey);
            return StatusCode(500, "An error occurred while updating the referral authorization.");
        }
    }

    // ── Home Exercise Program ─────────────────────────────────────────

    /// <summary>Adds a home exercise prescription.</summary>
    [HttpPost("hep/prescriptions")]
    [ProducesResponseType(typeof(AddHepPrescriptionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddHepPrescription(
        string patientId, [FromBody] AddHepPrescriptionRequest req)
    {
        try
        {
            string id = await GetWorkflow(patientId).AddHepPrescriptionAsync(new HepPrescription
            {
                ExerciseName = req.ExerciseName,
                Instructions = req.Instructions ?? string.Empty,
                Frequency = req.Frequency ?? string.Empty,
                Sets = req.Sets,
                Reps = req.Reps,
                DurationSeconds = req.DurationSeconds,
                BodyGroup = req.BodyGroup,
                Movement = req.Movement,
                Side = req.Side,
                Category = req.Category,
                PrescribedBy = req.PrescribedBy ?? string.Empty,
                Notes = req.Notes
            });
            return Created("", new AddHepPrescriptionResponse { PrescriptionId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding HEP prescription for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding the prescription.");
        }
    }

    /// <summary>Gets active home exercise prescriptions.</summary>
    [HttpGet("hep/prescriptions/active")]
    [ProducesResponseType(typeof(List<HepPrescription>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveHepPrescriptions(string patientId)
    {
        try
        {
            var prescriptions = await GetWorkflow(patientId).GetActiveHepPrescriptionsAsync();
            return Ok(prescriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting HEP prescriptions for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving prescriptions.");
        }
    }

    /// <summary>Updates a home exercise prescription status.</summary>
    [HttpPut("hep/prescriptions/{prescriptionId}/status")]
    public async Task<IActionResult> UpdateHepPrescriptionStatus(
        string patientId, string prescriptionId, [FromBody] UpdateHepStatusRequest req)
    {
        try
        {
            await GetWorkflow(patientId).UpdateHepPrescriptionStatusAsync(prescriptionId, req.Status);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating HEP prescription {PrescriptionId}", prescriptionId);
            return StatusCode(500, "An error occurred while updating the prescription.");
        }
    }

    /// <summary>Logs completion of a home exercise.</summary>
    [HttpPost("hep/completions")]
    [ProducesResponseType(typeof(LogHepCompletionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> LogHepCompletion(
        string patientId, [FromBody] LogHepCompletionRequest req)
    {
        try
        {
            string logId = await GetWorkflow(patientId).LogHepCompletionAsync(new HepCompletionLog
            {
                PrescriptionId = req.PrescriptionId,
                CompletedDate = req.CompletedDate ?? DateTime.UtcNow,
                CompletedBy = req.CompletedBy ?? string.Empty,
                SetsCompleted = req.SetsCompleted,
                RepsCompleted = req.RepsCompleted,
                DurationSecondsCompleted = req.DurationSecondsCompleted,
                PainLevel = req.PainLevel,
                Notes = req.Notes
            });
            return Created("", new LogHepCompletionResponse { LogId = logId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging HEP completion for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while logging the completion.");
        }
    }

    /// <summary>Gets home exercise completion logs.</summary>
    [HttpGet("hep/completions")]
    [ProducesResponseType(typeof(List<HepCompletionLog>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHepCompletionLogs(
        string patientId,
        [FromQuery] string? prescriptionId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var logs = await GetWorkflow(patientId).GetHepCompletionLogsAsync(prescriptionId, from, to);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting HEP completion logs for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving completion logs.");
        }
    }
}

// --- Request / Response DTOs ---

public record RecordSessionRequest
{
    public DateTime? SessionDate { get; init; }
    public string? TherapistId { get; init; }
    public string? TherapistName { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public Laterality Side { get; init; }
    public List<RomMeasurementDto>? RomMeasurements { get; init; }
    public List<StrengthMeasurementDto>? StrengthMeasurements { get; init; }
    public string? Notes { get; init; }
    public string? ReferralGrainKey { get; init; }
}

public record RomMeasurementDto
{
    public Movement Movement { get; init; }
    public decimal? ActiveRom { get; init; }
    public decimal? PassiveRom { get; init; }
    public string? PainOnMotion { get; init; }
    public string? Comments { get; init; }
}

public record StrengthMeasurementDto
{
    public Movement Movement { get; init; }
    public string Grade { get; init; } = "0";
    public string? Comments { get; init; }
}

public record RecordSessionResponse
{
    public string SessionKey { get; init; } = string.Empty;
}

public record MovementReference
{
    public Movement Movement { get; init; }
    public decimal? NormalRom { get; init; }
}

// --- Goal DTOs ---

public record AddPTGoalRequest
{
    public GoalType GoalType { get; init; }
    public Movement? Movement { get; init; }
    public Laterality Side { get; init; }
    public string? Description { get; init; }
    public decimal TargetValue { get; init; }
    public decimal BaselineValue { get; init; }
    public DateTime? TargetDate { get; init; }
    public string? Notes { get; init; }
}

public record AddPTGoalResponse
{
    public string GoalId { get; init; } = string.Empty;
}

public record UpdatePTGoalRequest
{
    public GoalStatus? Status { get; init; }
    public decimal? CurrentValue { get; init; }
    public string? Notes { get; init; }
}

public record AddPTGoalProgressRequest
{
    public decimal Value { get; init; }
    public string? Notes { get; init; }
}

// --- Clinic Exercise DTOs ---

public record AddClinicExerciseRequest
{
    public string ExerciseName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ExerciseCategory Category { get; init; }
    public BodyGroup BodyGroup { get; init; }
    public Movement? Movement { get; init; }
    public int? Sets { get; init; }
    public int? Reps { get; init; }
    public decimal? WeightLbs { get; init; }
    public int? DurationSeconds { get; init; }
    public decimal? DistanceFeet { get; init; }
    public string? Notes { get; init; }
}

// --- HEP DTOs ---

public record AddHepPrescriptionRequest
{
    public string ExerciseName { get; init; } = string.Empty;
    public string? Instructions { get; init; }
    public string? Frequency { get; init; }
    public int? Sets { get; init; }
    public int? Reps { get; init; }
    public int? DurationSeconds { get; init; }
    public BodyGroup BodyGroup { get; init; }
    public Movement? Movement { get; init; }
    public Laterality Side { get; init; }
    public ExerciseCategory Category { get; init; }
    public string? PrescribedBy { get; init; }
    public string? Notes { get; init; }
}

public record AddHepPrescriptionResponse
{
    public string PrescriptionId { get; init; } = string.Empty;
}

public record UpdateHepStatusRequest
{
    public HepStatus Status { get; init; }
}

public record LogHepCompletionRequest
{
    public string PrescriptionId { get; init; } = string.Empty;
    public DateTime? CompletedDate { get; init; }
    public string? CompletedBy { get; init; }
    public int? SetsCompleted { get; init; }
    public int? RepsCompleted { get; init; }
    public int? DurationSecondsCompleted { get; init; }
    public int? PainLevel { get; init; }
    public string? Notes { get; init; }
}

public record LogHepCompletionResponse
{
    public string LogId { get; init; } = string.Empty;
}

// --- Referral DTOs ---

public record CreatePTReferralRequest
{
    public string PatientName { get; init; } = string.Empty;
    public string? ReferringProviderName { get; init; }
    public string? ReferringProviderId { get; init; }
    public string? ReferringProviderSpecialty { get; init; }
    public string? ReferringFacilityName { get; init; }
    public string? Diagnosis { get; init; }
    public string? DiagnosisCode { get; init; }
    public List<BodyGroup>? BodyGroups { get; init; }
    public string? ReasonForReferral { get; init; }
    public string? Precautions { get; init; }
    public int AuthorizedVisits { get; init; }
    public DateTime? AuthorizationExpirationDate { get; init; }
    public DateTime ReferralDate { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public string? Notes { get; init; }
}

public record CreatePTReferralResponse
{
    public string ReferralKey { get; init; } = string.Empty;
}

public record UpdatePTReferralStatusRequest
{
    public PTReferralStatus Status { get; init; }
    public string? Notes { get; init; }
}

public record UpdatePTReferralAuthRequest
{
    public int AuthorizedVisits { get; init; }
    public DateTime? ExpirationDate { get; init; }
}
