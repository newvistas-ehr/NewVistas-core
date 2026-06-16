// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Beneficiary Travel Controller — travel pay claims processing.
/// Maps to VistA DGBT package.
/// </summary>
[ApiController]
[Route("api/beneficiary-travel")]
[Authorize]
public class BeneficiaryTravelController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BeneficiaryTravelController> _logger;

    public BeneficiaryTravelController(IGrainFactory grainFactory, ILogger<BeneficiaryTravelController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    [HttpGet("{patientId}/claims")]
    public async Task<ActionResult> GetClaims(string patientId, [FromQuery] string? status = null)
    {
        try
        {
            var index = _grainFactory.GetGrain<IBeneficiaryTravelIndexGrain>($"DGBT-INDEX:{patientId}");
            List<BeneficiaryTravelClaimEntry> claims = status != null
                ? await index.GetClaimsByStatusAsync(status)
                : await index.GetClaimsAsync();
            return Ok(claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting travel claims for {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("{patientId}/claims/{claimId}")]
    public async Task<ActionResult> GetClaim(string patientId, string claimId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
            BeneficiaryTravelClaimState claim = await grain.GetClaimAsync();
            return Ok(claim);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting travel claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/claims")]
    public async Task<ActionResult> FileClaim(string patientId, [FromBody] BtFileClaimRequest request)
    {
        try
        {
            string claimId = $"BT-{Guid.NewGuid():N}";
            var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
            await grain.CreateClaimAsync(
                patientId, request.PatientName, request.TravelDate,
                request.ClaimType ?? "MILEAGE", request.OneWayMileage, request.IsRoundTrip,
                request.TransportMode ?? "POV", request.OriginAddress, request.DestinationFacility,
                request.AppointmentId, request.EligibilityCode, request.DeductibleExempt);

            BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
            await SyncIndexAsync(patientId, state);

            _logger.LogInformation("Travel claim {ClaimId} filed for {PatientId}", claimId, patientId);
            return Created("", new { ClaimId = claimId, NetAmount = state.NetAmount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filing travel claim for {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/claims/{claimId}/approve")]
    public async Task<ActionResult> ApproveClaim(string patientId, string claimId, [FromBody] BtAuthorizeRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
            await grain.ApproveAsync(request.AuthorizedBy);
            await SyncIndexFromGrainAsync(patientId, claimId);
            return Ok(new { Message = "Claim approved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving travel claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/claims/{claimId}/deny")]
    public async Task<ActionResult> DenyClaim(string patientId, string claimId, [FromBody] BtDenyRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
            await grain.DenyAsync(request.Reason, request.AuthorizedBy);
            await SyncIndexFromGrainAsync(patientId, claimId);
            return Ok(new { Message = "Claim denied." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying travel claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/claims/{claimId}/pay")]
    public async Task<ActionResult> RecordPayment(string patientId, string claimId, [FromBody] BtPaymentRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
            await grain.RecordPaymentAsync(request.PaymentMethod ?? "EFT", request.PaymentDate ?? DateTime.UtcNow);
            await SyncIndexFromGrainAsync(patientId, claimId);
            return Ok(new { Message = "Payment recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording payment for claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{patientId}/claims/{claimId}/cancel")]
    public async Task<ActionResult> CancelClaim(string patientId, string claimId, [FromBody] BtCancelRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
            await grain.CancelAsync(request.Reason);
            await SyncIndexFromGrainAsync(patientId, claimId);
            return Ok(new { Message = "Claim cancelled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling travel claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    [AllowAnonymous]
    public async Task<ActionResult> LoadDemo([FromQuery] string patientId = "PATIENT-001")
    {
        try
        {
            var demoClaims = new[]
            {
                (DateTime.UtcNow.AddDays(-30), "MILEAGE", 25.4m, true, "POV", "SC", false, "APPROVED"),
                (DateTime.UtcNow.AddDays(-15), "MILEAGE", 25.4m, true, "POV", "SC", false, "PAID"),
                (DateTime.UtcNow.AddDays(-7), "COMMON_CARRIER", 0m, false, "BUS", "NSC", false, "PENDING"),
                (DateTime.UtcNow.AddDays(-2), "MILEAGE", 45.0m, true, "POV", "SC", true, "PENDING"),
            };

            foreach (var (date, type, miles, rt, mode, elig, exempt, status) in demoClaims)
            {
                string claimId = $"BT-DEMO-{Guid.NewGuid():N}";
                var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
                await grain.CreateClaimAsync(patientId, "JONES,MICHAEL R", date, type, miles, rt,
                    mode, "123 MAIN ST, ANYTOWN, ST 12345", "VA MEDICAL CENTER", null, elig, exempt);

                if (status == "APPROVED" || status == "PAID")
                    await grain.ApproveAsync("CLERK-001");
                if (status == "PAID")
                    await grain.RecordPaymentAsync("EFT", DateTime.UtcNow.AddDays(-1));

                BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
                await SyncIndexAsync(patientId, state);
            }

            return Ok(new { Message = $"Loaded {demoClaims.Length} demo travel claims." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading beneficiary travel demo data");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task SyncIndexAsync(string patientId, BeneficiaryTravelClaimState state)
    {
        var index = _grainFactory.GetGrain<IBeneficiaryTravelIndexGrain>($"DGBT-INDEX:{patientId}");
        await index.AddOrUpdateAsync(new BeneficiaryTravelClaimEntry
        {
            ClaimId = state.ClaimId,
            TravelDate = state.TravelDate,
            ClaimType = state.ClaimType,
            NetAmount = state.NetAmount,
            Status = state.Status,
            DestinationFacility = state.DestinationFacility,
            TotalMileage = state.TotalMileage
        });
    }

    private async Task SyncIndexFromGrainAsync(string patientId, string claimId)
    {
        var grain = _grainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        await SyncIndexAsync(patientId, state);
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record BtFileClaimRequest
{
    public required string PatientName { get; init; }
    public required DateTime TravelDate { get; init; }
    public string? ClaimType { get; init; }
    public required decimal OneWayMileage { get; init; }
    public bool IsRoundTrip { get; init; } = true;
    public string? TransportMode { get; init; }
    public string? OriginAddress { get; init; }
    public string? DestinationFacility { get; init; }
    public string? AppointmentId { get; init; }
    public string? EligibilityCode { get; init; }
    public bool DeductibleExempt { get; init; }
}

public record BtAuthorizeRequest { public required string AuthorizedBy { get; init; } }
public record BtDenyRequest { public required string Reason { get; init; } public required string AuthorizedBy { get; init; } }
public record BtPaymentRequest { public string? PaymentMethod { get; init; } public DateTime? PaymentDate { get; init; } }
public record BtCancelRequest { public required string Reason { get; init; } }
