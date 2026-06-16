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
/// REST API for Insurance Eligibility Verification (EDI 270/271).
/// Manages real-time eligibility inquiries, payer configuration, and verification history.
/// Maps to VistA IB ELIGIBILITY VERIFICATION — IBCNEDE*.m, IBCNEUT.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/eligibility")]
[Produces("application/json")]
public class EligibilityVerificationController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EligibilityVerificationController> _logger;

    public EligibilityVerificationController(
        IGrainFactory grainFactory,
        ILogger<EligibilityVerificationController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Eligibility Inquiry (EDI 270/271) ──────────────────────────────────

    /// <summary>Submit a new eligibility inquiry (270) and receive simulated 271 response.</summary>
    [HttpPost("{patientId}/inquiries")]
    public async Task<IActionResult> SubmitInquiry(string patientId, [FromBody] SubmitEligibilityInquiryRequest req)
    {
        try
        {
            string inquiryId = await W(patientId).SubmitEligibilityInquiryAsync(
                req.InsurancePlanId, req.PersonalPolicyId,
                req.PayerId, req.PayerName,
                req.SubscriberId, req.SubscriberName,
                req.RelationshipToSubscriber, req.PatientDateOfBirth,
                req.ServiceTypeCodes ?? new List<string> { "30" },
                req.ServiceDate ?? DateTime.UtcNow,
                req.InitiatedByUserId, req.InitiatedByUserName,
                req.Notes);
            return Created("", new { InquiryId = inquiryId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting eligibility inquiry for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to submit eligibility inquiry.");
        }
    }

    /// <summary>Get a specific eligibility inquiry by ID.</summary>
    [HttpGet("{patientId}/inquiries/{inquiryId}")]
    public async Task<IActionResult> GetInquiry(string patientId, string inquiryId)
    {
        try
        {
            return Ok(await W(patientId).GetEligibilityInquiryAsync(inquiryId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eligibility inquiry {InquiryId} for patient {PatientId}", inquiryId, patientId);
            return StatusCode(500, "Failed to get eligibility inquiry.");
        }
    }

    /// <summary>Get all eligibility verification history for a patient.</summary>
    [HttpGet("{patientId}/history")]
    public async Task<IActionResult> GetHistory(string patientId)
    {
        try
        {
            return Ok(await W(patientId).GetEligibilityVerificationHistoryAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eligibility history for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to get eligibility history.");
        }
    }

    /// <summary>Get only eligible verifications for a patient.</summary>
    [HttpGet("{patientId}/eligible")]
    public async Task<IActionResult> GetEligible(string patientId)
    {
        try
        {
            return Ok(await W(patientId).GetEligibleVerificationsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eligible verifications for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to get eligible verifications.");
        }
    }

    /// <summary>Get the most recent verification for a specific payer.</summary>
    [HttpGet("{patientId}/latest/{payerName}")]
    public async Task<IActionResult> GetLatestForPayer(string patientId, string payerName)
    {
        try
        {
            EligibilityVerificationIndexEntry? entry = await W(patientId).GetLatestVerificationForPayerAsync(payerName);
            return entry is not null ? Ok(entry) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest verification for payer {PayerName}, patient {PatientId}", payerName, patientId);
            return StatusCode(500, "Failed to get latest verification.");
        }
    }

    // ─── Payer Configuration ────────────────────────────────────────────────

    /// <summary>Get all configured payers for eligibility verification.</summary>
    [HttpGet("payers")]
    public async Task<IActionResult> GetPayers()
    {
        try
        {
            IPayerConfigIndexGrain index = _grainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX");
            return Ok(await index.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payer configurations");
            return StatusCode(500, "Failed to get payer configurations.");
        }
    }

    /// <summary>Search payers by name.</summary>
    [HttpGet("payers/search")]
    public async Task<IActionResult> SearchPayers([FromQuery] string q)
    {
        try
        {
            IPayerConfigIndexGrain index = _grainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX");
            return Ok(await index.SearchAsync(q ?? string.Empty));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching payers");
            return StatusCode(500, "Failed to search payers.");
        }
    }

    /// <summary>Get payers supporting real-time 270/271 verification.</summary>
    [HttpGet("payers/realtime")]
    public async Task<IActionResult> GetRealTimePayers()
    {
        try
        {
            IPayerConfigIndexGrain index = _grainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX");
            return Ok(await index.GetRealTimePayersAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting real-time payers");
            return StatusCode(500, "Failed to get real-time payers.");
        }
    }

    /// <summary>Get a specific payer's configuration.</summary>
    [HttpGet("payers/{payerId}")]
    public async Task<IActionResult> GetPayerConfig(string payerId)
    {
        try
        {
            IPayerConfigGrain grain = _grainFactory.GetGrain<IPayerConfigGrain>($"PAYER-CFG:{payerId}");
            return Ok(await grain.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payer config {PayerId}", payerId);
            return StatusCode(500, "Failed to get payer configuration.");
        }
    }

    /// <summary>Create or update a payer configuration.</summary>
    [HttpPost("payers/{payerId}")]
    public async Task<IActionResult> CreatePayerConfig(string payerId, [FromBody] CreatePayerConfigRequest req)
    {
        try
        {
            IPayerConfigGrain grain = _grainFactory.GetGrain<IPayerConfigGrain>($"PAYER-CFG:{payerId}");
            await grain.CreateAsync(
                req.PayerName, req.SupportsRealTimeEligibility,
                req.InterchangeReceiverId, req.InterchangeReceiverQualifier,
                req.EligibilityEndpoint, req.EligibilityPhone,
                req.SupportedServiceTypeCodes, req.Notes);

            IPayerConfigIndexGrain index = _grainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX");
            await index.AddOrUpdateAsync(new PayerConfigIndexEntry
            {
                PayerId = payerId,
                PayerName = req.PayerName,
                SupportsRealTimeEligibility = req.SupportsRealTimeEligibility,
                IsActive = true,
            });

            return Created("", new { PayerId = payerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payer config {PayerId}", payerId);
            return StatusCode(500, "Failed to create payer configuration.");
        }
    }

    // ─── Demo Data ──────────────────────────────────────────────────────────

    /// <summary>Load demo eligibility verification data for a patient.</summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemoData([FromQuery] string patientId)
    {
        try
        {
            // Submit inquiries for multiple payers
            await W(patientId).SubmitEligibilityInquiryAsync(
                null, null, "PAYER-MEDICARE", "Medicare Part A/B",
                "1EG4-TE5-MK72", "PATIENT,TEST", "SELF", null,
                new List<string> { "30" }, DateTime.UtcNow,
                "DEMO-USER", "Demo User", "Demo eligibility check");

            await W(patientId).SubmitEligibilityInquiryAsync(
                null, null, "PAYER-BCBS", "Blue Cross Blue Shield",
                "XYZ123456789", "PATIENT,TEST", "SELF", null,
                new List<string> { "30", "1", "47" }, DateTime.UtcNow,
                "DEMO-USER", "Demo User", "Demo supplemental check");

            await W(patientId).SubmitEligibilityInquiryAsync(
                null, null, "PAYER-AETNA", "Aetna",
                "W999888777", "PATIENT,TEST", "SPOUSE", null,
                new List<string> { "30" }, DateTime.UtcNow.AddDays(-30),
                "DEMO-USER", "Demo User", "Demo prior-month check");

            return Ok(new { Message = "Demo eligibility verification data loaded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo eligibility data for {PatientId}", patientId);
            return StatusCode(500, "Failed to load demo data.");
        }
    }

    // ─── Request DTOs ───────────────────────────────────────────────────────

    public record SubmitEligibilityInquiryRequest
    {
        public string? InsurancePlanId { get; init; }
        public string? PersonalPolicyId { get; init; }
        public string PayerId { get; init; } = string.Empty;
        public string PayerName { get; init; } = string.Empty;
        public string SubscriberId { get; init; } = string.Empty;
        public string? SubscriberName { get; init; }
        public string? RelationshipToSubscriber { get; init; }
        public DateTime? PatientDateOfBirth { get; init; }
        public List<string>? ServiceTypeCodes { get; init; }
        public DateTime? ServiceDate { get; init; }
        public string? InitiatedByUserId { get; init; }
        public string? InitiatedByUserName { get; init; }
        public string? Notes { get; init; }
    }

    public record CreatePayerConfigRequest
    {
        public string PayerName { get; init; } = string.Empty;
        public bool SupportsRealTimeEligibility { get; init; }
        public string? InterchangeReceiverId { get; init; }
        public string? InterchangeReceiverQualifier { get; init; }
        public string? EligibilityEndpoint { get; init; }
        public string? EligibilityPhone { get; init; }
        public List<string>? SupportedServiceTypeCodes { get; init; }
        public string? Notes { get; init; }
    }
}
