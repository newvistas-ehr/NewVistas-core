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
/// Bed Management Controller — real-time bed availability, assignment, and census.
/// Maps to VistA DG Bed Control / File #405.4.
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

    private IBedBoardGrain GetBoard(string facilityId = "MAIN")
        => _grainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:{facilityId}");

    // ─── Bed Board ───────────────────────────────────────────────────────────

    [HttpGet("board")]
    public async Task<ActionResult> GetAllBeds([FromQuery] string facilityId = "MAIN")
    {
        try
        {
            List<BedSummaryEntry> beds = await GetBoard(facilityId).GetAllBedsAsync();
            return Ok(beds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bed board");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("board/ward/{wardId}")]
    public async Task<ActionResult> GetBedsByWard(string wardId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            List<BedSummaryEntry> beds = await GetBoard(facilityId).GetBedsByWardAsync(wardId);
            return Ok(beds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting beds for ward {WardId}", wardId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("board/available")]
    public async Task<ActionResult> GetAvailableBeds([FromQuery] string facilityId = "MAIN", [FromQuery] string? bedType = null)
    {
        try
        {
            List<BedSummaryEntry> beds = bedType != null
                ? await GetBoard(facilityId).GetAvailableBedsByTypeAsync(bedType)
                : await GetBoard(facilityId).GetAvailableBedsAsync();
            return Ok(beds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available beds");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("board/stats")]
    public async Task<ActionResult> GetStats([FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var board = GetBoard(facilityId);
            int total = await board.GetTotalBedCountAsync();
            int available = await board.GetAvailableBedCountAsync();
            int occupied = await board.GetOccupiedBedCountAsync();
            return Ok(new { TotalBeds = total, AvailableBeds = available, OccupiedBeds = occupied, OccupancyRate = total > 0 ? Math.Round((double)occupied / total * 100, 1) : 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bed stats");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Bed Actions ─────────────────────────────────────────────────────────

    [HttpGet("{bedId}")]
    public async Task<ActionResult> GetBed(string bedId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            BedState bed = await grain.GetBedAsync();
            return Ok(bed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/assign")]
    public async Task<ActionResult> AssignPatient(string bedId, [FromBody] BedAssignRequest request, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.AssignPatientAsync(request.PatientId, request.PatientName, request.ExpectedDischargeDate);

            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Patient assigned to bed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning patient to bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/discharge")]
    public async Task<ActionResult> DischargeBed(string bedId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.DischargePatientAsync();
            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Patient discharged, bed set to cleaning." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/reserve")]
    public async Task<ActionResult> ReserveBed(string bedId, [FromBody] BedReserveRequest request, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.ReserveForPatientAsync(request.PatientId, request.PatientName);
            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Bed reserved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/clear-reservation")]
    public async Task<ActionResult> ClearReservation(string bedId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.ClearReservationAsync();
            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Reservation cleared." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing reservation for bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/block")]
    public async Task<ActionResult> BlockBed(string bedId, [FromBody] BedBlockRequest request, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.BlockBedAsync(request.Reason);
            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Bed blocked." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/unblock")]
    public async Task<ActionResult> UnblockBed(string bedId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.UnblockBedAsync();
            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Bed unblocked." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking bed {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{bedId}/available")]
    public async Task<ActionResult> SetAvailable(string bedId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
            await grain.SetAvailableAsync();
            await SyncBoardAsync(facilityId, bedId, grain);
            return Ok(new { Message = "Bed set to available." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting bed available {BedId}", bedId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task SyncBoardAsync(string facilityId, string bedId, IBedGrain grain)
    {
        BedState state = await grain.GetBedAsync();
        await GetBoard(facilityId).AddOrUpdateBedAsync(new BedSummaryEntry
        {
            BedId = bedId,
            WardId = state.WardId,
            WardName = state.WardName,
            RoomNumber = state.RoomNumber,
            BedPosition = state.BedPosition,
            Status = state.Status,
            BedType = state.BedType,
            PatientName = state.PatientName,
            IsolationType = state.IsolationType
        });
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record BedAssignRequest
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public DateTime? ExpectedDischargeDate { get; init; }
}

public record BedReserveRequest { public required string PatientId { get; init; } public required string PatientName { get; init; } }
public record BedBlockRequest { public required string Reason { get; init; } }
