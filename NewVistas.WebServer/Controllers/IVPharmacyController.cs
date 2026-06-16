// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize(Roles = "Pharmacist,Provider")]
[ApiController]
[Route("api/[controller]")]
public class IVPharmacyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IVPharmacyController> _logger;

    public IVPharmacyController(IGrainFactory grainFactory, ILogger<IVPharmacyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(Uri.UnescapeDataString(patientId));

    // ── Order Queries ─────────────────────────────────────────────────────────

    [HttpGet("{patientId}/orders")]
    public async Task<IActionResult> GetAllOrders(string patientId)
    {
        try
        {
            List<IVAdmixOrderIndexEntry> result = await GetWorkflow(patientId).GetIVAdmixOrdersAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving IV admix orders for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving IV admixture orders.");
        }
    }

    [HttpGet("{patientId}/orders/pending")]
    public async Task<IActionResult> GetPendingOrders(string patientId)
    {
        try
        {
            List<IVAdmixOrderIndexEntry> result = await GetWorkflow(patientId).GetPendingIVAdmixOrdersAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending IV admix orders for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving pending IV admixture orders.");
        }
    }

    [HttpGet("{patientId}/orders/active")]
    public async Task<IActionResult> GetActiveOrders(string patientId)
    {
        try
        {
            List<IVAdmixOrderIndexEntry> result = await GetWorkflow(patientId).GetActiveIVAdmixOrdersAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active IV admix orders for {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving active IV admixture orders.");
        }
    }

    [HttpGet("{patientId}/orders/{orderId}")]
    public async Task<IActionResult> GetOrder(string patientId, string orderId)
    {
        try
        {
            IVAdmixOrderState result = await GetWorkflow(patientId)
                .GetIVAdmixOrderAsync(Uri.UnescapeDataString(orderId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving IV admix order {OrderId} for {PatientId}", orderId, patientId);
            return StatusCode(500, "An error occurred retrieving the IV admixture order.");
        }
    }

    // ── Order Lifecycle ───────────────────────────────────────────────────────

    [HttpPost("{patientId}/orders")]
    public async Task<IActionResult> CreateOrder(string patientId, [FromBody] CreateIVAdmixOrderRequest req)
    {
        try
        {
            string orderId = await GetWorkflow(patientId).CreateIVAdmixOrderAsync(
                req.BaseSolution, req.BaseSolutionVolumeMl,
                req.Route, req.Frequency, req.ContainerType, req.ContainerCount, req.Priority,
                req.LinkedInpatientOrderId,
                req.InfusionRateStr, req.InfusionRateMlHr, req.InfusionDurationHours,
                req.RouteDescription, req.FrequencyDescription,
                req.StartDateTime, req.StopDateTime,
                req.ProviderId, req.ProviderName, req.Notes);
            return Created(
                $"/api/ivpharmacy/{Uri.EscapeDataString(patientId)}/orders/{Uri.EscapeDataString(orderId)}",
                new { orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating IV admix order for {PatientId}", patientId);
            return StatusCode(500, "An error occurred creating the IV admixture order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/additives")]
    public async Task<IActionResult> AddAdditive(string patientId, string orderId, [FromBody] IVAdmixAdditive additive)
    {
        try
        {
            await GetWorkflow(patientId).AddIVAdmixAdditiveAsync(Uri.UnescapeDataString(orderId), additive);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding additive to IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred adding the additive.");
        }
    }

    [HttpDelete("{patientId}/orders/{orderId}/additives/{drugName}")]
    public async Task<IActionResult> RemoveAdditive(string patientId, string orderId, string drugName)
    {
        try
        {
            await GetWorkflow(patientId).RemoveIVAdmixAdditiveAsync(
                Uri.UnescapeDataString(orderId), Uri.UnescapeDataString(drugName));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing additive from IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred removing the additive.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/verify")]
    public async Task<IActionResult> VerifyOrder(string patientId, string orderId, [FromBody] IVVerifyRequest req)
    {
        try
        {
            await GetWorkflow(patientId).VerifyIVAdmixOrderAsync(
                Uri.UnescapeDataString(orderId), req.PharmacistId, req.PharmacistName, req.VerifiedDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred verifying the order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/compound/start")]
    public async Task<IActionResult> StartCompounding(string patientId, string orderId, [FromBody] IVStartCompoundRequest req)
    {
        try
        {
            await GetWorkflow(patientId).StartIVAdmixCompoundingAsync(
                Uri.UnescapeDataString(orderId), req.CompoundedById, req.CompoundedByName, req.StartDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting compounding for IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred starting compounding.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/compound/complete")]
    public async Task<IActionResult> CompleteCompounding(string patientId, string orderId, [FromBody] IVCompleteCompoundRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CompleteIVAdmixCompoundingAsync(
                Uri.UnescapeDataString(orderId), req.CompletedDate, req.LotNumber, req.ExpirationDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing compounding for IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred completing compounding.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/label")]
    public async Task<IActionResult> PrintLabel(string patientId, string orderId, [FromBody] IVPrintLabelRequest req)
    {
        try
        {
            await GetWorkflow(patientId).PrintIVAdmixLabelAsync(
                Uri.UnescapeDataString(orderId), req.PrintedBy, req.PrintedDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing label for IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred printing the label.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/dispense")]
    public async Task<IActionResult> DispenseOrder(string patientId, string orderId, [FromBody] IVDispenseRequest req)
    {
        try
        {
            await GetWorkflow(patientId).DispenseIVAdmixOrderAsync(
                Uri.UnescapeDataString(orderId), req.DispensingDateTime);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispensing IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred dispensing the order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/administer")]
    public async Task<IActionResult> RecordAdministration(string patientId, string orderId, [FromBody] IVAdministerRequest req)
    {
        try
        {
            await GetWorkflow(patientId).RecordIVAdmixAdministrationAsync(
                Uri.UnescapeDataString(orderId), req.AdministrationDateTime);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording administration for IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred recording administration.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/discontinue")]
    public async Task<IActionResult> DiscontinueOrder(string patientId, string orderId, [FromBody] IVDiscontinueRequest req)
    {
        try
        {
            await GetWorkflow(patientId).DiscontinueIVAdmixOrderAsync(
                Uri.UnescapeDataString(orderId), req.Reason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred discontinuing the order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(string patientId, string orderId, [FromBody] IVCancelRequest req)
    {
        try
        {
            await GetWorkflow(patientId).CancelIVAdmixOrderAsync(
                Uri.UnescapeDataString(orderId), req.Reason);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling IV admix order {OrderId}", orderId);
            return StatusCode(500, "An error occurred cancelling the order.");
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateIVAdmixOrderRequest(
    string BaseSolution,
    int BaseSolutionVolumeMl,
    IVAdmixRoute Route,
    IVAdmixFrequency Frequency,
    IVContainerType ContainerType,
    int ContainerCount,
    IVAdmixPriority Priority,
    string? LinkedInpatientOrderId,
    string? InfusionRateStr,
    decimal? InfusionRateMlHr,
    decimal? InfusionDurationHours,
    string? RouteDescription,
    string? FrequencyDescription,
    DateTime? StartDateTime,
    DateTime? StopDateTime,
    string? ProviderId,
    string? ProviderName,
    string? Notes);

public record IVVerifyRequest(string PharmacistId, string PharmacistName, DateTime VerifiedDate);
public record IVStartCompoundRequest(string CompoundedById, string CompoundedByName, DateTime StartDate);
public record IVCompleteCompoundRequest(DateTime CompletedDate, string? LotNumber, DateTime? ExpirationDate);
public record IVPrintLabelRequest(string PrintedBy, DateTime PrintedDate);
public record IVDispenseRequest(DateTime DispensingDateTime);
public record IVAdministerRequest(DateTime AdministrationDateTime);
public record IVDiscontinueRequest(string Reason);
public record IVCancelRequest(string Reason);
