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
/// REST API for Integrated Billing (IB) — charge capture, copay account management,
/// billing clock, and patient insurance (Files #350–354, #355.x).
/// </summary>
[Authorize]
[ApiController]
[Route("api/integratedbilling")]
[Produces("application/json")]
public class IntegratedBillingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IntegratedBillingController> _logger;

    public IntegratedBillingController(
        IGrainFactory grainFactory,
        ILogger<IntegratedBillingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Billing Actions (File #350) ─────────────────────────────────────────

    [HttpGet("patients/{patientId}/billing-actions")]
    public async Task<IActionResult> GetBillingActions(string patientId)
    {
        try
        {
            return Ok(await W(patientId).GetBillingActionsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting billing actions for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving billing actions.");
        }
    }

    [HttpGet("patients/{patientId}/billing-actions/status/{status}")]
    public async Task<IActionResult> GetBillingActionsByStatus(
        string patientId,
        IBillingActionStatus status)
    {
        try
        {
            return Ok(await W(patientId).GetBillingActionsByStatusAsync(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting billing actions by status for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving billing actions.");
        }
    }

    [HttpGet("patients/{patientId}/billing-actions/{actionId}")]
    public async Task<IActionResult> GetBillingAction(string patientId, string actionId)
    {
        try
        {
            return Ok(await W(patientId).GetBillingActionAsync(actionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting billing action {ActionId} for {PatientId}", actionId, patientId);
            return StatusCode(500, "Error retrieving billing action.");
        }
    }

    [HttpPost("patients/{patientId}/billing-actions")]
    public async Task<IActionResult> RecordBillingAction(
        string patientId,
        [FromBody] RecordBillingActionRequest req)
    {
        try
        {
            string actionId = await W(patientId).RecordBillingActionAsync(
                req.ActionTypeCode, req.ActionTypeDescription, req.ActionCategory,
                req.ChargeAmount, req.ServiceDate,
                req.EnteredByUserId, req.EnteredByUserName,
                req.EncounterId, req.DiagnosisCode, req.ProcedureCode,
                req.LocationId, req.OrderId, req.PrescriptionId, req.Notes);
            return Created($"api/integratedbilling/patients/{patientId}/billing-actions/{actionId}", actionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording billing action for {PatientId}", patientId);
            return StatusCode(500, "Error recording billing action.");
        }
    }

    [HttpPost("patients/{patientId}/billing-actions/{actionId}/cancel")]
    public async Task<IActionResult> CancelBillingAction(
        string patientId,
        string actionId,
        [FromBody] CancelBillingActionRequest req)
    {
        try
        {
            await W(patientId).CancelBillingActionAsync(
                actionId, req.RemoveReasonCode, req.RemoveReasonDescription, req.RemovedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling billing action {ActionId} for {PatientId}", actionId, patientId);
            return StatusCode(500, "Error cancelling billing action.");
        }
    }

    // ─── Copay Account (File #354) ────────────────────────────────────────────

    [HttpGet("patients/{patientId}/copay-account")]
    public async Task<IActionResult> GetCopayAccount(string patientId)
    {
        try
        {
            return Ok(await W(patientId).GetPatientCopayAccountAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting copay account for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving copay account.");
        }
    }

    [HttpPost("patients/{patientId}/copay-account/exemption")]
    public async Task<IActionResult> SetCopayExemption(
        string patientId,
        [FromBody] SetCopayExemptionRequest req)
    {
        try
        {
            await W(patientId).SetCopayExemptionAsync(
                req.IsExempt, req.ReasonCode, req.EffectiveDate, req.ExpirationDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting copay exemption for {PatientId}", patientId);
            return StatusCode(500, "Error updating copay exemption.");
        }
    }

    // ─── Billing Clock (File #351) ────────────────────────────────────────────

    [HttpGet("patients/{patientId}/billing-clock")]
    public async Task<IActionResult> GetBillingClock(string patientId)
    {
        try
        {
            return Ok(await W(patientId).GetBillingClockAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting billing clock for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving billing clock.");
        }
    }

    [HttpPost("patients/{patientId}/billing-clock")]
    public async Task<IActionResult> SetBillingClock(
        string patientId,
        [FromBody] SetBillingClockRequest req)
    {
        try
        {
            await W(patientId).SetBillingClockAsync(
                req.ClockStatus, req.StartDate, req.ExpirationDate,
                req.MeansTestId, req.BillingCategory, req.PriorityGroup);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting billing clock for {PatientId}", patientId);
            return StatusCode(500, "Error updating billing clock.");
        }
    }

    // ─── Insurance — Personal Policies (File #355.7) ─────────────────────────

    [HttpGet("patients/{patientId}/insurance/policies")]
    public async Task<IActionResult> GetPersonalPolicies(string patientId)
    {
        try
        {
            return Ok(await W(patientId).GetPersonalPoliciesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting insurance policies for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving insurance policies.");
        }
    }

    [HttpGet("patients/{patientId}/insurance/policies/{policyId}")]
    public async Task<IActionResult> GetPersonalPolicy(string patientId, string policyId)
    {
        try
        {
            return Ok(await W(patientId).GetPersonalPolicyAsync(policyId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting policy {PolicyId} for {PatientId}", policyId, patientId);
            return StatusCode(500, "Error retrieving insurance policy.");
        }
    }

    [HttpPost("patients/{patientId}/insurance/policies")]
    public async Task<IActionResult> AddPersonalPolicy(
        string patientId,
        [FromBody] AddPersonalPolicyRequest req)
    {
        try
        {
            string policyId = await W(patientId).AddPersonalPolicyAsync(
                req.GroupPlanId, req.GroupPlanName, req.SubscriberId, req.SubscriberName,
                req.RelationshipToSubscriber, req.EffectiveDate, req.ExpirationDate,
                req.CoverageType, req.IsPrimary, req.CopayAmount, req.PharmacyMemberId, req.Notes);
            return Created($"api/integratedbilling/patients/{patientId}/insurance/policies/{policyId}", policyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding insurance policy for {PatientId}", patientId);
            return StatusCode(500, "Error adding insurance policy.");
        }
    }

    [HttpPost("patients/{patientId}/insurance/policies/{policyId}/deactivate")]
    public async Task<IActionResult> DeactivatePersonalPolicy(string patientId, string policyId)
    {
        try
        {
            await W(patientId).DeactivatePersonalPolicyAsync(policyId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating policy {PolicyId} for {PatientId}", policyId, patientId);
            return StatusCode(500, "Error deactivating insurance policy.");
        }
    }

    // ─── Insurance — Group Plans (File #355.3) ────────────────────────────────

    [HttpGet("insurance/plans/search")]
    public async Task<IActionResult> SearchInsurancePlans(
        [FromQuery] string? q,
        [FromQuery] string? planType,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            IInsurancePlanIndexGrain idx = _grainFactory.GetGrain<IInsurancePlanIndexGrain>("IB-PLAN-INDEX");
            return Ok(await idx.SearchAsync(q ?? string.Empty, planType, activeOnly, maxResults));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching insurance plans");
            return StatusCode(500, "Error searching insurance plans.");
        }
    }

    [HttpGet("insurance/plans/{planId}")]
    public async Task<IActionResult> GetInsurancePlan(string planId)
    {
        try
        {
            IInsurancePlanGrain grain = _grainFactory.GetGrain<IInsurancePlanGrain>($"IB-PLAN:{planId}");
            return Ok(await grain.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting insurance plan {PlanId}", planId);
            return StatusCode(500, "Error retrieving insurance plan.");
        }
    }

    [HttpPost("insurance/plans")]
    public async Task<IActionResult> CreateInsurancePlan([FromBody] CreateInsurancePlanRequest req)
    {
        try
        {
            string planId = Guid.NewGuid().ToString();
            IInsurancePlanGrain planGrain = _grainFactory.GetGrain<IInsurancePlanGrain>($"IB-PLAN:{planId}");
            await planGrain.CreateAsync(
                req.GroupPlanName, req.InsuranceCompanyName, req.PlanType,
                req.GroupNumber, req.CoverageType, req.EffectiveDate, req.ExpirationDate,
                req.CoinsurancePercent, req.DeductibleAmount, req.AnnualMaxBenefit,
                req.ClaimsAddress, req.ClaimsPhone, req.PharmacyBinNumber, req.PharmacyPcnNumber,
                req.FilingTimeFrameDays, req.IsPreCertRequired, req.AllowsElectronicVerification, req.Notes);

            // Add to global index
            IInsurancePlanIndexGrain idx = _grainFactory.GetGrain<IInsurancePlanIndexGrain>("IB-PLAN-INDEX");
            await idx.AddOrUpdateAsync(new InsurancePlanIndexEntry
            {
                PlanId               = planId,
                GroupPlanName        = req.GroupPlanName,
                InsuranceCompanyName = req.InsuranceCompanyName,
                PlanType             = req.PlanType,
                IsActive             = true,
            });

            return Created($"api/integratedbilling/insurance/plans/{planId}", planId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating insurance plan {PlanName}", req.GroupPlanName);
            return StatusCode(500, "Error creating insurance plan.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record RecordBillingActionRequest(
    string ActionTypeCode,
    string ActionTypeDescription,
    IBActionCategory ActionCategory,
    decimal? ChargeAmount,
    DateTime ServiceDate,
    string EnteredByUserId,
    string EnteredByUserName,
    string? EncounterId,
    string? DiagnosisCode,
    string? ProcedureCode,
    string? LocationId,
    string? OrderId,
    string? PrescriptionId,
    string? Notes);

public record CancelBillingActionRequest(
    string RemoveReasonCode,
    string RemoveReasonDescription,
    string RemovedByUserId);

public record SetCopayExemptionRequest(
    bool IsExempt,
    string? ReasonCode,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate);

public record SetBillingClockRequest(
    string ClockStatus,
    DateTime? StartDate,
    DateTime? ExpirationDate,
    string? MeansTestId,
    string? BillingCategory,
    string? PriorityGroup);

public record AddPersonalPolicyRequest(
    string? GroupPlanId,
    string GroupPlanName,
    string SubscriberId,
    string? SubscriberName,
    string? RelationshipToSubscriber,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate,
    string? CoverageType,
    bool IsPrimary,
    decimal? CopayAmount,
    string? PharmacyMemberId,
    string? Notes);

public record CreateInsurancePlanRequest(
    string GroupPlanName,
    string InsuranceCompanyName,
    string? PlanType,
    string? GroupNumber,
    string? CoverageType,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate,
    decimal? CoinsurancePercent,
    decimal? DeductibleAmount,
    decimal? AnnualMaxBenefit,
    string? ClaimsAddress,
    string? ClaimsPhone,
    string? PharmacyBinNumber,
    string? PharmacyPcnNumber,
    int? FilingTimeFrameDays,
    bool IsPreCertRequired,
    bool AllowsElectronicVerification,
    string? Notes);
