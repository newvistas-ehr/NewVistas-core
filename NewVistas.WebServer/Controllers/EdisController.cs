// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// EDIS Controller — Emergency Department Integration Software.
/// Manages ED visits, triage, bed assignment, tracking board, and disposition.
/// </summary>
[ApiController]
[Route("api/edis")]
[Authorize]
public class EdisController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EdisController> _logger;

    public EdisController(IGrainFactory grainFactory, ILogger<EdisController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IEdBoardGrain GetBoardGrain(string facilityId = "MAIN")
        => _grainFactory.GetGrain<IEdBoardGrain>($"ED-BOARD:{facilityId}");

    // ─── Tracking Board ─────────────────────────────────────────────────────

    [HttpGet("board")]
    public async Task<ActionResult> GetBoard([FromQuery] string facilityId = "MAIN")
    {
        try
        {
            List<EdBoardEntry> visits = await GetBoardGrain(facilityId).GetActiveVisitsAsync();
            return Ok(visits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ED board");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("board/waiting")]
    public async Task<ActionResult> GetWaiting([FromQuery] string facilityId = "MAIN")
    {
        try
        {
            List<EdBoardEntry> waiting = await GetBoardGrain(facilityId).GetWaitingPatientsAsync();
            return Ok(waiting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting waiting patients");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("board/stats")]
    public async Task<ActionResult> GetStats([FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var board = GetBoardGrain(facilityId);
            EdBoardState state = await board.GetBoardAsync();
            int waiting = await board.GetWaitingCountAsync();
            return Ok(new { TotalBeds = state.TotalBeds, OccupiedBeds = state.OccupiedBeds,
                AvailableBeds = state.TotalBeds - state.OccupiedBeds,
                WaitingCount = waiting, ActiveCount = state.ActiveVisits.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ED stats");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Visit Management ───────────────────────────────────────────────────

    [HttpGet("visits/{visitId}")]
    public async Task<ActionResult> GetVisit(string visitId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            EdVisitState visit = await grain.GetVisitAsync();
            return Ok(visit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ED visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits")]
    public async Task<ActionResult> RegisterArrival([FromBody] EdArrivalRequest request)
    {
        try
        {
            string visitId = $"EDVIS-{Guid.NewGuid():N}";
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.RegisterArrivalAsync(
                request.PatientId, request.PatientName, request.ChiefComplaint,
                request.ArrivalMode ?? "WALK_IN", DateTime.UtcNow);

            await GetBoardGrain().AddOrUpdateVisitAsync(new EdBoardEntry
            {
                VisitId = visitId,
                PatientName = request.PatientName,
                ChiefComplaint = request.ChiefComplaint,
                AcuityLevel = 0,
                Status = "WAITING",
                ArrivalDateTime = DateTime.UtcNow,
                ArrivalMode = request.ArrivalMode ?? "WALK_IN"
            });

            _logger.LogInformation("ED visit {VisitId} registered for {PatientName}", visitId, request.PatientName);
            return Created("", new { VisitId = visitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering ED arrival");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits/{visitId}/triage")]
    public async Task<ActionResult> Triage(string visitId, [FromBody] EdTriageRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.TriageAsync(request.AcuityLevel, request.NurseId, request.NurseName);

            EdVisitState state = await grain.GetVisitAsync();
            await SyncBoardAsync(visitId, state);
            return Ok(new { Message = "Triaged." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triaging visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits/{visitId}/assign-bed")]
    public async Task<ActionResult> AssignBed(string visitId, [FromBody] EdAssignBedRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.AssignBedAsync(request.BedName);

            EdVisitState state = await grain.GetVisitAsync();
            await SyncBoardAsync(visitId, state);
            return Ok(new { Message = "Bed assigned." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning bed for visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits/{visitId}/assign-physician")]
    public async Task<ActionResult> AssignPhysician(string visitId, [FromBody] EdAssignStaffRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.AssignPhysicianAsync(request.StaffId, request.StaffName);

            EdVisitState state = await grain.GetVisitAsync();
            await SyncBoardAsync(visitId, state);
            return Ok(new { Message = "Physician assigned." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning physician for visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits/{visitId}/physician-seen")]
    public async Task<ActionResult> PhysicianSeen(string visitId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.RecordPhysicianSeenAsync();
            return Ok(new { Message = "Physician seen recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording physician seen {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits/{visitId}/discharge")]
    public async Task<ActionResult> Discharge(string visitId, [FromBody] EdDischargeRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.DischargeAsync(request.Diagnosis);
            await GetBoardGrain().RemoveVisitAsync(visitId);
            return Ok(new { Message = "Patient discharged from ED." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discharging visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("visits/{visitId}/admit")]
    public async Task<ActionResult> Admit(string visitId, [FromBody] EdAdmitRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
            await grain.AdmitAsync(request.DestinationWard);
            await GetBoardGrain().RemoveVisitAsync(visitId);
            return Ok(new { Message = "Patient admitted." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error admitting visit {VisitId}", visitId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ───────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    [AllowAnonymous]
    public async Task<ActionResult> LoadDemo()
    {
        try
        {
            var demoVisits = new[]
            {
                ("PATIENT-ED-001", "JONES,MICHAEL R", "Chest pain, radiating to left arm", "AMBULANCE", 2, "TRAUMA-1", "DR. MARTINEZ"),
                ("PATIENT-ED-002", "SMITH,SARAH L", "Laceration right forearm", "WALK_IN", 4, "FAST-TRACK-1", null as string),
                ("PATIENT-ED-003", "WILLIAMS,JAMES T", "Shortness of breath, fever", "WALK_IN", 3, "ED-BAY-3", "DR. CHEN"),
                ("PATIENT-ED-004", "BROWN,PATRICIA A", "Fall with hip pain", "AMBULANCE", 3, "ED-BAY-5", "DR. MARTINEZ"),
                ("PATIENT-ED-005", "DAVIS,ROBERT K", "Abdominal pain, nausea", "WALK_IN", 3, null as string, null as string),
                ("PATIENT-ED-006", "MILLER,JENNIFER M", "Headache, worst of life", "AMBULANCE", 2, "RESUS-1", "DR. PATEL"),
            };

            var board = GetBoardGrain();
            foreach (var (patId, name, complaint, mode, acuity, bed, doc) in demoVisits)
            {
                string visitId = $"EDVIS-DEMO-{Guid.NewGuid():N}";
                var grain = _grainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{visitId}");
                var arrival = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(10, 180));
                await grain.RegisterArrivalAsync(patId, name, complaint, mode, arrival);
                await grain.TriageAsync(acuity, "NURSE-ED-001", "TRIAGE NURSE");
                if (bed != null) await grain.AssignBedAsync(bed);
                if (doc != null) await grain.AssignPhysicianAsync("PROV-ED", doc);

                string status = bed != null ? "IN_TREATMENT" : "WAITING";
                await board.AddOrUpdateVisitAsync(new EdBoardEntry
                {
                    VisitId = visitId, PatientName = name, ChiefComplaint = complaint,
                    AcuityLevel = acuity, AssignedBed = bed, AttendingPhysicianName = doc,
                    Status = status, ArrivalDateTime = arrival, ArrivalMode = mode,
                    WaitMinutes = (int)(DateTime.UtcNow - arrival).TotalMinutes
                });
            }

            return Ok(new { Message = $"Loaded {demoVisits.Length} demo ED visits." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading ED demo data");
            return StatusCode(500, "An error occurred.");
        }
    }

    private async Task SyncBoardAsync(string visitId, EdVisitState state)
    {
        await GetBoardGrain().AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = visitId,
            PatientName = state.PatientName,
            ChiefComplaint = state.ChiefComplaint,
            AcuityLevel = state.AcuityLevel,
            AssignedBed = state.AssignedBed,
            AttendingPhysicianName = state.AttendingPhysicianName,
            Status = state.Status,
            ArrivalDateTime = state.ArrivalDateTime,
            ArrivalMode = state.ArrivalMode,
            WaitMinutes = (int)(DateTime.UtcNow - state.ArrivalDateTime).TotalMinutes
        });
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record EdArrivalRequest
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string ChiefComplaint { get; init; }
    public string? ArrivalMode { get; init; }
}

public record EdTriageRequest
{
    public required int AcuityLevel { get; init; }
    public required string NurseId { get; init; }
    public required string NurseName { get; init; }
}

public record EdAssignBedRequest { public required string BedName { get; init; } }
public record EdAssignStaffRequest { public required string StaffId { get; init; } public required string StaffName { get; init; } }
public record EdDischargeRequest { public required string Diagnosis { get; init; } }
public record EdAdmitRequest { public required string DestinationWard { get; init; } }
