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
/// REST API for Research / IRB Tracking: study protocols, IRB submissions,
/// and research subject consent tracking.
/// VistA Research Module (~File #900). RCRJ.m, RCRTX.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ResearchController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ResearchController> _logger;

    public ResearchController(IGrainFactory grainFactory, ILogger<ResearchController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IResearchStudyGrain Study(string id)
        => _grainFactory.GetGrain<IResearchStudyGrain>(Uri.UnescapeDataString(id));

    private IResearchStudyIndexGrain StudyIndex()
        => _grainFactory.GetGrain<IResearchStudyIndexGrain>("IRB-STUDY-IDX");

    private IResearchSubjectGrain Subject(string id)
        => _grainFactory.GetGrain<IResearchSubjectGrain>(Uri.UnescapeDataString(id));

    private IResearchSubjectIndexGrain SubjectIndex(string studyId)
        => _grainFactory.GetGrain<IResearchSubjectIndexGrain>($"IRB-SUBJECT-IDX:{studyId}");

    private static IrbStudyIndexEntry BuildStudyIndex(ResearchStudyState s) => new()
    {
        StudyId = s.StudyId,
        IrbProtocolNumber = s.IrbProtocolNumber,
        Title = s.Title,
        PrincipalInvestigator = s.PrincipalInvestigator,
        StudyType = s.StudyType,
        Phase = s.Phase,
        Status = s.Status,
        CurrentEnrollment = s.CurrentEnrollment,
        TargetEnrollment = s.TargetEnrollment,
        CurrentExpirationDate = s.CurrentExpirationDate
    };

    private static ResearchSubjectIndexEntry BuildSubjectIndex(ResearchSubjectState s) => new()
    {
        SubjectId = s.SubjectId,
        StudyId = s.StudyId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        EnrollmentStatus = s.EnrollmentStatus,
        EnrollmentDate = s.EnrollmentDate,
        ConsentDate = s.ConsentDate,
        Arm = s.Arm
    };

    // ── Studies ───────────────────────────────────────────────────────────────

    [HttpPost("studies")]
    public async Task<IActionResult> CreateStudy([FromBody] CreateResearchStudyDto dto)
    {
        try
        {
            string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
            await Study(studyId).CreateStudyAsync(
                dto.IrbProtocolNumber, dto.Title, dto.ShortTitle,
                dto.PrincipalInvestigator, dto.PiEmployeeId, dto.Sponsor,
                dto.StudyType, dto.Phase, dto.Department,
                dto.TargetEnrollment, dto.Description);
            ResearchStudyState state = await Study(studyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(state));
            return Created($"/api/research/studies/{Uri.EscapeDataString(studyId)}", new { studyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating research study {Protocol}", dto.IrbProtocolNumber);
            return StatusCode(500, "Error creating study.");
        }
    }

    [HttpGet("studies/{studyId}")]
    public async Task<IActionResult> GetStudy(string studyId)
    {
        try
        {
            return Ok(await Study(studyId).GetStudyAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving study {StudyId}", studyId);
            return StatusCode(500, "Error retrieving study.");
        }
    }

    [HttpGet("studies")]
    public async Task<IActionResult> GetAllStudies()
    {
        try
        {
            return Ok(await StudyIndex().GetAllStudiesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all studies");
            return StatusCode(500, "Error retrieving studies.");
        }
    }

    [HttpGet("studies/open")]
    public async Task<IActionResult> GetOpenStudies()
    {
        try
        {
            return Ok(await StudyIndex().GetOpenStudiesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving open studies");
            return StatusCode(500, "Error retrieving open studies.");
        }
    }

    [HttpGet("studies/expiring")]
    public async Task<IActionResult> GetStudiesExpiring([FromQuery] int withinDays = 60)
    {
        try
        {
            return Ok(await StudyIndex().GetStudiesExpiringAsync(withinDays));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expiring studies");
            return StatusCode(500, "Error retrieving expiring studies.");
        }
    }

    [HttpPost("studies/{studyId}/open")]
    public async Task<IActionResult> OpenForEnrollment(string studyId, [FromBody] OpenForEnrollmentDto dto)
    {
        try
        {
            await Study(studyId).OpenForEnrollmentAsync(dto.ApprovalDate, dto.ExpirationDate, dto.NextContinuingReviewDue);
            ResearchStudyState state = await Study(studyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening study {StudyId} for enrollment", studyId);
            return StatusCode(500, "Error opening study.");
        }
    }

    [HttpPost("studies/{studyId}/close")]
    public async Task<IActionResult> CloseToEnrollment(string studyId)
    {
        try
        {
            await Study(studyId).CloseToEnrollmentAsync();
            ResearchStudyState state = await Study(studyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing study {StudyId} to enrollment", studyId);
            return StatusCode(500, "Error closing study.");
        }
    }

    [HttpPost("studies/{studyId}/complete")]
    public async Task<IActionResult> CompleteStudy(string studyId)
    {
        try
        {
            await Study(studyId).CompleteStudyAsync();
            ResearchStudyState state = await Study(studyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing study {StudyId}", studyId);
            return StatusCode(500, "Error completing study.");
        }
    }

    [HttpPost("studies/{studyId}/submissions")]
    public async Task<IActionResult> RecordSubmission(string studyId, [FromBody] RecordSubmissionDto dto)
    {
        try
        {
            string submissionId = Guid.NewGuid().ToString();
            await Study(studyId).RecordSubmissionAsync(
                submissionId, dto.SubmissionType, dto.SubmissionDate, dto.Notes);
            return Created($"/api/research/studies/{Uri.EscapeDataString(studyId)}", new { submissionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording submission for study {StudyId}", studyId);
            return StatusCode(500, "Error recording submission.");
        }
    }

    [HttpPost("studies/{studyId}/submissions/{submissionId}/decision")]
    public async Task<IActionResult> UpdateSubmissionDecision(
        string studyId, string submissionId, [FromBody] UpdateSubmissionDecisionDto dto)
    {
        try
        {
            await Study(studyId).UpdateSubmissionDecisionAsync(
                submissionId, dto.Status, dto.Decision, dto.ReviewDate, dto.NewExpirationDate);
            ResearchStudyState state = await Study(studyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating submission decision for study {StudyId}", studyId);
            return StatusCode(500, "Error updating submission decision.");
        }
    }

    // ── Subjects ──────────────────────────────────────────────────────────────

    [HttpPost("subjects/enroll")]
    public async Task<IActionResult> EnrollSubject([FromBody] EnrollResearchSubjectDto dto)
    {
        try
        {
            string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
            string studyId = Uri.UnescapeDataString(dto.StudyId.Trim());
            await Subject(subjectId).EnrollSubjectAsync(
                studyId, dto.StudyTitle, dto.PatientId, dto.PatientName,
                dto.PatientDOB, dto.ScreeningDate, dto.EnrollmentDate,
                dto.ConsentDate, dto.ConsentType, dto.ConsentObtainedBy, dto.Arm);
            ResearchSubjectState state = await Subject(subjectId).GetSubjectAsync();
            await SubjectIndex(studyId).UpsertSubjectAsync(BuildSubjectIndex(state));
            // Increment study enrollment count
            await Study(studyId).IncrementEnrollmentAsync();
            ResearchStudyState studyState = await Study(studyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(studyState));
            return Created($"/api/research/subjects/{Uri.EscapeDataString(subjectId)}", new { subjectId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling subject in study {StudyId}", dto.StudyId);
            return StatusCode(500, "Error enrolling subject.");
        }
    }

    [HttpGet("subjects/{subjectId}")]
    public async Task<IActionResult> GetSubject(string subjectId)
    {
        try
        {
            return Ok(await Subject(subjectId).GetSubjectAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subject {SubjectId}", subjectId);
            return StatusCode(500, "Error retrieving subject.");
        }
    }

    [HttpGet("studies/{studyId}/subjects")]
    public async Task<IActionResult> GetSubjectsByStudy(string studyId)
    {
        try
        {
            string id = Uri.UnescapeDataString(studyId.Trim());
            return Ok(await SubjectIndex(id).GetAllSubjectsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subjects for study {StudyId}", studyId);
            return StatusCode(500, "Error retrieving subjects.");
        }
    }

    [HttpGet("studies/{studyId}/subjects/active")]
    public async Task<IActionResult> GetActiveSubjects(string studyId)
    {
        try
        {
            string id = Uri.UnescapeDataString(studyId.Trim());
            return Ok(await SubjectIndex(id).GetActiveSubjectsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active subjects for study {StudyId}", studyId);
            return StatusCode(500, "Error retrieving active subjects.");
        }
    }

    [HttpPost("subjects/{subjectId}/withdraw")]
    public async Task<IActionResult> WithdrawSubject(string subjectId, [FromBody] WithdrawSubjectDto dto)
    {
        try
        {
            await Subject(subjectId).WithdrawSubjectAsync(dto.Reason, dto.WithdrawalDate);
            ResearchSubjectState state = await Subject(subjectId).GetSubjectAsync();
            await SubjectIndex(state.StudyId).UpsertSubjectAsync(BuildSubjectIndex(state));
            // Decrement study enrollment count
            await Study(state.StudyId).DecrementEnrollmentAsync();
            ResearchStudyState studyState = await Study(state.StudyId).GetStudyAsync();
            await StudyIndex().UpsertStudyAsync(BuildStudyIndex(studyState));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing subject {SubjectId}", subjectId);
            return StatusCode(500, "Error withdrawing subject.");
        }
    }

    [HttpPost("subjects/{subjectId}/complete")]
    public async Task<IActionResult> CompleteSubject(string subjectId, [FromBody] CompleteSubjectDto dto)
    {
        try
        {
            await Subject(subjectId).CompleteSubjectAsync(dto.CompletionDate);
            ResearchSubjectState state = await Subject(subjectId).GetSubjectAsync();
            await SubjectIndex(state.StudyId).UpsertSubjectAsync(BuildSubjectIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing subject {SubjectId}", subjectId);
            return StatusCode(500, "Error completing subject.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<IrbStudyIndexEntry> all = await StudyIndex().GetAllStudiesAsync();
            List<IrbStudyIndexEntry> expiring = await StudyIndex().GetStudiesExpiringAsync(60);

            var dashboard = new
            {
                TotalStudies = all.Count,
                OpenForEnrollment = all.Count(s => s.Status == IrbStudyStatus.OpenForEnrollment),
                Draft = all.Count(s => s.Status == IrbStudyStatus.Draft),
                ClosedToEnrollment = all.Count(s => s.Status == IrbStudyStatus.ClosedToEnrollment),
                Completed = all.Count(s => s.Status == IrbStudyStatus.Completed),
                Suspended = all.Count(s => s.Status == IrbStudyStatus.Suspended),
                Interventional = all.Count(s => s.StudyType == IrbStudyType.Interventional),
                Observational = all.Count(s => s.StudyType == IrbStudyType.Observational),
                TotalCurrentEnrollment = all.Sum(s => s.CurrentEnrollment),
                ExpiringNext60Days = expiring.Count
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating research dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CreateResearchStudyDto(
    string IrbProtocolNumber,
    string Title,
    string ShortTitle,
    string PrincipalInvestigator,
    string PiEmployeeId,
    string Sponsor,
    IrbStudyType StudyType,
    IrbStudyPhase Phase,
    string Department,
    int TargetEnrollment,
    string Description);

public record OpenForEnrollmentDto(
    DateTime ApprovalDate,
    DateTime ExpirationDate,
    DateTime? NextContinuingReviewDue);

public record RecordSubmissionDto(
    IrbSubmissionType SubmissionType,
    DateTime SubmissionDate,
    string Notes);

public record UpdateSubmissionDecisionDto(
    IrbSubmissionStatus Status,
    string Decision,
    DateTime ReviewDate,
    DateTime? NewExpirationDate);

public record EnrollResearchSubjectDto(
    string StudyId,
    string StudyTitle,
    string PatientId,
    string PatientName,
    DateTime? PatientDOB,
    DateTime ScreeningDate,
    DateTime EnrollmentDate,
    DateTime ConsentDate,
    ConsentType ConsentType,
    string ConsentObtainedBy,
    string Arm);

public record WithdrawSubjectDto(string Reason, DateTime WithdrawalDate);

public record CompleteSubjectDto(DateTime CompletionDate);
