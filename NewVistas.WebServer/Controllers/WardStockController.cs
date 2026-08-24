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
/// Ward Stock Controller — Auto Replenishment / Ward Drug Inventory.
/// Maps to VistA PSGW package.
/// </summary>
[ApiController]
[Route("api/ward-stock")]
[Authorize]
public class WardStockController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<WardStockController> _logger;

    public WardStockController(IGrainFactory grainFactory, ILogger<WardStockController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    [HttpGet("{wardId}")]
    public async Task<ActionResult> GetWardStock(string wardId)
    {
        try
        {
            var index = _grainFactory.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{wardId}");
            List<WardStockSummaryEntry> items = await index.GetAllItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ward stock for {WardId}", wardId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("{wardId}/low-stock")]
    public async Task<ActionResult> GetLowStock(string wardId)
    {
        try
        {
            var index = _grainFactory.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{wardId}");
            List<WardStockSummaryEntry> items = await index.GetLowStockItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low stock for {WardId}", wardId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("{wardId}/{drugId}")]
    public async Task<ActionResult> GetItem(string wardId, string drugId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{wardId}:{drugId}");
            WardStockItemState item = await grain.GetItemAsync();
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ward stock item {WardId}:{DrugId}", wardId, drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{wardId}/setup")]
    public async Task<ActionResult> SetupItem(string wardId, [FromBody] WsSetupRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{wardId}:{request.DrugId}");
            await grain.SetupItemAsync(request.DrugId, request.DrugName, wardId, request.WardName,
                request.ParLevel, request.ReorderPoint, request.ReorderQuantity,
                request.UnitOfMeasure ?? "TAB", request.IsControlledSubstance);

            WardStockItemState state = await grain.GetItemAsync();
            await SyncIndexAsync(wardId, state);
            return Created("", new { Message = "Ward stock item set up." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up ward stock item");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{wardId}/{drugId}/dispense")]
    public async Task<ActionResult> Dispense(string wardId, string drugId, [FromBody] WsDispenseRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{wardId}:{drugId}");
            bool success = await grain.DispenseAsync(request.Quantity);
            if (!success) return BadRequest(new { Message = "Insufficient stock." });

            WardStockItemState state = await grain.GetItemAsync();
            await SyncIndexAsync(wardId, state);
            return Ok(new { Message = "Dispensed.", QuantityOnHand = state.QuantityOnHand });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispensing ward stock {WardId}:{DrugId}", wardId, drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{wardId}/{drugId}/receive")]
    public async Task<ActionResult> Receive(string wardId, string drugId, [FromBody] WsReceiveRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{wardId}:{drugId}");
            await grain.ReceiveAsync(request.Quantity);

            WardStockItemState state = await grain.GetItemAsync();
            await SyncIndexAsync(wardId, state);
            return Ok(new { Message = "Received.", QuantityOnHand = state.QuantityOnHand });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving ward stock {WardId}:{DrugId}", wardId, drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{wardId}/{drugId}/inventory-count")]
    public async Task<ActionResult> InventoryCount(string wardId, string drugId, [FromBody] WsInventoryCountRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{wardId}:{drugId}");
            await grain.RecordInventoryCountAsync(request.ActualCount);

            WardStockItemState state = await grain.GetItemAsync();
            await SyncIndexAsync(wardId, state);
            return Ok(new { Message = "Inventory count recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording inventory count");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Replenishment Log ───────────────────────────────────────────────────

    [HttpGet("{wardId}/replenishment-log")]
    public async Task<ActionResult> GetReplenishmentLog(string wardId)
    {
        try
        {
            var log = _grainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{wardId}");
            List<ReplenishmentRequest> requests = await log.GetRequestsAsync();
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting replenishment log for {WardId}", wardId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{wardId}/replenishment-log/{requestId}/fill")]
    public async Task<ActionResult> FillReplenishment(string wardId, string requestId)
    {
        try
        {
            var log = _grainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{wardId}");
            await log.FillRequestAsync(requestId);
            return Ok(new { Message = "Replenishment marked as filled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filling replenishment {RequestId}", requestId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    [AllowAnonymous]
    public async Task<ActionResult> LoadDemo([FromQuery] string wardId = "WARD-MED-3A")
    {
        try
        {
            await _grainFactory.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{wardId}").SeedDemoDataAsync();
            return Ok(new { Message = "Loaded demo ward stock items." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading ward stock demo data");
            return StatusCode(500, "An error occurred.");
        }
    }

    private async Task SyncIndexAsync(string wardId, WardStockItemState state)
    {
        var index = _grainFactory.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{wardId}");
        await index.AddOrUpdateAsync(new WardStockSummaryEntry
        {
            DrugId = state.DrugId,
            DrugName = state.DrugName,
            QuantityOnHand = state.QuantityOnHand,
            ParLevel = state.ParLevel,
            ReorderPoint = state.ReorderPoint,
            UnitOfMeasure = state.UnitOfMeasure,
            NeedsReplenishment = state.QuantityOnHand <= state.ReorderPoint,
            ReplenishmentPending = state.ReplenishmentPending,
            IsControlledSubstance = state.IsControlledSubstance
        });
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record WsSetupRequest
{
    public required string DrugId { get; init; }
    public required string DrugName { get; init; }
    public required string WardName { get; init; }
    public required int ParLevel { get; init; }
    public required int ReorderPoint { get; init; }
    public required int ReorderQuantity { get; init; }
    public string? UnitOfMeasure { get; init; }
    public bool IsControlledSubstance { get; init; }
}

public record WsDispenseRequest { public required int Quantity { get; init; } }
public record WsReceiveRequest { public required int Quantity { get; init; } }
public record WsInventoryCountRequest { public required int ActualCount { get; init; } }
