// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implementation of <see cref="IEngineeringWorkOrderGrain"/>.
/// Persists all state for a single engineering work order.
/// </summary>
public class EngineeringWorkOrderGrain : Grain, IEngineeringWorkOrderGrain
{
    private readonly IPersistentState<EngineeringWorkOrderState> _state;

    public EngineeringWorkOrderGrain(
        [PersistentState("engWorkOrderState", "engWorkOrderStore")]
        IPersistentState<EngineeringWorkOrderState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.WorkOrderId))
        {
            _state.State.WorkOrderId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<EngineeringWorkOrderState> GetWorkOrderAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string workOrderNumber,
        string facilityId,
        string facilityName,
        string? locationDescription,
        WorkOrderType workOrderType,
        WorkOrderPriority priority,
        EngineeringShop shop,
        string description,
        string requestedById,
        string requestedByName,
        decimal? estimatedHours,
        decimal? estimatedPartsCost,
        DateTime? scheduledDate)
    {
        _state.State.WorkOrderNumber = workOrderNumber;
        _state.State.FacilityId = facilityId;
        _state.State.FacilityName = facilityName;
        _state.State.LocationDescription = locationDescription;
        _state.State.WorkOrderType = workOrderType;
        _state.State.Priority = priority;
        _state.State.Shop = shop;
        _state.State.Description = description;
        _state.State.RequestedById = requestedById;
        _state.State.RequestedByName = requestedByName;
        _state.State.EstimatedHours = estimatedHours;
        _state.State.EstimatedPartsCost = estimatedPartsCost;
        _state.State.ScheduledDate = scheduledDate;
        _state.State.Status = WorkOrderStatus.Open;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignAsync(string technicianId, string technicianName)
    {
        _state.State.AssignedToId = technicianId;
        _state.State.AssignedToName = technicianName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartAsync(string technicianId, string technicianName)
    {
        _state.State.AssignedToId = technicianId;
        _state.State.AssignedToName = technicianName;
        _state.State.Status = WorkOrderStatus.InProgress;
        _state.State.StartedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PlaceOnHoldAsync(string? reason)
    {
        _state.State.Status = WorkOrderStatus.OnHold;
        _state.State.HoldReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        _state.State.Status = WorkOrderStatus.InProgress;
        _state.State.HoldReason = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime completedDate)
    {
        _state.State.Status = WorkOrderStatus.Completed;
        _state.State.CompletedDate = completedDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string cancelledById, string cancelledByName, string? reason)
    {
        _state.State.Status = WorkOrderStatus.Cancelled;
        _state.State.CancelledById = cancelledById;
        _state.State.CancelledByName = cancelledByName;
        _state.State.CancellationReason = reason;
        _state.State.CancelledDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddLaborAsync(
        string technicianId,
        string technicianName,
        decimal hoursWorked,
        DateTime workDate,
        string? notes)
    {
        WoLaborEntry entry = new()
        {
            TechnicianId = technicianId,
            TechnicianName = technicianName,
            HoursWorked = hoursWorked,
            WorkDate = workDate,
            Notes = notes,
            EnteredDate = DateTime.UtcNow,
        };

        _state.State.LaborEntries.Add(entry);
        _state.State.ActualHours = _state.State.LaborEntries.Sum(e => e.HoursWorked);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddPartAsync(
        string partNumber,
        string partDescription,
        int quantity,
        decimal unitCost)
    {
        WoPartEntry entry = new()
        {
            PartNumber = partNumber,
            PartDescription = partDescription,
            Quantity = quantity,
            UnitCost = unitCost,
            AddedDate = DateTime.UtcNow,
        };

        _state.State.PartsEntries.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddNoteAsync(string authorId, string authorName, string noteText)
    {
        WoNoteEntry entry = new()
        {
            AuthorId = authorId,
            AuthorName = authorName,
            NoteText = noteText,
            EnteredDate = DateTime.UtcNow,
        };

        _state.State.Notes.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdatePriorityAsync(WorkOrderPriority priority)
    {
        _state.State.Priority = priority;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateScheduledDateAsync(DateTime? scheduledDate)
    {
        _state.State.ScheduledDate = scheduledDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
