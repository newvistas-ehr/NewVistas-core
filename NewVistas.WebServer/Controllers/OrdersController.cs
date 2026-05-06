// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Orders Controller — comprehensive order entry, management, order sets, and order checking.
/// Mirrors VistA CPRS Order Entry: ORWDX.m (save), ORWDXA.m (actions),
/// ORWORR.m (retrieval), ORWDXC.m (order checking), ORWDXM.m (order sets).
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IGrainFactory grainFactory, ILogger<OrdersController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Order CRUD ─────────────────────────────────────────────────────────

    /// <summary>GET api/orders/{patientId} — list orders with status filter.</summary>
    [HttpGet("{patientId}")]
    public async Task<ActionResult> GetOrders(string patientId, [FromQuery] int filter = 2)
    {
        try
        {
            List<OrderSummary> orders = await GetWorkflow(patientId).GetOrdersByFilterAsync(filter);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving orders.");
        }
    }

    /// <summary>GET api/orders/{patientId}/{orderId} — get order detail.</summary>
    [HttpGet("{patientId}/{orderId}")]
    public async Task<ActionResult> GetOrderDetail(string patientId, string orderId)
    {
        try
        {
            OrderState order = await GetWorkflow(patientId).GetOrderDetailAsync(orderId);
            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while retrieving the order.");
        }
    }

    /// <summary>POST api/orders/{patientId} — place a new order.</summary>
    [HttpPost("{patientId}")]
    public async Task<ActionResult> PlaceOrder(string patientId, [FromBody] OrdPlaceRequest request)
    {
        try
        {
            string orderId = await GetWorkflow(patientId).PlaceOrderAsync(
                request.OrderType, request.OrderText, request.OrderableItemId,
                request.ProviderId, request.ProviderName,
                request.LocationId, request.LocationName,
                request.Urgency ?? "ROUTINE", request.Instructions, request.Indication);

            _logger.LogInformation("Order {OrderId} placed for patient {PatientId}", orderId, patientId);
            return Created("", new { OrderId = orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while placing the order.");
        }
    }

    // ─── Order Actions ──────────────────────────────────────────────────────

    /// <summary>POST api/orders/{patientId}/{orderId}/sign — sign an order.</summary>
    [HttpPost("{patientId}/{orderId}/sign")]
    public async Task<ActionResult> SignOrder(string patientId, string orderId,
        [FromBody] OrdSignRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SignOrderAsync(orderId, request.ElectronicSignature);
            return Ok(new { Message = "Order signed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while signing the order.");
        }
    }

    /// <summary>POST api/orders/{patientId}/{orderId}/discontinue — discontinue an order.</summary>
    [HttpPost("{patientId}/{orderId}/discontinue")]
    public async Task<ActionResult> DiscontinueOrder(string patientId, string orderId,
        [FromBody] OrdDiscontinueRequest request)
    {
        try
        {
            await GetWorkflow(patientId).DiscontinueOrderAsync(orderId, request.Reason);
            return Ok(new { Message = "Order discontinued." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while discontinuing the order.");
        }
    }

    /// <summary>POST api/orders/{patientId}/{orderId}/hold — place order on hold.</summary>
    [HttpPost("{patientId}/{orderId}/hold")]
    public async Task<ActionResult> HoldOrder(string patientId, string orderId)
    {
        try
        {
            await GetWorkflow(patientId).HoldOrderAsync(orderId);
            return Ok(new { Message = "Order placed on hold." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error holding order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while holding the order.");
        }
    }

    /// <summary>POST api/orders/{patientId}/{orderId}/release — release held order.</summary>
    [HttpPost("{patientId}/{orderId}/release")]
    public async Task<ActionResult> ReleaseOrder(string patientId, string orderId)
    {
        try
        {
            await GetWorkflow(patientId).ReleaseOrderAsync(orderId);
            return Ok(new { Message = "Order released." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while releasing the order.");
        }
    }

    /// <summary>POST api/orders/{patientId}/{orderId}/renew — renew an order.</summary>
    [HttpPost("{patientId}/{orderId}/renew")]
    public async Task<ActionResult> RenewOrder(string patientId, string orderId,
        [FromBody] OrdRenewRequest request)
    {
        try
        {
            await GetWorkflow(patientId).RenewOrderAsync(orderId, request.ProviderId, request.NewStopDateTime);
            return Ok(new { Message = "Order renewed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while renewing the order.");
        }
    }

    /// <summary>POST api/orders/{patientId}/{orderId}/verify — nurse verification.</summary>
    [HttpPost("{patientId}/{orderId}/verify")]
    public async Task<ActionResult> VerifyOrder(string patientId, string orderId,
        [FromBody] OrdVerifyRequest request)
    {
        try
        {
            await GetWorkflow(patientId).VerifyOrderAsync(orderId, request.NurseId);
            return Ok(new { Message = "Order verified." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying order {OrderId}", orderId);
            return StatusCode(500, "An error occurred while verifying the order.");
        }
    }

    // ─── Order Checking ─────────────────────────────────────────────────────

    /// <summary>POST api/orders/{patientId}/check — check a proposed order for warnings.</summary>
    [HttpPost("{patientId}/check")]
    public async Task<ActionResult> CheckOrder(string patientId,
        [FromBody] OrdCheckRequest request)
    {
        try
        {
            List<OrderCheckResult> results = await GetWorkflow(patientId).CheckOrderAsync(
                request.OrderType, request.OrderText, request.OrderableItemId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking order for {PatientId}", patientId);
            return StatusCode(500, "An error occurred during order checking.");
        }
    }

    // ─── Order Sets ─────────────────────────────────────────────────────────

    /// <summary>GET api/orders/sets — list all order sets.</summary>
    [HttpGet("sets")]
    public async Task<ActionResult> GetOrderSets()
    {
        try
        {
            var index = _grainFactory.GetGrain<IOrderSetIndexGrain>("ORDSET-INDEX");
            List<OrderSetEntry> sets = await index.GetAllOrderSetsAsync();
            return Ok(sets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order sets");
            return StatusCode(500, "An error occurred while retrieving order sets.");
        }
    }

    /// <summary>GET api/orders/sets/search?q={query} — search order sets.</summary>
    [HttpGet("sets/search")]
    public async Task<ActionResult> SearchOrderSets([FromQuery] string q)
    {
        try
        {
            var index = _grainFactory.GetGrain<IOrderSetIndexGrain>("ORDSET-INDEX");
            List<OrderSetEntry> sets = await index.SearchOrderSetsAsync(q);
            return Ok(sets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching order sets");
            return StatusCode(500, "An error occurred while searching order sets.");
        }
    }

    /// <summary>GET api/orders/sets/{orderSetId} — get order set detail with templates.</summary>
    [HttpGet("sets/{orderSetId}")]
    public async Task<ActionResult> GetOrderSetDetail(string orderSetId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IOrderSetGrain>(orderSetId);
            OrderSetState set = await grain.GetOrderSetAsync();
            return Ok(set);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order set {OrderSetId}", orderSetId);
            return StatusCode(500, "An error occurred while retrieving the order set.");
        }
    }

    /// <summary>POST api/orders/{patientId}/execute-set — execute an order set.</summary>
    [HttpPost("{patientId}/execute-set")]
    public async Task<ActionResult> ExecuteOrderSet(string patientId,
        [FromBody] OrdExecuteSetRequest request)
    {
        try
        {
            List<string> orderIds = await GetWorkflow(patientId).ExecuteOrderSetAsync(
                request.OrderSetId,
                request.ProviderId, request.ProviderName,
                request.LocationId, request.LocationName,
                request.SelectedTemplateIds);

            _logger.LogInformation("Executed order set {SetId} for patient {PatientId}: {Count} orders created",
                request.OrderSetId, patientId, orderIds.Count);
            return Created("", new { OrderIds = orderIds, Count = orderIds.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing order set for {PatientId}", patientId);
            return StatusCode(500, "An error occurred while executing the order set.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public record OrdPlaceRequest
{
    public required string OrderType { get; init; }
    public required string OrderText { get; init; }
    public string? OrderableItemId { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? Urgency { get; init; }
    public string? Instructions { get; init; }
    public string? Indication { get; init; }
}

public record OrdSignRequest
{
    public required string ElectronicSignature { get; init; }
}

public record OrdDiscontinueRequest
{
    public required string Reason { get; init; }
}

public record OrdRenewRequest
{
    public required string ProviderId { get; init; }
    public DateTime? NewStopDateTime { get; init; }
}

public record OrdVerifyRequest
{
    public required string NurseId { get; init; }
}

public record OrdCheckRequest
{
    public required string OrderType { get; init; }
    public required string OrderText { get; init; }
    public string? OrderableItemId { get; init; }
}

public record OrdExecuteSetRequest
{
    public required string OrderSetId { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public string? LocationId { get; init; }
    public string? LocationName { get; init; }
    public List<string>? SelectedTemplateIds { get; init; }
}
