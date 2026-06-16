// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize(Roles = "PrivacyOfficer,Administrator")]
[ApiController]
[Route("api/suicideprevention")]
public class SuicidePreventionController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SuicidePreventionController> _logger;

    public SuicidePreventionController(IGrainFactory grainFactory, ILogger<SuicidePreventionController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private ISuicidePreventionIndexGrain SiteIndex =>
        _grainFactory.GetGrain<ISuicidePreventionIndexGrain>("SP-INDEX");

    private IPatientRiskGrain GetRiskGrain(string patientId) =>
        _grainFactory.GetGrain<IPatientRiskGrain>($"SP-RISK:{patientId}");

    private ISafetyPlanIndexGrain GetPlanIndex(string patientId) =>
        _grainFactory.GetGrain<ISafetyPlanIndexGrain>($"SP-PLAN-IDX:{patientId}");

    private ISafetyPlanGrain GetPlanGrain(string planId) =>
        _grainFactory.GetGrain<ISafetyPlanGrain>($"SP-PLAN:{planId}");

    // ── Roster / Risk ─────────────────────────────────────────────────────────

    [HttpGet("patients")]
    public async Task<IActionResult> GetAllPatients()
    {
        try
        {
            List<PatientHighRiskSummary> result = await SiteIndex.GetAllPatientsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suicide prevention patients");
            return StatusCode(500, "An error occurred retrieving patients.");
        }
    }

    [HttpGet("patients/highrisk")]
    public async Task<IActionResult> GetHighRiskPatients()
    {
        try
        {
            List<PatientHighRiskSummary> result = await SiteIndex.GetHighRiskPatientsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving high-risk patients");
            return StatusCode(500, "An error occurred retrieving high-risk patients.");
        }
    }

    [HttpGet("patients/{patientId}/risk")]
    public async Task<IActionResult> GetPatientRisk(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            PatientRiskState result = await GetRiskGrain(decodedId).GetRiskStateAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving risk state for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving the risk state.");
        }
    }

    [HttpPost("patients/{patientId}/risk")]
    public async Task<IActionResult> SetRiskLevel(string patientId, [FromBody] SetRiskLevelRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPatientRiskGrain riskGrain = GetRiskGrain(decodedId);
            await riskGrain.SetRiskLevelAsync(req.RiskLevel, decodedId, req.PatientName, req.ProviderId, req.ProviderName);

            PatientRiskState state = await riskGrain.GetRiskStateAsync();
            List<SafetyPlanSummary> plans = await GetPlanIndex(decodedId).GetAllPlansAsync();
            await SiteIndex.UpsertPatientAsync(new PatientHighRiskSummary
            {
                PatientId = decodedId,
                PatientName = req.PatientName,
                CurrentRiskLevel = req.RiskLevel,
                IsHighRiskFlagged = state.IsHighRiskFlagged,
                LastContactDate = state.FollowUpContacts.OrderByDescending(f => f.ContactDate).FirstOrDefault()?.ContactDate,
                ActivePlanCount = plans.Count(p => p.Status == SafetyPlanStatus.Active || p.Status == SafetyPlanStatus.Draft),
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting risk level for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred setting the risk level.");
        }
    }

    [HttpPost("patients/{patientId}/flag")]
    public async Task<IActionResult> SetHighRiskFlag(string patientId, [FromBody] SetHighRiskFlagRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPatientRiskGrain riskGrain = GetRiskGrain(decodedId);
            await riskGrain.SetHighRiskFlagAsync(req.Flagged);

            PatientRiskState state = await riskGrain.GetRiskStateAsync();
            List<SafetyPlanSummary> plans = await GetPlanIndex(decodedId).GetAllPlansAsync();
            await SiteIndex.UpsertPatientAsync(new PatientHighRiskSummary
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                CurrentRiskLevel = state.CurrentRiskLevel,
                IsHighRiskFlagged = req.Flagged,
                LastContactDate = state.FollowUpContacts.OrderByDescending(f => f.ContactDate).FirstOrDefault()?.ContactDate,
                ActivePlanCount = plans.Count(p => p.Status == SafetyPlanStatus.Active || p.Status == SafetyPlanStatus.Draft),
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting high-risk flag for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred setting the high-risk flag.");
        }
    }

    [HttpPost("patients/{patientId}/followup")]
    public async Task<IActionResult> AddFollowUp(string patientId, [FromBody] AddFollowUpRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            IPatientRiskGrain riskGrain = GetRiskGrain(decodedId);
            await riskGrain.AddFollowUpContactAsync(new FollowUpContact
            {
                ContactDate = req.ContactDate,
                ContactType = req.ContactType,
                Outcome = req.Outcome,
                ProviderName = req.ProviderName,
                Notes = req.Notes ?? string.Empty,
            });

            PatientRiskState state = await riskGrain.GetRiskStateAsync();
            List<SafetyPlanSummary> plans = await GetPlanIndex(decodedId).GetAllPlansAsync();
            await SiteIndex.UpsertPatientAsync(new PatientHighRiskSummary
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                CurrentRiskLevel = state.CurrentRiskLevel,
                IsHighRiskFlagged = state.IsHighRiskFlagged,
                LastContactDate = state.FollowUpContacts.OrderByDescending(f => f.ContactDate).FirstOrDefault()?.ContactDate,
                ActivePlanCount = plans.Count(p => p.Status == SafetyPlanStatus.Active || p.Status == SafetyPlanStatus.Draft),
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding follow-up for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred adding the follow-up contact.");
        }
    }

    // ── Safety Plans ──────────────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/plans")]
    public async Task<IActionResult> GetPatientPlans(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            List<SafetyPlanSummary> result = await GetPlanIndex(decodedId).GetAllPlansAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving plans for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving safety plans.");
        }
    }

    [HttpGet("patients/{patientId}/plans/active")]
    public async Task<IActionResult> GetActivePlan(string patientId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            SafetyPlanSummary? result = await GetPlanIndex(decodedId).GetActivePlanAsync();
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active plan for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving the active plan.");
        }
    }

    [HttpGet("plans/{planId}")]
    public async Task<IActionResult> GetPlan(string planId)
    {
        try
        {
            SafetyPlanState result = await GetPlanGrain(planId).GetPlanAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving safety plan {PlanId}", planId);
            return StatusCode(500, "An error occurred retrieving the safety plan.");
        }
    }

    [HttpPost("patients/{patientId}/plans")]
    public async Task<IActionResult> CreatePlan(string patientId, [FromBody] CreateSafetyPlanRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string planId = Guid.NewGuid().ToString();
            ISafetyPlanGrain planGrain = GetPlanGrain(planId);
            await planGrain.CreatePlanAsync(planId, decodedId, req.PatientName, req.ProviderId, req.ProviderName);

            SafetyPlanSummary summary = new()
            {
                PlanId = planId,
                PatientId = decodedId,
                PatientName = req.PatientName,
                Status = SafetyPlanStatus.Draft,
                CreatedDate = DateTime.UtcNow,
            };
            await GetPlanIndex(decodedId).UpsertPlanAsync(summary);

            List<SafetyPlanSummary> plans = await GetPlanIndex(decodedId).GetAllPlansAsync();
            PatientRiskState riskState = await GetRiskGrain(decodedId).GetRiskStateAsync();
            await SiteIndex.UpsertPatientAsync(new PatientHighRiskSummary
            {
                PatientId = decodedId,
                PatientName = req.PatientName,
                CurrentRiskLevel = riskState.CurrentRiskLevel,
                IsHighRiskFlagged = riskState.IsHighRiskFlagged,
                LastContactDate = riskState.FollowUpContacts.OrderByDescending(f => f.ContactDate).FirstOrDefault()?.ContactDate,
                ActivePlanCount = plans.Count(p => p.Status == SafetyPlanStatus.Active || p.Status == SafetyPlanStatus.Draft),
                LastModifiedDate = DateTime.UtcNow,
            });

            return Created($"/api/suicideprevention/plans/{planId}", new { planId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating safety plan for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred creating the safety plan.");
        }
    }

    [HttpPost("plans/{planId}/warningsigns")]
    public async Task<IActionResult> UpdateWarningSigns(string planId, [FromBody] UpdateWarningSignsRequest req)
    {
        try
        {
            await GetPlanGrain(planId).UpdateWarningSigns(req.Signs);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warning signs for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred updating warning signs.");
        }
    }

    [HttpPost("plans/{planId}/coping")]
    public async Task<IActionResult> UpdateCopingStrategies(string planId, [FromBody] UpdateCopingStrategiesRequest req)
    {
        try
        {
            await GetPlanGrain(planId).UpdateCopingStrategies(req.Strategies);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating coping strategies for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred updating coping strategies.");
        }
    }

    [HttpPost("plans/{planId}/contacts")]
    public async Task<IActionResult> UpdateContacts(string planId, [FromBody] UpdateContactsRequest req)
    {
        try
        {
            await GetPlanGrain(planId).UpdateContacts(
                req.DistractionContacts,
                req.SupportContacts,
                req.ProfessionalContacts,
                req.CrisisLineNumbers);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contacts for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred updating contacts.");
        }
    }

    [HttpPost("plans/{planId}/means")]
    public async Task<IActionResult> UpdateMeansRestriction(string planId, [FromBody] UpdateMeansRequest req)
    {
        try
        {
            await GetPlanGrain(planId).UpdateMeansRestriction(req.MeansRemoved, req.Notes);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating means restriction for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred updating means restriction.");
        }
    }

    [HttpPost("plans/{planId}/reasons")]
    public async Task<IActionResult> UpdateReasonsForLiving(string planId, [FromBody] UpdateReasonsRequest req)
    {
        try
        {
            await GetPlanGrain(planId).UpdateReasonsForLiving(req.Reasons);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reasons for living for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred updating reasons for living.");
        }
    }

    [HttpPost("plans/{planId}/review")]
    public async Task<IActionResult> ReviewPlan(string planId, [FromBody] ReviewPlanRequest req)
    {
        try
        {
            ISafetyPlanGrain planGrain = GetPlanGrain(planId);
            await planGrain.ReviewPlanAsync(req.ReviewDate);

            SafetyPlanState state = await planGrain.GetPlanAsync();
            await GetPlanIndex(state.PatientId).UpsertPlanAsync(new SafetyPlanSummary
            {
                PlanId = planId,
                PatientId = state.PatientId,
                PatientName = state.PatientName,
                Status = state.Status,
                CreatedDate = state.CreatedDate,
                LastReviewedDate = state.LastReviewedDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing plan {PlanId}", planId);
            return StatusCode(500, "An error occurred reviewing the plan.");
        }
    }

    [HttpPost("plans/{planId}/archive")]
    public async Task<IActionResult> ArchivePlan(string planId)
    {
        try
        {
            ISafetyPlanGrain planGrain = GetPlanGrain(planId);
            await planGrain.ArchivePlanAsync();

            SafetyPlanState state = await planGrain.GetPlanAsync();
            SafetyPlanSummary updatedSummary = new()
            {
                PlanId = planId,
                PatientId = state.PatientId,
                PatientName = state.PatientName,
                Status = SafetyPlanStatus.Archived,
                CreatedDate = state.CreatedDate,
                LastReviewedDate = state.LastReviewedDate,
            };
            await GetPlanIndex(state.PatientId).UpsertPlanAsync(updatedSummary);

            List<SafetyPlanSummary> plans = await GetPlanIndex(state.PatientId).GetAllPlansAsync();
            PatientRiskState riskState = await GetRiskGrain(state.PatientId).GetRiskStateAsync();
            await SiteIndex.UpsertPatientAsync(new PatientHighRiskSummary
            {
                PatientId = state.PatientId,
                PatientName = state.PatientName,
                CurrentRiskLevel = riskState.CurrentRiskLevel,
                IsHighRiskFlagged = riskState.IsHighRiskFlagged,
                LastContactDate = riskState.FollowUpContacts.OrderByDescending(f => f.ContactDate).FirstOrDefault()?.ContactDate,
                ActivePlanCount = plans.Count(p => p.Status == SafetyPlanStatus.Active || p.Status == SafetyPlanStatus.Draft),
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving plan {PlanId}", planId);
            return StatusCode(500, "An error occurred archiving the plan.");
        }
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record SetRiskLevelRequest(
    RiskLevel RiskLevel,
    string PatientName,
    string ProviderId,
    string ProviderName);

public record SetHighRiskFlagRequest(bool Flagged);

public record AddFollowUpRequest(
    DateTime ContactDate,
    FollowUpContactType ContactType,
    FollowUpContactOutcome Outcome,
    string ProviderName,
    string? Notes);

public record CreateSafetyPlanRequest(
    string PatientName,
    string ProviderId,
    string ProviderName);

public record UpdateWarningSignsRequest(List<string> Signs);

public record UpdateCopingStrategiesRequest(List<string> Strategies);

public record UpdateContactsRequest(
    List<string> DistractionContacts,
    List<SupportContact> SupportContacts,
    List<ProfessionalContact> ProfessionalContacts,
    List<string> CrisisLineNumbers);

public record UpdateMeansRequest(List<string> MeansRemoved, string Notes);

public record UpdateReasonsRequest(List<string> Reasons);

public record ReviewPlanRequest(DateTime ReviewDate);
