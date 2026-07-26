// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Medical/procedure prior-authorization API (`api/procedurepriorauth`). Parallel to the drug
/// PharmacyBenefits PA. Writes route through <see cref="IPatientWorkflowGrain"/> so the IBCNR PRECERT
/// security key + audit filters fire; the requirements checklist read is open (advisory). Consumed by the
/// Blazor `/prior-auth` hub and (future) an external UM client.
/// </summary>
[Authorize]
[ApiController]
[Route("api/procedurepriorauth")]
public class ProcedurePriorAuthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ProcedurePriorAuthController> _logger;

    public ProcedurePriorAuthController(IGrainFactory grainFactory, ILogger<ProcedurePriorAuthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain Workflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private string CurrentUser => User.Identity?.Name ?? "web";

    [HttpGet("patients/{patientId}/procedure-auths")]
    public async Task<IActionResult> GetAll(string patientId)
    {
        try { return Ok(await Workflow(patientId).GetProcedureAuthsAsync()); }
        catch (Exception ex) { return Fail(ex, "listing procedure prior-auths"); }
    }

    [HttpGet("patients/{patientId}/procedure-auths/{procAuthId}")]
    public async Task<IActionResult> Get(string patientId, string procAuthId)
    {
        try { return Ok(await Workflow(patientId).GetProcedureAuthAsync(procAuthId)); }
        catch (Exception ex) { return Fail(ex, "retrieving procedure prior-auth"); }
    }

    /// <summary>The ranked "fill these boxes" requirements checklist for a (procedure, payer).</summary>
    [HttpGet("patients/{patientId}/procedure-auth-requirements")]
    public async Task<IActionResult> GetRequirements(string patientId, [FromQuery] string cpt, [FromQuery] string payerId)
    {
        try { return Ok(await Workflow(patientId).GetPriorAuthRequirementsAsync(cpt, payerId)); }
        catch (Exception ex) { return Fail(ex, "computing prior-auth requirements"); }
    }

    [HttpPost("patients/{patientId}/procedure-auths")]
    public async Task<IActionResult> Submit(string patientId, [FromBody] SubmitProcedureAuthRequest req)
    {
        try
        {
            string id = await Workflow(patientId).SubmitProcedureAuthAsync(
                req.CptCode, req.CptDescription ?? string.Empty, req.PayerId, req.PayerName ?? string.Empty,
                req.OrderingProviderId ?? string.Empty, req.OrderingProviderName ?? string.Empty,
                req.DiagnosisCodes ?? new(), req.ClinicalJustification ?? string.Empty,
                req.ServiceStartDate, req.ServiceEndDate, req.Channel, req.OrderId, req.ConsultId, req.ExternalReferralId);
            return Created($"api/procedurepriorauth/patients/{patientId}/procedure-auths/{id}", new { ProcAuthId = id });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex, "submitting procedure prior-auth"); }
    }

    [HttpPost("patients/{patientId}/procedure-auths/{procAuthId}/pend")]
    public Task<IActionResult> Pend(string patientId, string procAuthId, [FromBody] PendProcedureAuthRequest req) =>
        Mutate(() => Workflow(patientId).PendProcedureAuthAsync(procAuthId, req.InfoRequested ?? string.Empty), "pending");

    [HttpPost("patients/{patientId}/procedure-auths/{procAuthId}/approve")]
    public Task<IActionResult> Approve(string patientId, string procAuthId, [FromBody] ApproveProcedureAuthRequest req) =>
        Mutate(() => Workflow(patientId).ApproveProcedureAuthAsync(procAuthId,
            req.ReviewerId ?? CurrentUser, req.ReviewerName ?? CurrentUser, req.AuthorizationNumber ?? string.Empty,
            req.ExpirationDate, req.CategoriesSatisfied ?? new()), "approving");

    [HttpPost("patients/{patientId}/procedure-auths/{procAuthId}/deny")]
    public Task<IActionResult> Deny(string patientId, string procAuthId, [FromBody] DenyProcedureAuthRequest req) =>
        Mutate(() => Workflow(patientId).DenyProcedureAuthAsync(procAuthId,
            req.ReviewerId ?? CurrentUser, req.ReviewerName ?? CurrentUser, req.DenialReasons ?? new()), "denying");

    [HttpPost("patients/{patientId}/procedure-auths/{procAuthId}/expire")]
    public Task<IActionResult> Expire(string patientId, string procAuthId) =>
        Mutate(() => Workflow(patientId).ExpireProcedureAuthAsync(procAuthId), "expiring");

    [HttpPost("patients/{patientId}/procedure-auths/{procAuthId}/cancel")]
    public Task<IActionResult> Cancel(string patientId, string procAuthId) =>
        Mutate(() => Workflow(patientId).CancelProcedureAuthAsync(procAuthId), "cancelling");

    // ─── Demo ──────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo([FromQuery] string patientId = "P-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            // Make sure Blue Cross Blue Shield of Florida is in the payer directory.
            await _grainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX").AddOrUpdateAsync(
                new PayerConfigIndexEntry { PayerId = "PAYER-BCBS-FL", PayerName = "Blue Cross Blue Shield of Florida", SupportsRealTimeEligibility = true, IsActive = true });

            IPatientWorkflowGrain wf = Workflow(patientId);

            // 1) A TKA PA phoned in to BCBS-FL and DENIED for missing conservative-therapy + imaging docs.
            string deniedId = await wf.SubmitProcedureAuthAsync(
                "27447", "Total knee arthroplasty", "PAYER-BCBS-FL", "Blue Cross Blue Shield of Florida",
                "PROV-007", "Dr. Sarah Lee", new List<string> { "M17.11" },
                "Severe primary osteoarthritis, right knee; failing function.", null, null,
                ProcedureAuthSubmissionChannel.Phone, null, null, null);
            await wf.DenyProcedureAuthAsync(deniedId, "UM-1", "UM Nurse", new List<ProcedureDenialReason>
            {
                new() { Category = PriorAuthRequirementCategory.ConservativeTherapyTrial, ReasonText = "No documented 3-month conservative therapy." },
                new() { Category = PriorAuthRequirementCategory.ImagingEvidence, ReasonText = "Weight-bearing radiographs not attached." }
            });

            // 2) Resubmitted with the missing docs → APPROVED (records the satisfied categories).
            string approvedId = await wf.SubmitProcedureAuthAsync(
                "27447", "Total knee arthroplasty", "PAYER-BCBS-FL", "Blue Cross Blue Shield of Florida",
                "PROV-007", "Dr. Sarah Lee", new List<string> { "M17.11" },
                "OA right knee; 4 months PT + NSAIDs + injections failed; WB films show grade-4 JSN.", null, null,
                ProcedureAuthSubmissionChannel.PayerPortal, null, null, null);
            await wf.ApproveProcedureAuthAsync(approvedId, "UM-1", "UM Nurse", "AUTH-BCBSFL-88213",
                new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                new List<PriorAuthRequirementCategory> { PriorAuthRequirementCategory.ConservativeTherapyTrial, PriorAuthRequirementCategory.ImagingEvidence });

            PriorAuthRequirementChecklist checklist = await wf.GetPriorAuthRequirementsAsync("27447", "PAYER-BCBS-FL");
            return Ok(new
            {
                message = "Procedure prior-auth demo loaded",
                patientId,
                deniedId,
                approvedId,
                checklistTopCategory = checklist.Items.FirstOrDefault()?.CategoryDisplay,
                observedDenials = checklist.ObservedDenialTotal
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading procedure prior-auth demo data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<IActionResult> Mutate(Func<Task> action, string verb)
    {
        try { await action(); return Ok(new { Message = "OK" }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { Error = ex.Message }); }
        catch (Exception ex) { return Fail(ex, verb + " procedure prior-auth"); }
    }

    private IActionResult Fail(Exception ex, string what)
    {
        _logger.LogError(ex, "Error {What}", what);
        return StatusCode(500, new { Error = $"An error occurred {what}." });
    }
}

public record SubmitProcedureAuthRequest
{
    public string CptCode { get; init; } = string.Empty;
    public string? CptDescription { get; init; }
    public string PayerId { get; init; } = string.Empty;
    public string? PayerName { get; init; }
    public string? OrderingProviderId { get; init; }
    public string? OrderingProviderName { get; init; }
    public List<string>? DiagnosisCodes { get; init; }
    public string? ClinicalJustification { get; init; }
    public DateTime? ServiceStartDate { get; init; }
    public DateTime? ServiceEndDate { get; init; }
    public ProcedureAuthSubmissionChannel Channel { get; init; } = ProcedureAuthSubmissionChannel.PayerPortal;
    public string? OrderId { get; init; }
    public string? ConsultId { get; init; }
    public string? ExternalReferralId { get; init; }
}

public record PendProcedureAuthRequest { public string? InfoRequested { get; init; } }

public record ApproveProcedureAuthRequest
{
    public string? ReviewerId { get; init; }
    public string? ReviewerName { get; init; }
    public string? AuthorizationNumber { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public List<PriorAuthRequirementCategory>? CategoriesSatisfied { get; init; }
}

public record DenyProcedureAuthRequest
{
    public string? ReviewerId { get; init; }
    public string? ReviewerName { get; init; }
    public List<ProcedureDenialReason>? DenialReasons { get; init; }
}
