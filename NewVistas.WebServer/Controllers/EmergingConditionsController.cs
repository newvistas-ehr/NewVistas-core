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
/// Emerging-condition surveillance API (the ProtoCondition module). Proto-scoped mutations are
/// key-gated on the proto grain directly (the caller's <c>EPI MANAGER</c> key propagates via the
/// Orleans request-context middleware); patient-touching operations route through the workflow grain
/// so they are per-patient audited; reads are open. Mirrors the <c>/pcc-surveillance</c> precedent.
/// </summary>
[ApiController]
[Route("api/emerging-conditions")]
[Authorize]
public class EmergingConditionsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EmergingConditionsController> _logger;

    public EmergingConditionsController(IGrainFactory grainFactory, ILogger<EmergingConditionsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain Workflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IProtoConditionGrain Proto(string id) => _grainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{id}");
    private IProtoConditionIndexGrain Index() => _grainFactory.GetGrain<IProtoConditionIndexGrain>("PROTOCONDITION-INDEX");
    private string CurrentUser => User.Identity?.Name ?? "web";

    // ─── Directory & reads (open) ───────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false)
    {
        try { return Ok(activeOnly ? await Index().GetActiveAsync() : await Index().GetAllAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        try { return Ok(await Proto(id).GetAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("{id}/members/{status}")]
    public async Task<IActionResult> Members(string id, ProtoMemberStatus status)
    {
        try { return Ok(await Proto(id).GetMembersByStatusAsync(status)); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("{id}/analytics")]
    public async Task<IActionResult> Analytics(string id)
    {
        try { return Ok(await _grainFactory.GetGrain<IProtoAnalyticsGrain>($"PROTO-ANALYTICS:{id}").AnalyzeAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("sweeps")]
    public async Task<IActionResult> Sweeps()
    {
        try { return Ok(await _grainFactory.GetGrain<IProtoSweepGrain>("PROTO-SWEEP").GetRecentRunsAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    // ─── Definition & lifecycle (EPI-gated on the grain) ────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProtoRequest req)
    {
        try
        {
            string id = Guid.NewGuid().ToString();
            await Proto(id).CreateAsync(req.Name, req.Description ?? string.Empty, CurrentUser);
            return Created($"api/emerging-conditions/{id}", new { Id = id });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("{id}/features")]
    public async Task<IActionResult> UpsertFeature(string id, [FromBody] ProtoFeature feature) =>
        await Mutate(() => Proto(id).AddOrUpdateFeatureAsync(feature, CurrentUser));

    [HttpDelete("{id}/features/{featureId}")]
    public async Task<IActionResult> RemoveFeature(string id, string featureId) =>
        await Mutate(() => Proto(id).RemoveFeatureAsync(featureId, CurrentUser));

    [HttpPut("{id}/threshold")]
    public async Task<IActionResult> SetThreshold(string id, [FromBody] ThresholdRequest req) =>
        await Mutate(() => Proto(id).SetMatchThresholdAsync(req.Threshold, CurrentUser));

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(string id) =>
        await Mutate(() => Proto(id).ActivateAsync(CurrentUser));

    [HttpPost("{id}/retire")]
    public async Task<IActionResult> Retire(string id, [FromBody] ReasonRequest req) =>
        await Mutate(() => Proto(id).RetireAsync(CurrentUser, req.Reason ?? string.Empty));

    [HttpPut("{id}/guidance")]
    public async Task<IActionResult> SetGuidance(string id, [FromBody] GuidanceRequest req) =>
        await Mutate(() => Proto(id).SetGuidanceAsync(ParseIsolation(req.Isolation), req.PpeNotes, req.OrderSetIds ?? new(), CurrentUser));

    [HttpPut("{id}/alert-rule")]
    public async Task<IActionResult> SetAlertRule(string id, [FromBody] ProtoAlertRule rule) =>
        await Mutate(() => Proto(id).SetAlertRuleAsync(rule, CurrentUser));

    [HttpPost("{id}/promote")]
    public async Task<IActionResult> Promote(string id, [FromBody] PromoteRequest req) =>
        await Mutate(() => Proto(id).PromoteAsync(req.OfficialName, req.Icd10Codes ?? new(), req.SnomedCode,
            req.EffectiveFrom, req.Jurisdictions ?? new(), req.Notes ?? string.Empty, CurrentUser));

    // ─── Screening & sweep ──────────────────────────────────────────────

    [HttpGet("{id}/patients/{patientId}/screen")]
    public async Task<IActionResult> Preview(string id, string patientId)
    {
        try { return Ok(await _grainFactory.GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{patientId}").EvaluateAsync(id)); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("{id}/patients/{patientId}/screen")]
    public async Task<IActionResult> ScreenAndRecord(string id, string patientId)
    {
        try { return Ok(await _grainFactory.GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{patientId}").EvaluateAndRecordAsync(id)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("{id}/sweep")]
    public async Task<IActionResult> Sweep(string id, [FromBody] SweepRequest req)
    {
        try { return Ok(await _grainFactory.GetGrain<IProtoSweepGrain>("PROTO-SWEEP").SweepProtoAsync(id, req?.MaxPatients, CurrentUser)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    // ─── Membership & migration (via workflow — audited) ────────────────

    [HttpPost("{id}/patients/{patientId}/suggest")]
    public async Task<IActionResult> Suggest(string id, string patientId) =>
        await Mutate(() => Workflow(patientId).SuggestForProtoConditionAsync(id, CurrentUser));

    [HttpPost("{id}/patients/{patientId}/confirm")]
    public async Task<IActionResult> Confirm(string id, string patientId) =>
        await Mutate(() => Workflow(patientId).ConfirmProtoMembershipAsync(id, CurrentUser));

    [HttpPost("{id}/patients/{patientId}/exclude")]
    public async Task<IActionResult> Exclude(string id, string patientId, [FromBody] ReasonRequest req) =>
        await Mutate(() => Workflow(patientId).ExcludeProtoMembershipAsync(id, req.Reason ?? string.Empty, CurrentUser));

    [HttpPost("{id}/patients/{patientId}/migrate")]
    public async Task<IActionResult> Migrate(string id, string patientId)
    {
        try { return Ok(new { ProblemId = await Workflow(patientId).MigratePromotedProtoProblemAsync(id, CurrentUser) }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("{id}/patients/{patientId}/skip-migration")]
    public async Task<IActionResult> SkipMigration(string id, string patientId, [FromBody] ReasonRequest req) =>
        await Mutate(() => Workflow(patientId).SkipMemberMigrationAsync(id, req.Reason ?? string.Empty, CurrentUser));

    // ─── Symptom capture & survey ───────────────────────────────────────

    [HttpGet("symptom-catalog")]
    public IActionResult SymptomCatalogAll() => Ok(SymptomCatalog.All);

    [HttpGet("survey")]
    public async Task<IActionResult> Survey()
    {
        try
        {
            // Wide net = core screen ∪ the symptom features of the currently active protos.
            List<ProtoConditionSummary> active = await Index().GetActiveAsync();
            var extraCodes = new List<string>();
            foreach (ProtoConditionSummary s in active.Take(25))
            {
                ProtoConditionState proto = await Proto(s.ProtoConditionId).GetAsync();
                extraCodes.AddRange(proto.Features.Where(f => f.Kind == ProtoFeatureKind.Symptom).Select(f => f.Code));
            }
            return Ok(SymptomCatalog.BuildSurveyQuestionSet(extraCodes));
        }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpGet("patients/{patientId}/symptoms")]
    public async Task<IActionResult> GetSymptoms(string patientId)
    {
        try { return Ok(await Workflow(patientId).GetPatientSymptomsAsync()); }
        catch (Exception ex) { return Fail(ex); }
    }

    [HttpPost("patients/{patientId}/symptoms")]
    public async Task<IActionResult> RecordSymptoms(string patientId, [FromBody] List<SymptomObservation> observations)
    {
        try { return Ok(new { Accepted = await Workflow(patientId).RecordSymptomObservationsAsync(observations ?? new()) }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private async Task<IActionResult> Mutate(Func<Task> action)
    {
        try { await action(); return Ok(new { Message = "OK" }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex); }
    }

    private IActionResult Fail(Exception ex)
    {
        _logger.LogError(ex, "Emerging-conditions API error");
        return StatusCode(500, new { Error = "An error occurred." });
    }

    private static BedIsolationType? ParseIsolation(string? isolation) =>
        Enum.TryParse(isolation, ignoreCase: true, out BedIsolationType v) ? v : null;
}

public record CreateProtoRequest(string Name, string? Description);
public record ThresholdRequest(double Threshold);
public record ReasonRequest(string? Reason);
public record GuidanceRequest(string? Isolation, string? PpeNotes, List<string>? OrderSetIds);
public record PromoteRequest(string OfficialName, List<string>? Icd10Codes, string? SnomedCode,
    DateTime? EffectiveFrom, List<string>? Jurisdictions, string? Notes);
public record SweepRequest(int? MaxPatients);
