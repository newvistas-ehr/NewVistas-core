// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Whole-Person Social Care API (ADR-005): the Person-anchored household and the coded SDOH screening
/// closed loop. Patient-scoped operations route through the workflow grain (audited); household
/// structural edits go to the household grain directly; reads are open. Gated by the SOCIAL_CARE feature.
/// </summary>
[ApiController]
[Route("api/social-care")]
[Authorize]
public class SocialCareController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SocialCareController> _logger;

    public SocialCareController(IGrainFactory grainFactory, ILogger<SocialCareController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain Workflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IHouseholdGrain Household(string id) => _grainFactory.GetGrain<IHouseholdGrain>(id);
    private string CurrentUser => User.Identity?.Name ?? "web";

    // ─── Household ──────────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/household")]
    public async Task<IActionResult> GetHousehold(string patientId)
    {
        try { return Ok(await Workflow(patientId).GetPatientHouseholdAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/household")]
    public async Task<IActionResult> CreateHousehold(string patientId, [FromBody] CreateHouseholdRequest req)
    {
        try
        {
            string id = await Workflow(patientId).CreateHouseholdForPatientAsync(
                req.Label, req.Relationship ?? "Self", req.FacilityId ?? "500", CurrentUser);
            return Created($"api/social-care/households/{id}", new { HouseholdId = id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/household/{householdId}/join")]
    public async Task<IActionResult> JoinHousehold(string patientId, string householdId, [FromBody] JoinHouseholdRequest req) =>
        await Mutate(() => Workflow(patientId).AddPatientToHouseholdAsync(householdId, req.Relationship ?? string.Empty, req.Role, req.FacilityId ?? "500", CurrentUser));

    [HttpPost("patients/{patientId}/household/{householdId}/non-patient-member")]
    public async Task<IActionResult> AddNonPatientMember(string patientId, string householdId, [FromBody] NonPatientMemberRequest req)
    {
        try
        {
            string personId = await Workflow(patientId).AddNonPatientMemberToHouseholdAsync(
                householdId, req.Name, req.DateOfBirth, req.Sex ?? string.Empty, req.SsnLast4 ?? string.Empty,
                req.Relationship ?? string.Empty, req.Role, CurrentUser);
            return Ok(new { PersonId = personId });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("households/{householdId}/members/{personId}/remove")]
    public async Task<IActionResult> RemoveMember(string householdId, string personId) =>
        await Mutate(() => Household(householdId).RemoveMemberAsync(personId, CurrentUser));

    [HttpPut("households/{householdId}/housing")]
    public async Task<IActionResult> SetHousing(string householdId, [FromBody] HousingRequest req) =>
        await Mutate(() => Household(householdId).SetHousingAsync(req.HousingType, req.Street, req.City, req.State, req.Zip, CurrentUser));

    // ─── SDOH screening + closed loop ───────────────────────────────────

    [HttpPost("patients/{patientId}/sdoh")]
    public async Task<IActionResult> RecordScreening(string patientId, [FromBody] SdohScreeningRequest req)
    {
        try
        {
            string id = await Workflow(patientId).RecordSdohScreeningAsync(
                req.InstrumentName ?? SdohScreeningCatalog.DefaultInstrument, req.Responses ?? new(), CurrentUser);
            return Ok(new { ScreeningId = id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("patients/{patientId}/sdoh")]
    public async Task<IActionResult> GetScreenings(string patientId)
    {
        try { return Ok(await Workflow(patientId).GetSdohScreeningsAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("patients/{patientId}/sdoh/{screeningId}")]
    public async Task<IActionResult> GetScreening(string patientId, string screeningId)
    {
        try { return Ok(await Workflow(patientId).GetSdohScreeningAsync(screeningId)); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/sdoh/{screeningId}/apply-zcode/{domain}")]
    public async Task<IActionResult> ApplyZCode(string patientId, string screeningId, SdohDomain domain)
    {
        try { return Ok(new { ProblemId = await Workflow(patientId).AddSdohProblemAsync(screeningId, domain, CurrentUser) }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/sdoh/{screeningId}/refer/{domain}")]
    public async Task<IActionResult> Refer(string patientId, string screeningId, SdohDomain domain)
    {
        try { return Ok(new { ReferralId = await Workflow(patientId).CreateSdohReferralAsync(screeningId, domain, CurrentUser) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("sdoh-cohort/{domain}")]
    public async Task<IActionResult> CohortCount(SdohDomain domain)
    {
        try { return Ok(new { Domain = domain.ToString(), Count = await _grainFactory.GetGrain<ISdohCohortIndexGrain>($"SDOH-COHORT:{domain}").GetCountAsync() }); }
        catch (Exception ex) { return Fail(ex); }
    }

    // ─── Case management (goal / step / follow-up / outcome) ────────────

    private ICaseManagementGrain Case(string patientId) => _grainFactory.GetGrain<ICaseManagementGrain>($"CASE-MGMT:{patientId}");

    [HttpGet("patients/{patientId}/case")]
    public async Task<IActionResult> GetCase(string patientId)
    {
        try { return Ok(await Workflow(patientId).GetCaseManagementAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("caseload")]
    public async Task<IActionResult> Caseload()
    {
        try { return Ok(await _grainFactory.GetGrain<ICaseManagementIndexGrain>("CASE-MGMT-INDEX").GetCaseloadAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/case/goals")]
    public async Task<IActionResult> AddGoal(string patientId, [FromBody] CaseGoalRequest req)
    {
        try { return Ok(new { GoalId = await Workflow(patientId).AddCaseGoalAsync(req.Description, req.Domain, req.TargetDate, req.SourceReference, CurrentUser) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPut("patients/{patientId}/case/goals/{goalId}/status")]
    public async Task<IActionResult> SetGoalStatus(string patientId, string goalId, [FromBody] GoalStatusRequest req) =>
        await Mutate(() => Case(patientId).UpdateGoalStatusAsync(goalId, req.Status, CurrentUser));

    [HttpPost("patients/{patientId}/case/goals/{goalId}/steps")]
    public async Task<IActionResult> AddStep(string patientId, string goalId, [FromBody] WorkStepRequest req)
    {
        try { return Ok(new { StepId = await Case(patientId).AddWorkStepAsync(goalId, req.Description, req.DueDate, CurrentUser) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPut("patients/{patientId}/case/goals/{goalId}/steps/{stepId}/status")]
    public async Task<IActionResult> SetStepStatus(string patientId, string goalId, string stepId, [FromBody] StepStatusRequest req) =>
        await Mutate(() => Case(patientId).UpdateWorkStepStatusAsync(goalId, stepId, req.Status, CurrentUser));

    [HttpPost("patients/{patientId}/case/goals/{goalId}/followups")]
    public async Task<IActionResult> AddFollowUp(string patientId, string goalId, [FromBody] CaseFollowUpRequest req)
    {
        try { return Ok(new { FollowUpId = await Case(patientId).AddFollowUpAsync(goalId, req.Date, req.Note ?? string.Empty, CurrentUser) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/case/goals/{goalId}/outcome")]
    public async Task<IActionResult> RecordOutcome(string patientId, string goalId, [FromBody] OutcomeRequest req) =>
        await Mutate(() => Case(patientId).RecordOutcomeAsync(goalId, req.Achieved, req.Note ?? string.Empty, CurrentUser));

    // ─── Community-resource directory (R3) ──────────────────────────────

    private IResourceDirectoryGrain Directory() => _grainFactory.GetGrain<IResourceDirectoryGrain>("RESOURCE-DIRECTORY");

    [HttpGet("resources")]
    public async Task<IActionResult> ListResources([FromQuery] SocialWorkReferralServiceType? serviceType, [FromQuery] string? q)
    {
        try
        {
            List<CommunityResource> results = (serviceType is null && string.IsNullOrWhiteSpace(q))
                ? await Directory().GetAllAsync()
                : await Directory().SearchAsync(serviceType, q);
            return Ok(results);
        }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("resources/{resourceId}")]
    public async Task<IActionResult> GetResource(string resourceId)
    {
        try
        {
            CommunityResource? r = await Directory().GetAsync(resourceId);
            return r is null ? NotFound() : Ok(r);
        }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("resources")]
    public async Task<IActionResult> AddResource([FromBody] CommunityResource resource)
    {
        try
        {
            string id = await Directory().AddOrUpdateAsync(resource);
            return Created($"api/social-care/resources/{id}", new { ResourceId = id });
        }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpDelete("resources/{resourceId}")]
    public async Task<IActionResult> RemoveResource(string resourceId) =>
        await Mutate(() => Directory().RemoveAsync(resourceId));

    /// <summary>Refer this patient to a directory resource (opens a Social Work referral to that agency).</summary>
    [HttpPost("patients/{patientId}/refer-to-resource/{resourceId}")]
    public async Task<IActionResult> ReferToResource(string patientId, string resourceId, [FromBody] ReferToResourceRequest? req)
    {
        try { return Ok(new { ReferralId = await Workflow(patientId).ReferToResourceAsync(resourceId, req?.Reason ?? string.Empty, CurrentUser) }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    // ─── Veteran psychosocial enrichment (R4) ───────────────────────────

    [HttpGet("patients/{patientId}/veteran-psychosocial")]
    public async Task<IActionResult> GetVeteranPsychosocial(string patientId)
    {
        try { return Ok(await Workflow(patientId).GetVeteranPsychosocialAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPut("patients/{patientId}/veteran-psychosocial")]
    public async Task<IActionResult> SetVeteranPsychosocial(string patientId, [FromBody] VeteranPsychosocialProfile profile) =>
        await Mutate(() => Workflow(patientId).UpdateVeteranPsychosocialAsync(profile, CurrentUser));

    // ─── Helpers ────────────────────────────────────────────────────────

    private async Task<IActionResult> Mutate(Func<Task> action)
    {
        try { await action(); return Ok(new { Message = "OK" }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    private IActionResult Fail(Exception ex)
    {
        _logger.LogError(ex, "Social Care API error");
        return StatusCode(500, new { Error = "An error occurred." });
    }
}

public record CreateHouseholdRequest(string Label, string? Relationship, string? FacilityId);
public record JoinHouseholdRequest(string? Relationship, HouseholdMemberRole Role, string? FacilityId);
public record NonPatientMemberRequest(string Name, DateTime? DateOfBirth, string? Sex, string? SsnLast4, string? Relationship, HouseholdMemberRole Role);
public record HousingRequest(HouseholdHousingType HousingType, string? Street, string? City, string? State, string? Zip);
public record SdohScreeningRequest(string? InstrumentName, List<SdohScreeningResponse>? Responses);
public record CaseGoalRequest(string Description, CaseGoalDomain Domain, DateTime? TargetDate, string? SourceReference);
public record GoalStatusRequest(CaseGoalStatus Status);
public record WorkStepRequest(string Description, DateTime? DueDate);
public record StepStatusRequest(CaseWorkStepStatus Status);
public record CaseFollowUpRequest(DateTime Date, string? Note);
public record OutcomeRequest(bool Achieved, string? Note);
public record ReferToResourceRequest(string? Reason);
