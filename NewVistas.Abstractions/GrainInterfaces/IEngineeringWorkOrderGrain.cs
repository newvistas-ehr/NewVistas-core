// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Engineering Work Order grain — manages a single maintenance, repair, or installation work order.
/// Key format: "ENG-WO:{guid}".
/// Corresponds to VistA Engineering Work Order file (#6920).
/// MUMPS routines: ENWORK.m, ENWLIS.m.
/// </summary>
public interface IEngineeringWorkOrderGrain : IGrainWithStringKey
{
    /// <summary>Get the full work order state.</summary>
    Task<EngineeringWorkOrderState> GetWorkOrderAsync();

    /// <summary>
    /// Create a new work order. Sets status to Open and records CreatedDate.
    /// </summary>
    Task CreateAsync(
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
        DateTime? scheduledDate);

    /// <summary>
    /// Assign the work order to a technician or team.
    /// Does not change status (assignment can happen before or after start).
    /// </summary>
    Task AssignAsync(string technicianId, string technicianName);

    /// <summary>
    /// Start work — transitions status to InProgress and records StartedDate.
    /// </summary>
    Task StartAsync(string technicianId, string technicianName);

    /// <summary>
    /// Place the work order on hold with an optional reason.
    /// Transitions status to OnHold.
    /// </summary>
    Task PlaceOnHoldAsync(string? reason);

    /// <summary>
    /// Resume a held work order — transitions status back to InProgress.
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// Complete the work order — transitions status to Completed and records CompletedDate.
    /// </summary>
    Task CompleteAsync(DateTime completedDate);

    /// <summary>
    /// Cancel the work order with a reason.
    /// Transitions status to Cancelled and records cancellation details.
    /// </summary>
    Task CancelAsync(string cancelledById, string cancelledByName, string? reason);

    /// <summary>
    /// Add a labor time entry to the work order.
    /// Automatically updates ActualHours total.
    /// </summary>
    Task AddLaborAsync(
        string technicianId,
        string technicianName,
        decimal hoursWorked,
        DateTime workDate,
        string? notes);

    /// <summary>
    /// Add a parts/materials entry to the work order.
    /// </summary>
    Task AddPartAsync(
        string partNumber,
        string partDescription,
        int quantity,
        decimal unitCost);

    /// <summary>
    /// Add a progress note or comment to the work order.
    /// </summary>
    Task AddNoteAsync(string authorId, string authorName, string noteText);

    /// <summary>
    /// Update the priority of the work order.
    /// </summary>
    Task UpdatePriorityAsync(WorkOrderPriority priority);

    /// <summary>
    /// Update the scheduled date for the work order.
    /// </summary>
    Task UpdateScheduledDateAsync(DateTime? scheduledDate);
}
