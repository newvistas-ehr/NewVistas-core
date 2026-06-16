// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Inpatient Pharmacy (PSJ) REST API — unit dose, IV admixture, and LVP orders.
/// Covers pharmacist verification, BCMA administration recording, and per-patient profile.
/// </summary>
[Authorize(Roles = "Pharmacist,Provider")]
[ApiController]
[Route("api/inpatientpharmacy")]
public class InpatientPharmacyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InpatientPharmacyController> _logger;

    public InpatientPharmacyController(IGrainFactory grainFactory, ILogger<InpatientPharmacyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // -------------------------------------------------------------------
    // List endpoints
    // -------------------------------------------------------------------

    [HttpGet("{patientId}/orders")]
    public async Task<IActionResult> GetOrders(string patientId)
    {
        try
        {
            List<InpatientOrderIndexEntry> orders = await GetProfileGrain(patientId).GetAllOrdersAsync();
            return Ok(orders.Select(ToSummaryDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inpatient orders for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve orders.");
        }
    }

    [HttpGet("{patientId}/orders/active")]
    public async Task<IActionResult> GetActiveOrders(string patientId)
    {
        try
        {
            List<InpatientOrderIndexEntry> orders = await GetProfileGrain(patientId).GetActiveOrdersAsync();
            return Ok(orders.Select(ToSummaryDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active inpatient orders for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve active orders.");
        }
    }

    [HttpGet("{patientId}/orders/pending")]
    public async Task<IActionResult> GetPendingOrders(string patientId)
    {
        try
        {
            List<InpatientOrderIndexEntry> orders = await GetProfileGrain(patientId).GetPendingVerificationAsync();
            return Ok(orders.Select(ToSummaryDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending orders for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve pending orders.");
        }
    }

    [HttpGet("{patientId}/orders/{orderId}")]
    public async Task<IActionResult> GetOrder(string patientId, string orderId)
    {
        try
        {
            InpatientOrderState state = await GetOrderGrain(orderId).GetOrderAsync();
            return Ok(ToDetailDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inpatient order {OrderId}", orderId);
            return StatusCode(500, "Failed to retrieve order.");
        }
    }

    // -------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------

    [HttpPost("{patientId}/orders")]
    public async Task<IActionResult> CreateOrder(string patientId, [FromBody] CreateOrderRequest request)
    {
        try
        {
            string orderId = $"PSJ-ORDER-{Guid.NewGuid():N}";
            IInpatientOrderGrain orderGrain = GetOrderGrain(orderId);

            await orderGrain.CreateOrderAsync(
                patientId,
                request.WardId ?? string.Empty,
                request.WardName ?? string.Empty,
                request.RoomBed,
                request.OrderType ?? "UNIT_DOSE",
                request.DrugName,
                request.DrugId,
                request.Dosage,
                request.DoseUnit,
                request.Route,
                request.Schedule,
                request.Priority ?? "ROUTINE",
                request.StartDate,
                request.StopDate,
                request.DurationDays,
                request.QuantityPerDose,
                request.ProviderId,
                request.ProviderName,
                request.Comments,
                request.IvSolution,
                request.IvVolumeMl,
                request.InfusionRateStr);

            await SyncProfile(patientId, orderId);
            return Created($"api/inpatientpharmacy/{patientId}/orders/{orderId}", new { OrderId = orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inpatient order for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to create order.");
        }
    }

    // -------------------------------------------------------------------
    // Lifecycle actions
    // -------------------------------------------------------------------

    [HttpPost("{patientId}/orders/{orderId}/verify")]
    public async Task<IActionResult> Verify(string patientId, string orderId, [FromBody] IpVerifyRequest request)
    {
        try
        {
            await GetOrderGrain(orderId).VerifyAsync(request.PharmacistId ?? "UNKNOWN", request.PharmacistName);
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying order {OrderId}", orderId);
            return StatusCode(500, "Failed to verify order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/discontinue")]
    public async Task<IActionResult> Discontinue(string patientId, string orderId, [FromBody] IpDiscontinueRequest request)
    {
        try
        {
            await GetOrderGrain(orderId).DiscontinueAsync(request.Reason ?? string.Empty);
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing order {OrderId}", orderId);
            return StatusCode(500, "Failed to discontinue order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/hold")]
    public async Task<IActionResult> Hold(string patientId, string orderId, [FromBody] IpHoldRequest request)
    {
        try
        {
            await GetOrderGrain(orderId).HoldAsync(request.Reason ?? string.Empty);
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order {OrderId} on hold", orderId);
            return StatusCode(500, "Failed to hold order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/resume")]
    public async Task<IActionResult> Resume(string patientId, string orderId)
    {
        try
        {
            await GetOrderGrain(orderId).ResumeAsync();
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming order {OrderId}", orderId);
            return StatusCode(500, "Failed to resume order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/expire")]
    public async Task<IActionResult> Expire(string patientId, string orderId)
    {
        try
        {
            await GetOrderGrain(orderId).ExpireAsync();
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error expiring order {OrderId}", orderId);
            return StatusCode(500, "Failed to expire order.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/additive")]
    public async Task<IActionResult> AddAdditive(string patientId, string orderId, [FromBody] AddAdditiveRequest request)
    {
        try
        {
            IvAdditive additive = new()
            {
                DrugName = request.DrugName,
                DrugId = request.DrugId,
                Dose = request.Dose ?? string.Empty,
                DoseUnit = request.DoseUnit ?? string.Empty,
                IsBase = request.IsBase
            };
            await GetOrderGrain(orderId).AddIvAdditiveAsync(additive);
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding IV additive to order {OrderId}", orderId);
            return StatusCode(500, "Failed to add additive.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/schedule")]
    public async Task<IActionResult> AddScheduledTime(string patientId, string orderId, [FromBody] AddScheduleRequest request)
    {
        try
        {
            await GetOrderGrain(orderId).AddScheduledTimeAsync(request.ScheduledTime);
            await SyncProfile(patientId, orderId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding scheduled time to order {OrderId}", orderId);
            return StatusCode(500, "Failed to add scheduled time.");
        }
    }

    [HttpPost("{patientId}/orders/{orderId}/administered")]
    public async Task<IActionResult> RecordAdministration(string patientId, string orderId, [FromBody] RecordAdminRequest request)
    {
        try
        {
            string bcmaId = request.BcmaId ?? $"BCMA-{Guid.NewGuid():N}";
            DateTime adminDate = request.AdminDate ?? DateTime.UtcNow;
            await GetOrderGrain(orderId).RecordAdministrationAsync(bcmaId, adminDate);
            await SyncProfile(patientId, orderId);
            return Ok(new { BcmaId = bcmaId, AdminDate = adminDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording administration for order {OrderId}", orderId);
            return StatusCode(500, "Failed to record administration.");
        }
    }

    // -------------------------------------------------------------------
    // Demo data
    // -------------------------------------------------------------------

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemoData([FromQuery] string patientId = "P-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientInpatientProfileGrain profileGrain = GetProfileGrain(patientId);
            await profileGrain.ClearAsync();

            List<string> created = new();

            // 1. VANCOMYCIN 1GM IV Q12H — ACTIVE, verified
            string id1 = await CreateDemoOrder(patientId,
                "WARD-4B", "4 NORTH", "4B-12", "IV",
                "VANCOMYCIN 1GM/NS 250ML", "50-VANC", "1", "GM",
                "IV", "Q12H", "STAT",
                "NS", 250, "over 60 min",
                "PROV-001", "DR. JOHNSON",
                new[] { new IvAdditive { DrugName = "VANCOMYCIN", DrugId = "50-VANC", Dose = "1", DoseUnit = "GM", IsBase = true } },
                verifyPharmacist: true, holdReason: null, isPending: false);
            created.Add(id1);

            // 2. METOPROLOL 25MG TABLET QD — ACTIVE, verified
            string id2 = await CreateDemoOrder(patientId,
                "WARD-4B", "4 NORTH", "4B-12", "UNIT_DOSE",
                "METOPROLOL TARTRATE 25MG TAB", "50-METOP", "25", "MG",
                "ORAL", "BID", "ROUTINE",
                null, null, null,
                "PROV-001", "DR. JOHNSON",
                null,
                verifyPharmacist: true, holdReason: null, isPending: false);
            created.Add(id2);

            // 3. NS 0.9% 1000ML LVP 125ML/HR — ACTIVE, verified
            string id3 = await CreateDemoOrder(patientId,
                "WARD-4B", "4 NORTH", "4B-12", "LVP",
                "SODIUM CHLORIDE 0.9% 1000ML", null, "1000", "ML",
                "IV", "CONTINUOUS", "ROUTINE",
                "NS 0.9%", 1000, "125 mL/hr",
                "PROV-002", "DR. PATEL",
                null,
                verifyPharmacist: true, holdReason: null, isPending: false);
            created.Add(id3);

            // 4. ASPIRIN 81MG QD — HOLD
            string id4 = await CreateDemoOrder(patientId,
                "WARD-4B", "4 NORTH", "4B-12", "UNIT_DOSE",
                "ASPIRIN 81MG TAB EC", "50-ASA", "81", "MG",
                "ORAL", "QD", "ROUTINE",
                null, null, null,
                "PROV-001", "DR. JOHNSON",
                null,
                verifyPharmacist: true, holdReason: "Pending cardiology clearance", isPending: false);
            created.Add(id4);

            // 5. DEXAMETHASONE 4MG IV Q6H — PENDING (not yet verified)
            string id5 = await CreateDemoOrder(patientId,
                "WARD-4B", "4 NORTH", "4B-12", "IV",
                "DEXAMETHASONE 4MG/NS 50ML", "50-DEXA", "4", "MG",
                "IV", "Q6H", "STAT",
                "NS", 50, "over 15 min",
                "PROV-002", "DR. PATEL",
                new[] { new IvAdditive { DrugName = "DEXAMETHASONE", DrugId = "50-DEXA", Dose = "4", DoseUnit = "MG", IsBase = true } },
                verifyPharmacist: false, holdReason: null, isPending: true);
            created.Add(id5);

            return Created("api/inpatientpharmacy/demo/load",
                new { PatientId = patientId, Created = created.Count, OrderIds = created });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading inpatient pharmacy demo data");
            return StatusCode(500, "Failed to load demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private IInpatientOrderGrain GetOrderGrain(string orderId) =>
        _grainFactory.GetGrain<IInpatientOrderGrain>(orderId);

    private IPatientInpatientProfileGrain GetProfileGrain(string patientId) =>
        _grainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

    private async Task SyncProfile(string patientId, string orderId)
    {
        InpatientOrderState state = await GetOrderGrain(orderId).GetOrderAsync();
        InpatientOrderIndexEntry entry = new()
        {
            OrderId = orderId,
            DrugName = state.DrugName,
            DrugId = state.DrugId,
            OrderType = state.OrderType,
            Status = state.Status,
            Priority = state.Priority,
            Schedule = state.Schedule,
            Dosage = state.Dosage,
            StartDate = state.StartDate,
            StopDate = state.StopDate,
            IsVerified = state.IsVerified,
            ProviderName = state.ProviderName,
            WardName = state.WardName,
            RoomBed = state.RoomBed,
            LastAdminDate = state.LastAdminDate
        };
        await GetProfileGrain(patientId).AddOrUpdateOrderAsync(entry);
    }

    private async Task<string> CreateDemoOrder(
        string patientId, string wardId, string wardName, string? roomBed,
        string orderType, string drugName, string? drugId, string? dosage, string? doseUnit,
        string? route, string? schedule, string priority,
        string? ivSolution, int? ivVolumeMl, string? infusionRateStr,
        string? providerId, string? providerName,
        IvAdditive[]? additives,
        bool verifyPharmacist, string? holdReason, bool isPending)
    {
        string orderId = $"PSJ-ORDER-{Guid.NewGuid():N}";
        IInpatientOrderGrain grain = GetOrderGrain(orderId);

        await grain.CreateOrderAsync(
            patientId, wardId, wardName, roomBed, orderType, drugName, drugId,
            dosage, doseUnit, route, schedule, priority,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(5), 5, 1,
            providerId, providerName, null,
            ivSolution, ivVolumeMl, infusionRateStr);

        if (additives != null)
            foreach (IvAdditive additive in additives)
                await grain.AddIvAdditiveAsync(additive);

        if (verifyPharmacist && !isPending)
            await grain.VerifyAsync("RPH-001", "JANE DOE, RPH");

        if (!string.IsNullOrEmpty(holdReason))
            await grain.HoldAsync(holdReason);

        await SyncProfile(patientId, orderId);
        return orderId;
    }

    private static OrderSummaryDto ToSummaryDto(InpatientOrderIndexEntry e) =>
        new(e.OrderId, e.DrugName, e.DrugId, e.OrderType, e.Status, e.Priority,
            e.Schedule, e.Dosage, e.StartDate, e.StopDate, e.IsVerified,
            e.ProviderName, e.WardName, e.RoomBed, e.LastAdminDate);

    private static OrderDetailDto ToDetailDto(InpatientOrderState s) =>
        new(s.OrderId, s.PatientId, s.AdmissionId, s.WardId, s.WardName, s.RoomBed,
            s.OrderType, s.DrugName, s.DrugId, s.Dosage, s.DoseUnit, s.Route,
            s.Schedule, s.Status, s.Priority, s.StartDate, s.StopDate, s.DurationDays,
            s.QuantityPerDose, s.IsVerified, s.VerifiedBy, s.VerifiedDate,
            s.ProviderId, s.ProviderName, s.PharmacistId, s.PharmacistName,
            s.Comments, s.DiscontinueReason, s.HoldReason,
            s.IvSolution, s.IvVolumeMl, s.InfusionRateStr,
            s.IvAdditives.Select(a => new IvAdditiveDto(a.DrugName, a.DrugId, a.Dose, a.DoseUnit, a.IsBase)).ToList(),
            s.ScheduledAdminTimes, s.BcmaAdministrationIds,
            s.LastAdminDate, s.CreatedDate, s.LastModifiedDate);
}

// -------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------

public record OrderSummaryDto(
    string OrderId,
    string DrugName,
    string? DrugId,
    string OrderType,
    string Status,
    string Priority,
    string? Schedule,
    string? Dosage,
    DateTime? StartDate,
    DateTime? StopDate,
    bool IsVerified,
    string? ProviderName,
    string? WardName,
    string? RoomBed,
    DateTime? LastAdminDate);

public record OrderDetailDto(
    string OrderId,
    string PatientId,
    string? AdmissionId,
    string WardId,
    string WardName,
    string? RoomBed,
    string OrderType,
    string DrugName,
    string? DrugId,
    string? Dosage,
    string? DoseUnit,
    string? Route,
    string? Schedule,
    string Status,
    string Priority,
    DateTime? StartDate,
    DateTime? StopDate,
    int? DurationDays,
    int? QuantityPerDose,
    bool IsVerified,
    string? VerifiedBy,
    DateTime? VerifiedDate,
    string? ProviderId,
    string? ProviderName,
    string? PharmacistId,
    string? PharmacistName,
    string? Comments,
    string? DiscontinueReason,
    string? HoldReason,
    string? IvSolution,
    int? IvVolumeMl,
    string? InfusionRateStr,
    List<IvAdditiveDto> IvAdditives,
    List<DateTime> ScheduledAdminTimes,
    List<string> BcmaAdministrationIds,
    DateTime? LastAdminDate,
    DateTime CreatedDate,
    DateTime LastModifiedDate);

public record IvAdditiveDto(
    string DrugName,
    string? DrugId,
    string Dose,
    string DoseUnit,
    bool IsBase);

public record CreateOrderRequest(
    string DrugName,
    string? DrugId,
    string? OrderType,
    string? WardId,
    string? WardName,
    string? RoomBed,
    string? Dosage,
    string? DoseUnit,
    string? Route,
    string? Schedule,
    string? Priority,
    DateTime? StartDate,
    DateTime? StopDate,
    int? DurationDays,
    int? QuantityPerDose,
    string? ProviderId,
    string? ProviderName,
    string? Comments,
    string? IvSolution,
    int? IvVolumeMl,
    string? InfusionRateStr);

public record IpVerifyRequest(string? PharmacistId, string? PharmacistName);
public record IpDiscontinueRequest(string? Reason);
public record IpHoldRequest(string? Reason);
public record AddAdditiveRequest(string DrugName, string? DrugId, string? Dose, string? DoseUnit, bool IsBase);
public record AddScheduleRequest(DateTime ScheduledTime);
public record RecordAdminRequest(string? BcmaId, DateTime? AdminDate);
