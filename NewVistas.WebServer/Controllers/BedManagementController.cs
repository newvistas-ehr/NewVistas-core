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
/// Bed Management Controller — institution-aware unit boards, capacity, EVS turnover,
/// and bed condition. Maps to VistA DGPM Bed Control / Files #42, #210, #405.4.
///
/// Bed truth lives on the unit grain ("UNIT:{institutionId}:{unitId}"); the per-institution
/// capacity grain is its rollup. Patient placement is NOT done here — admissions,
/// transfers, and discharges go through the ADT workflow (api/adt), which occupies and
/// releases beds atomically with the movement record.
/// </summary>
[ApiController]
[Route("api/beds")]
[Authorize]
public class BedManagementController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BedManagementController> _logger;

    public BedManagementController(IGrainFactory grainFactory, ILogger<BedManagementController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IBedCapacityGrain Capacity(string institutionId)
        => _grainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");

    private IInpatientUnitGrain Unit(string institutionId, string unitId)
        => _grainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    // ─── Capacity + directory ────────────────────────────────────────────────

    /// <summary>The institution's unit directory with live capacity counts.</summary>
    [HttpGet("institutions/{institutionId}/capacity")]
    public async Task<ActionResult> GetInstitutionCapacity(string institutionId)
    {
        try
        {
            List<UnitCapacitySummary> units = await Capacity(institutionId).GetUnitsAsync();
            (int total, int available, int occupied, int dirty, int blocked, int outOfService)
                = await Capacity(institutionId).GetInstitutionTotalsAsync();
            return Ok(new
            {
                InstitutionId = institutionId,
                TotalBeds = total,
                Available = available,
                Occupied = occupied,
                Dirty = dirty,
                Blocked = blocked,
                OutOfService = outOfService,
                OccupancyRate = total > 0 ? Math.Round((double)occupied / total * 100, 1) : 0,
                Units = units
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capacity for institution {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Full unit board: rooms + beds with lifecycle state, occupants, and reservations.</summary>
    [HttpGet("institutions/{institutionId}/units/{unitId}/board")]
    public async Task<ActionResult> GetUnitBoard(string institutionId, string unitId)
    {
        try
        {
            InpatientUnitState state = await Unit(institutionId, unitId).GetAsync();
            if (string.IsNullOrEmpty(state.Name))
                return NotFound($"Unit '{unitId}' not found at institution '{institutionId}'.");
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unit board {InstitutionId}/{UnitId}", institutionId, unitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Live unit census (occupied beds + boarders).</summary>
    [HttpGet("institutions/{institutionId}/units/{unitId}/census")]
    public async Task<ActionResult> GetUnitCensus(string institutionId, string unitId)
    {
        try
        {
            List<UnitCensusEntry> census = await Unit(institutionId, unitId).GetCensusAsync();
            return Ok(census);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting census {InstitutionId}/{UnitId}", institutionId, unitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Institution-wide EVS worklist: dirty/cleaning beds, oldest first, with isolation precautions.</summary>
    [HttpGet("institutions/{institutionId}/evs-queue")]
    public async Task<ActionResult> GetEvsQueue(string institutionId)
    {
        try
        {
            List<(string UnitId, DirtyBedEntry Bed)> queue = await Capacity(institutionId).GetDirtyBedQueueAsync();
            return Ok(queue.Select(x => new
            {
                x.UnitId,
                x.Bed.BedId,
                x.Bed.RoomId,
                State = x.Bed.State.ToString(),
                x.Bed.DirtySince,
                Isolation = x.Bed.Isolation.ToString()
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting EVS queue for institution {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Placeable (Available) beds, optionally filtered by unit or bed type.</summary>
    [HttpGet("institutions/{institutionId}/available")]
    public async Task<ActionResult> GetAvailableBeds(string institutionId,
        [FromQuery] string? unitId = null, [FromQuery] BedType? bedType = null)
    {
        try
        {
            // Directory first: only read units that actually have placeable beds.
            List<string> unitIds;
            if (!string.IsNullOrEmpty(unitId))
            {
                unitIds = new List<string> { unitId };
            }
            else
            {
                List<UnitCapacitySummary> units = await Capacity(institutionId).GetUnitsAsync();
                unitIds = units.Where(u => u.Available > 0).Select(u => u.UnitId).ToList();
            }

            var reads = unitIds.Select(id => Unit(institutionId, id).GetAsync()).ToList();
            InpatientUnitState[] states = await Task.WhenAll(reads);
            var beds = states
                .SelectMany(s => s.Beds.Select(b => new { s.UnitId, Bed = b }))
                .Where(x => x.Bed.State == BedLifecycleState.Available)
                .Where(x => bedType is null || x.Bed.BedType == bedType)
                .Select(x => new { x.UnitId, x.Bed.BedId, x.Bed.RoomId, BedType = x.Bed.BedType.ToString(), Isolation = x.Bed.Isolation.ToString() })
                .ToList();
            return Ok(beds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available beds for institution {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Unit structure (DG BED CONTROL enforced at the grain) ──────────────

    [HttpPost("institutions/{institutionId}/units/{unitId}")]
    public async Task<ActionResult> ConfigureUnit(string institutionId, string unitId, [FromBody] ConfigureUnitRequest request)
    {
        try
        {
            await Unit(institutionId, unitId).ConfigureUnitAsync(request.Name, request.UnitType, request.DefaultTreatingSpecialty);
            return Created($"api/beds/institutions/{institutionId}/units/{unitId}/board", new { Message = "Unit configured." });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring unit {InstitutionId}/{UnitId}", institutionId, unitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("institutions/{institutionId}/units/{unitId}/rooms")]
    public async Task<ActionResult> AddOrUpdateRoom(string institutionId, string unitId, [FromBody] InpatientRoom room)
    {
        try
        {
            await Unit(institutionId, unitId).AddOrUpdateRoomAsync(room);
            return Ok(new { Message = "Room saved." });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving room on {InstitutionId}/{UnitId}", institutionId, unitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds")]
    public async Task<ActionResult> AddBed(string institutionId, string unitId, [FromBody] AddBedRequest request)
    {
        try
        {
            await Unit(institutionId, unitId).AddBedAsync(request.BedId, request.RoomId, request.BedType);
            return Created($"api/beds/institutions/{institutionId}/units/{unitId}/board", new { Message = "Bed added." });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bed on {InstitutionId}/{UnitId}", institutionId, unitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Bed condition + EVS actions (keys enforced at the grain) ────────────

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/reserve")]
    public Task<ActionResult> Reserve(string institutionId, string unitId, string bedId, [FromBody] BedReserveRequest request)
        => BedAction(institutionId, unitId, bedId, "reserve",
            unit => unit.ReserveBedAsync(bedId, request.PatientId, request.PatientName, request.ExpiresAt));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/clear-reservation")]
    public Task<ActionResult> ClearReservation(string institutionId, string unitId, string bedId)
        => BedAction(institutionId, unitId, bedId, "clear-reservation",
            unit => unit.ClearReservationAsync(bedId));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/start-cleaning")]
    public Task<ActionResult> StartCleaning(string institutionId, string unitId, string bedId, [FromBody] EvsActionRequest? request = null)
        => BedAction(institutionId, unitId, bedId, "start-cleaning",
            unit => unit.StartCleaningAsync(bedId, request?.ByUserName));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/mark-clean")]
    public Task<ActionResult> MarkClean(string institutionId, string unitId, string bedId, [FromBody] EvsActionRequest? request = null)
        => BedAction(institutionId, unitId, bedId, "mark-clean",
            unit => unit.MarkBedCleanAsync(bedId, request?.ByUserName));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/mark-dirty")]
    public Task<ActionResult> MarkDirty(string institutionId, string unitId, string bedId)
        => BedAction(institutionId, unitId, bedId, "mark-dirty",
            unit => unit.MarkBedDirtyAsync(bedId));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/block")]
    public Task<ActionResult> Block(string institutionId, string unitId, string bedId, [FromBody] BedBlockRequest request)
        => BedAction(institutionId, unitId, bedId, "block",
            unit => unit.BlockBedAsync(bedId, request.Reason));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/unblock")]
    public Task<ActionResult> Unblock(string institutionId, string unitId, string bedId)
        => BedAction(institutionId, unitId, bedId, "unblock",
            unit => unit.UnblockBedAsync(bedId));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/out-of-service")]
    public Task<ActionResult> OutOfService(string institutionId, string unitId, string bedId, [FromBody] BedBlockRequest request)
        => BedAction(institutionId, unitId, bedId, "out-of-service",
            unit => unit.SetOutOfServiceAsync(bedId, request.Reason));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/return-to-service")]
    public Task<ActionResult> ReturnToService(string institutionId, string unitId, string bedId)
        => BedAction(institutionId, unitId, bedId, "return-to-service",
            unit => unit.ReturnToServiceAsync(bedId));

    [HttpPost("institutions/{institutionId}/units/{unitId}/beds/{bedId}/isolation")]
    public Task<ActionResult> SetIsolation(string institutionId, string unitId, string bedId, [FromBody] BedIsolationRequest request)
        => BedAction(institutionId, unitId, bedId, "isolation",
            unit => unit.SetBedIsolationAsync(bedId, request.Isolation));

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<ActionResult> BedAction(string institutionId, string unitId, string bedId,
        string action, Func<IInpatientUnitGrain, Task> operation)
    {
        try
        {
            await operation(Unit(institutionId, unitId));
            return Ok(new { Message = $"Bed {bedId}: {action} succeeded." });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on bed {BedId} action {Action} ({InstitutionId}/{UnitId})",
                bedId, action, institutionId, unitId);
            return StatusCode(500, "An error occurred.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ConfigureUnitRequest
{
    public required string Name { get; init; }
    public string? UnitType { get; init; }
    public string? DefaultTreatingSpecialty { get; init; }
}

public record AddBedRequest
{
    public required string BedId { get; init; }
    public string? RoomId { get; init; }
    public BedType BedType { get; init; } = BedType.Regular;
}

public record BedReserveRequest
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public record BedBlockRequest { public required string Reason { get; init; } }
public record EvsActionRequest { public string? ByUserName { get; init; } }
public record BedIsolationRequest { public BedIsolationType Isolation { get; init; } }
