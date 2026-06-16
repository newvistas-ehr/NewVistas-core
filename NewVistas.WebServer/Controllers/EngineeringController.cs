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
/// Engineering API — facility management and work order tracking.
/// Based on VistA Engineering (ENG) package, Files #6914 and #6920.
/// MUMPS routines: ENSITE.m (facility/site), ENWORK.m (work order entry), ENWLIS.m (work order list).
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class EngineeringController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EngineeringController> _logger;

    public EngineeringController(IGrainFactory grainFactory, ILogger<EngineeringController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IEngineeringWorkOrderGrain WorkOrder(string workOrderId)
        => _grainFactory.GetGrain<IEngineeringWorkOrderGrain>(workOrderId);

    private IEngineeringWorkOrderIndexGrain WorkOrderIndex()
        => _grainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>("ENG-WO-IDX");

    private IFacilityGrain GetFacilityGrain(string facilityId)
        => _grainFactory.GetGrain<IFacilityGrain>(facilityId);

    private IFacilityIndexGrain FacilityIndex()
        => _grainFactory.GetGrain<IFacilityIndexGrain>("ENG-FAC-IDX");

    // ── Facility Endpoints ────────────────────────────────────────────────────

    /// <summary>Search facility records by name, building, department, or category.</summary>
    [HttpGet("api/engineering/facilities")]
    [ProducesResponseType(typeof(List<FacilityIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FacilityIndexEntry>>> GetFacilities(
        [FromQuery] string? search,
        [FromQuery] FacilityCategory? category,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            List<FacilityIndexEntry> results =
                await FacilityIndex().SearchAsync(search, category, activeOnly, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving facilities");
            return StatusCode(500, "An error occurred while retrieving facilities");
        }
    }

    /// <summary>Get a single facility by ID.</summary>
    [HttpGet("api/engineering/facilities/{facilityId}")]
    [ProducesResponseType(typeof(FacilityState), StatusCodes.Status200OK)]
    public async Task<ActionResult<FacilityState>> GetFacility(string facilityId)
    {
        try
        {
            FacilityState state = await GetFacilityGrain(facilityId).GetFacilityAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving facility {FacilityId}", facilityId);
            return StatusCode(500, "An error occurred while retrieving the facility");
        }
    }

    /// <summary>Create a new facility record.</summary>
    [HttpPost("api/engineering/facilities")]
    [ProducesResponseType(typeof(CreateFacilityResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFacility([FromBody] CreateFacilityRequest request)
    {
        try
        {
            string facilityId = $"ENG-FAC:{Guid.NewGuid()}";

            await GetFacilityGrain(facilityId).UpsertAsync(
                request.FacilityName,
                request.Category,
                request.Building,
                request.Floor,
                request.Room,
                request.DepartmentId,
                request.DepartmentName,
                request.EquipmentType,
                request.SerialNumber,
                request.Model,
                request.Manufacturer,
                request.InstallationDate,
                request.WarrantyExpirationDate,
                request.Description);

            FacilityState state = await GetFacilityGrain(facilityId).GetFacilityAsync();
            await FacilityIndex().AddOrUpdateAsync(new FacilityIndexEntry
            {
                FacilityId = facilityId,
                FacilityName = state.FacilityName,
                Category = state.Category,
                Building = state.Building,
                Floor = state.Floor,
                Room = state.Room,
                DepartmentName = state.DepartmentName,
                Status = state.Status,
                WorkOrderCount = 0,
            });

            return Created(
                $"api/engineering/facilities/{facilityId}",
                new CreateFacilityResponse(facilityId, "Facility created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating facility {FacilityName}", request.FacilityName);
            return StatusCode(500, "An error occurred while creating the facility");
        }
    }

    /// <summary>Update an existing facility record.</summary>
    [HttpPut("api/engineering/facilities/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFacility(
        string facilityId,
        [FromBody] CreateFacilityRequest request)
    {
        try
        {
            await GetFacilityGrain(facilityId).UpsertAsync(
                request.FacilityName,
                request.Category,
                request.Building,
                request.Floor,
                request.Room,
                request.DepartmentId,
                request.DepartmentName,
                request.EquipmentType,
                request.SerialNumber,
                request.Model,
                request.Manufacturer,
                request.InstallationDate,
                request.WarrantyExpirationDate,
                request.Description);

            FacilityState state = await GetFacilityGrain(facilityId).GetFacilityAsync();
            await FacilityIndex().AddOrUpdateAsync(new FacilityIndexEntry
            {
                FacilityId = facilityId,
                FacilityName = state.FacilityName,
                Category = state.Category,
                Building = state.Building,
                Floor = state.Floor,
                Room = state.Room,
                DepartmentName = state.DepartmentName,
                Status = state.Status,
                WorkOrderCount = state.WorkOrderCount,
            });

            return Ok(new { FacilityId = facilityId, Message = "Facility updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating facility {FacilityId}", facilityId);
            return StatusCode(500, "An error occurred while updating the facility");
        }
    }

    /// <summary>Mark a facility as under maintenance.</summary>
    [HttpPost("api/engineering/facilities/{facilityId}/maintenance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetFacilityMaintenance(string facilityId)
    {
        try
        {
            await GetFacilityGrain(facilityId).SetUnderMaintenanceAsync();
            FacilityState state = await GetFacilityGrain(facilityId).GetFacilityAsync();
            await FacilityIndex().AddOrUpdateAsync(BuildFacilityIndexEntry(facilityId, state));
            return Ok(new { FacilityId = facilityId, Message = "Facility set to under maintenance" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting facility {FacilityId} to maintenance", facilityId);
            return StatusCode(500, "An error occurred while updating the facility status");
        }
    }

    /// <summary>Restore a facility to active status.</summary>
    [HttpPost("api/engineering/facilities/{facilityId}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateFacility(string facilityId)
    {
        try
        {
            await GetFacilityGrain(facilityId).SetActiveAsync();
            FacilityState state = await GetFacilityGrain(facilityId).GetFacilityAsync();
            await FacilityIndex().AddOrUpdateAsync(BuildFacilityIndexEntry(facilityId, state));
            return Ok(new { FacilityId = facilityId, Message = "Facility activated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating facility {FacilityId}", facilityId);
            return StatusCode(500, "An error occurred while activating the facility");
        }
    }

    /// <summary>Decommission a facility — removes it from active service.</summary>
    [HttpPost("api/engineering/facilities/{facilityId}/decommission")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DecommissionFacility(string facilityId)
    {
        try
        {
            await GetFacilityGrain(facilityId).DecommissionAsync();
            FacilityState state = await GetFacilityGrain(facilityId).GetFacilityAsync();
            await FacilityIndex().AddOrUpdateAsync(BuildFacilityIndexEntry(facilityId, state));
            return Ok(new { FacilityId = facilityId, Message = "Facility decommissioned" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decommissioning facility {FacilityId}", facilityId);
            return StatusCode(500, "An error occurred while decommissioning the facility");
        }
    }

    // ── Work Order Endpoints ──────────────────────────────────────────────────

    /// <summary>
    /// Search all work orders with optional filters.
    /// Corresponds to VistA ENWLIS workload list.
    /// </summary>
    [HttpGet("api/engineering/work-orders")]
    [ProducesResponseType(typeof(List<WorkOrderIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WorkOrderIndexEntry>>> SearchWorkOrders(
        [FromQuery] string? facilityId,
        [FromQuery] EngineeringShop? shop,
        [FromQuery] WorkOrderStatus? status,
        [FromQuery] WorkOrderPriority? priority,
        [FromQuery] WorkOrderType? workOrderType,
        [FromQuery] string? assignedToId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            List<WorkOrderIndexEntry> results = await WorkOrderIndex().SearchAsync(
                facilityId, shop, status, priority, workOrderType,
                assignedToId, fromDate, toDate, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching work orders");
            return StatusCode(500, "An error occurred while searching work orders");
        }
    }

    /// <summary>Get the active workload — all open and in-progress work orders.</summary>
    [HttpGet("api/engineering/work-orders/active")]
    [ProducesResponseType(typeof(List<WorkOrderIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WorkOrderIndexEntry>>> GetActiveWorkOrders(
        [FromQuery] int maxResults = 200)
    {
        try
        {
            List<WorkOrderIndexEntry> results = await WorkOrderIndex().GetActiveAsync(maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active work orders");
            return StatusCode(500, "An error occurred while retrieving active work orders");
        }
    }

    /// <summary>Get a single work order by ID.</summary>
    [HttpGet("api/engineering/work-orders/{workOrderId}")]
    [ProducesResponseType(typeof(EngineeringWorkOrderState), StatusCodes.Status200OK)]
    public async Task<ActionResult<EngineeringWorkOrderState>> GetWorkOrder(string workOrderId)
    {
        try
        {
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while retrieving the work order");
        }
    }

    /// <summary>Create a new engineering work order.</summary>
    [HttpPost("api/engineering/work-orders")]
    [ProducesResponseType(typeof(CreateWorkOrderResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderRequest request)
    {
        try
        {
            string workOrderId = $"ENG-WO:{Guid.NewGuid()}";
            string workOrderNumber = $"WO-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..16];

            await WorkOrder(workOrderId).CreateAsync(
                workOrderNumber,
                request.FacilityId,
                request.FacilityName,
                request.LocationDescription,
                request.WorkOrderType,
                request.Priority,
                request.Shop,
                request.Description,
                request.RequestedById,
                request.RequestedByName,
                request.EstimatedHours,
                request.EstimatedPartsCost,
                request.ScheduledDate);

            // Increment the facility's work order counter and sync index
            await GetFacilityGrain(request.FacilityId).IncrementWorkOrderCountAsync();
            FacilityState facilityState = await GetFacilityGrain(request.FacilityId).GetFacilityAsync();
            await FacilityIndex().AddOrUpdateAsync(BuildFacilityIndexEntry(request.FacilityId, facilityState));

            // Add to work order index
            await WorkOrderIndex().AddOrUpdateAsync(new WorkOrderIndexEntry
            {
                WorkOrderId = workOrderId,
                WorkOrderNumber = workOrderNumber,
                FacilityId = request.FacilityId,
                FacilityName = request.FacilityName,
                LocationDescription = request.LocationDescription,
                WorkOrderType = request.WorkOrderType,
                Priority = request.Priority,
                Status = WorkOrderStatus.Open,
                Shop = request.Shop,
                AssignedToName = null,
                RequestedByName = request.RequestedByName,
                ScheduledDate = request.ScheduledDate,
                CreatedDate = DateTime.UtcNow,
            });

            return Created(
                $"api/engineering/work-orders/{workOrderId}",
                new CreateWorkOrderResponse(workOrderId, workOrderNumber, "Work order created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order for facility {FacilityId}", request.FacilityId);
            return StatusCode(500, "An error occurred while creating the work order");
        }
    }

    /// <summary>Assign a work order to a technician.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignWorkOrder(
        string workOrderId,
        [FromBody] AssignWorkOrderRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).AssignAsync(request.TechnicianId, request.TechnicianName);
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Work order assigned" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while assigning the work order");
        }
    }

    /// <summary>Start work on a work order — transitions to InProgress.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartWorkOrder(
        string workOrderId,
        [FromBody] AssignWorkOrderRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).StartAsync(request.TechnicianId, request.TechnicianName);
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Work order started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while starting the work order");
        }
    }

    /// <summary>Place a work order on hold.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/hold")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> HoldWorkOrder(
        string workOrderId,
        [FromBody] HoldWorkOrderRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).PlaceOnHoldAsync(request.Reason);
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Work order placed on hold" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing work order {WorkOrderId} on hold", workOrderId);
            return StatusCode(500, "An error occurred while placing the work order on hold");
        }
    }

    /// <summary>Resume a held work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumeWorkOrder(string workOrderId)
    {
        try
        {
            await WorkOrder(workOrderId).ResumeAsync();
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Work order resumed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while resuming the work order");
        }
    }

    /// <summary>Complete a work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteWorkOrder(
        string workOrderId,
        [FromBody] CompleteWorkOrderRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).CompleteAsync(request.CompletedDate ?? DateTime.UtcNow);
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Work order completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while completing the work order");
        }
    }

    /// <summary>Cancel a work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelWorkOrder(
        string workOrderId,
        [FromBody] CancelWorkOrderRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).CancelAsync(
                request.CancelledById, request.CancelledByName, request.Reason);
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Work order cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while cancelling the work order");
        }
    }

    /// <summary>Record labor time against a work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/labor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddLabor(
        string workOrderId,
        [FromBody] AddLaborRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).AddLaborAsync(
                request.TechnicianId,
                request.TechnicianName,
                request.HoursWorked,
                request.WorkDate,
                request.Notes);
            return Ok(new { WorkOrderId = workOrderId, Message = "Labor entry recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding labor to work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while recording the labor entry");
        }
    }

    /// <summary>Record parts/materials used on a work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/parts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPart(
        string workOrderId,
        [FromBody] AddPartRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).AddPartAsync(
                request.PartNumber,
                request.PartDescription,
                request.Quantity,
                request.UnitCost);
            return Ok(new { WorkOrderId = workOrderId, Message = "Part entry recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding part to work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while recording the part entry");
        }
    }

    /// <summary>Add a progress note to a work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/notes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddNote(
        string workOrderId,
        [FromBody] AddNoteRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).AddNoteAsync(
                request.AuthorId, request.AuthorName, request.NoteText);
            return Ok(new { WorkOrderId = workOrderId, Message = "Note added" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding note to work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while adding the note");
        }
    }

    /// <summary>Update the priority of a work order.</summary>
    [HttpPost("api/engineering/work-orders/{workOrderId}/priority")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePriority(
        string workOrderId,
        [FromBody] UpdatePriorityRequest request)
    {
        try
        {
            await WorkOrder(workOrderId).UpdatePriorityAsync(request.Priority);
            EngineeringWorkOrderState state = await WorkOrder(workOrderId).GetWorkOrderAsync();
            await WorkOrderIndex().AddOrUpdateAsync(BuildWorkOrderIndexEntry(workOrderId, state));
            return Ok(new { WorkOrderId = workOrderId, Message = "Priority updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating priority on work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while updating the priority");
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static WorkOrderIndexEntry BuildWorkOrderIndexEntry(
        string workOrderId,
        EngineeringWorkOrderState state)
        => new()
        {
            WorkOrderId = workOrderId,
            WorkOrderNumber = state.WorkOrderNumber,
            FacilityId = state.FacilityId,
            FacilityName = state.FacilityName,
            LocationDescription = state.LocationDescription,
            WorkOrderType = state.WorkOrderType,
            Priority = state.Priority,
            Status = state.Status,
            Shop = state.Shop,
            AssignedToName = state.AssignedToName,
            RequestedByName = state.RequestedByName,
            ScheduledDate = state.ScheduledDate,
            CreatedDate = state.CreatedDate,
        };

    private static FacilityIndexEntry BuildFacilityIndexEntry(
        string facilityId,
        FacilityState state)
        => new()
        {
            FacilityId = facilityId,
            FacilityName = state.FacilityName,
            Category = state.Category,
            Building = state.Building,
            Floor = state.Floor,
            Room = state.Room,
            DepartmentName = state.DepartmentName,
            Status = state.Status,
            WorkOrderCount = state.WorkOrderCount,
        };

    // ── Request / Response DTOs ───────────────────────────────────────────────

    public record CreateFacilityRequest(
        string FacilityName,
        FacilityCategory Category,
        string? Building,
        string? Floor,
        string? Room,
        string? DepartmentId,
        string? DepartmentName,
        string? EquipmentType,
        string? SerialNumber,
        string? Model,
        string? Manufacturer,
        DateTime? InstallationDate,
        DateTime? WarrantyExpirationDate,
        string? Description);

    public record CreateFacilityResponse(string FacilityId, string Message);

    public record CreateWorkOrderRequest(
        string FacilityId,
        string FacilityName,
        string? LocationDescription,
        WorkOrderType WorkOrderType,
        WorkOrderPriority Priority,
        EngineeringShop Shop,
        string Description,
        string RequestedById,
        string RequestedByName,
        decimal? EstimatedHours,
        decimal? EstimatedPartsCost,
        DateTime? ScheduledDate);

    public record CreateWorkOrderResponse(string WorkOrderId, string WorkOrderNumber, string Message);

    public record AssignWorkOrderRequest(string TechnicianId, string TechnicianName);

    public record HoldWorkOrderRequest(string? Reason);

    public record CompleteWorkOrderRequest(DateTime? CompletedDate);

    public record CancelWorkOrderRequest(
        string CancelledById,
        string CancelledByName,
        string? Reason);

    public record AddLaborRequest(
        string TechnicianId,
        string TechnicianName,
        decimal HoursWorked,
        DateTime WorkDate,
        string? Notes);

    public record AddPartRequest(
        string PartNumber,
        string PartDescription,
        int Quantity,
        decimal UnitCost);

    public record AddNoteRequest(
        string AuthorId,
        string AuthorName,
        string NoteText);

    public record UpdatePriorityRequest(WorkOrderPriority Priority);
}
