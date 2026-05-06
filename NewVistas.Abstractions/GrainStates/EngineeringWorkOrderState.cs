// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Current lifecycle status of an engineering work order.
/// Maps to VistA STATUS field in Engineering Work Order file (#6920).
/// </summary>
[GenerateSerializer]
public enum WorkOrderStatus
{
    /// <summary>Work order submitted, not yet assigned or started.</summary>
    Open = 0,
    /// <summary>Work order assigned to a shop and actively being worked.</summary>
    InProgress = 1,
    /// <summary>Work is temporarily paused (waiting for parts, access, etc.).</summary>
    OnHold = 2,
    /// <summary>All work is complete and verified.</summary>
    Completed = 3,
    /// <summary>Work order cancelled before completion.</summary>
    Cancelled = 4,
}

/// <summary>
/// Priority level for an engineering work order.
/// Maps to VistA PRIORITY field in File #6920.
/// </summary>
[GenerateSerializer]
public enum WorkOrderPriority
{
    /// <summary>Routine maintenance or non-urgent repair.</summary>
    Routine = 1,
    /// <summary>Urgent — impacts patient care or operations, address within 24 hours.</summary>
    Urgent = 2,
    /// <summary>Emergency — immediate safety hazard or critical system failure.</summary>
    Emergency = 3,
}

/// <summary>
/// Type/category of engineering work being performed.
/// Maps to VistA TYPE OF WORK field in File #6920.
/// </summary>
[GenerateSerializer]
public enum WorkOrderType
{
    /// <summary>Repair of a broken or malfunctioning system or equipment.</summary>
    Repair = 0,
    /// <summary>Scheduled preventive maintenance.</summary>
    PreventiveMaintenance = 1,
    /// <summary>Installation of new equipment or infrastructure.</summary>
    NewInstall = 2,
    /// <summary>Inspection or compliance check.</summary>
    Inspection = 3,
    /// <summary>Emergency response work order.</summary>
    Emergency = 4,
    /// <summary>Physical alteration or renovation to space.</summary>
    Alteration = 5,
    /// <summary>Other / unclassified work.</summary>
    Other = 6,
}

/// <summary>
/// Engineering shop responsible for the work order.
/// Maps to VistA SHOP field in File #6920.
/// </summary>
[GenerateSerializer]
public enum EngineeringShop
{
    /// <summary>Electrical systems.</summary>
    Electrical = 0,
    /// <summary>Mechanical systems (general machinery).</summary>
    Mechanical = 1,
    /// <summary>Plumbing and piping systems.</summary>
    Plumbing = 2,
    /// <summary>HVAC — heating, ventilation, and air conditioning.</summary>
    Hvac = 3,
    /// <summary>Carpentry and structural woodwork.</summary>
    Carpentry = 4,
    /// <summary>Painting and surface finishing.</summary>
    Paint = 5,
    /// <summary>General maintenance (uncategorized).</summary>
    General = 6,
    /// <summary>Information technology infrastructure.</summary>
    IT = 7,
    /// <summary>Biomedical equipment maintenance.</summary>
    Biomedical = 8,
    /// <summary>Grounds and exterior maintenance.</summary>
    Grounds = 9,
}

/// <summary>
/// A labor time entry recorded against a work order.
/// Corresponds to the LABOR sub-file in VistA Engineering Work Order (#6920.1).
/// </summary>
[GenerateSerializer]
public class WoLaborEntry
{
    /// <summary>
    /// Technician ID (.01) — pointer to NEW PERSON file (#200).
    /// </summary>
    [Id(0)]
    public string TechnicianId { get; set; } = string.Empty;

    /// <summary>
    /// Technician name — display name of the assigned technician.
    /// </summary>
    [Id(1)]
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// Hours worked (.02) — labor hours charged to this work order.
    /// </summary>
    [Id(2)]
    public decimal HoursWorked { get; set; }

    /// <summary>
    /// Work date (.03) — date the labor was performed.
    /// </summary>
    [Id(3)]
    public DateTime WorkDate { get; set; }

    /// <summary>
    /// Notes — description of work performed during this time entry.
    /// </summary>
    [Id(4)]
    public string? Notes { get; set; }

    /// <summary>Date this labor entry was recorded.</summary>
    [Id(5)]
    public DateTime EnteredDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A part or material used on a work order.
/// Corresponds to the PARTS sub-file in VistA Engineering Work Order (#6920.2).
/// </summary>
[GenerateSerializer]
public class WoPartEntry
{
    /// <summary>
    /// Part number (.01) — stock or catalog number of the part.
    /// </summary>
    [Id(0)]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Part description — human-readable name of the part or material.
    /// </summary>
    [Id(1)]
    public string PartDescription { get; set; } = string.Empty;

    /// <summary>
    /// Quantity (.02) — number of units used.
    /// </summary>
    [Id(2)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Unit cost (.03) — cost per unit (USD).
    /// </summary>
    [Id(3)]
    public decimal UnitCost { get; set; }

    /// <summary>Date this part entry was added.</summary>
    [Id(4)]
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A note or progress comment on a work order.
/// Corresponds to REMARKS sub-file in VistA Engineering Work Order.
/// </summary>
[GenerateSerializer]
public class WoNoteEntry
{
    /// <summary>
    /// Author ID (.01) — pointer to NEW PERSON file (#200).
    /// </summary>
    [Id(0)]
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// Author name — display name of the person who entered the note.
    /// </summary>
    [Id(1)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Note text (.02) — free-text progress note or comment.
    /// </summary>
    [Id(2)]
    public string NoteText { get; set; } = string.Empty;

    /// <summary>
    /// Entered date (.03) — timestamp when the note was entered.
    /// </summary>
    [Id(3)]
    public DateTime EnteredDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight projection of a work order for index search results.
/// Used by <see cref="IEngineeringWorkOrderIndexGrain"/> for cross-facility workload queries.
/// </summary>
[GenerateSerializer]
public class WorkOrderIndexEntry
{
    /// <summary>Work order grain key, format "ENG-WO:{guid}".</summary>
    [Id(0)]
    public string WorkOrderId { get; set; } = string.Empty;

    /// <summary>Human-readable sequential work order number.</summary>
    [Id(1)]
    public string WorkOrderNumber { get; set; } = string.Empty;

    /// <summary>Facility grain key this work order is for.</summary>
    [Id(2)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>Display name of the facility.</summary>
    [Id(3)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>Location description (building, floor, room).</summary>
    [Id(4)]
    public string? LocationDescription { get; set; }

    /// <summary>Type of engineering work.</summary>
    [Id(5)]
    public WorkOrderType WorkOrderType { get; set; }

    /// <summary>Priority of the work order.</summary>
    [Id(6)]
    public WorkOrderPriority Priority { get; set; }

    /// <summary>Current lifecycle status.</summary>
    [Id(7)]
    public WorkOrderStatus Status { get; set; }

    /// <summary>Engineering shop responsible.</summary>
    [Id(8)]
    public EngineeringShop Shop { get; set; }

    /// <summary>Name of the technician or team assigned.</summary>
    [Id(9)]
    public string? AssignedToName { get; set; }

    /// <summary>Requested-by person name.</summary>
    [Id(10)]
    public string RequestedByName { get; set; } = string.Empty;

    /// <summary>Scheduled date for the work.</summary>
    [Id(11)]
    public DateTime? ScheduledDate { get; set; }

    /// <summary>Date the work order was created.</summary>
    [Id(12)]
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Engineering Work Order State — represents a single maintenance/repair/install work request.
/// Corresponds to VistA Engineering Work Order file (#6920).
/// MUMPS routines: ENWORK.m (work order entry), ENWLIS.m (work order list), ENWRPT.m (reports).
/// </summary>
[GenerateSerializer]
public class EngineeringWorkOrderState
{
    /// <summary>
    /// Work order grain key (.01) — format "ENG-WO:{guid}".
    /// </summary>
    [Id(0)]
    public string WorkOrderId { get; set; } = string.Empty;

    /// <summary>
    /// Work order number (.02) — sequential human-readable identifier (e.g., "WO-2024-0042").
    /// </summary>
    [Id(1)]
    public string WorkOrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Facility ID (.03) — grain key of the facility/location this work order is for.
    /// </summary>
    [Id(2)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// Facility name — display name of the affected facility or equipment.
    /// </summary>
    [Id(3)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>
    /// Location description (.04) — free-text description of exact location (building, floor, room).
    /// </summary>
    [Id(4)]
    public string? LocationDescription { get; set; }

    /// <summary>
    /// Work order type (.05) — classification of the type of work to be performed.
    /// </summary>
    [Id(5)]
    public WorkOrderType WorkOrderType { get; set; } = WorkOrderType.Repair;

    /// <summary>
    /// Priority (.06) — urgency level of the work order.
    /// </summary>
    [Id(6)]
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Routine;

    /// <summary>
    /// Status (.07) — current lifecycle state of the work order.
    /// </summary>
    [Id(7)]
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;

    /// <summary>
    /// Shop (.08) — engineering shop responsible for performing the work.
    /// </summary>
    [Id(8)]
    public EngineeringShop Shop { get; set; } = EngineeringShop.General;

    /// <summary>
    /// Description (.09) — free-text description of the problem or work required.
    /// </summary>
    [Id(9)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Requested by ID (.10) — pointer to NEW PERSON file (#200) for the requestor.
    /// </summary>
    [Id(10)]
    public string RequestedById { get; set; } = string.Empty;

    /// <summary>
    /// Requested by name — display name of the person who submitted the work order.
    /// </summary>
    [Id(11)]
    public string RequestedByName { get; set; } = string.Empty;

    /// <summary>
    /// Assigned to ID (.11) — pointer to NEW PERSON file (#200) for the assigned technician.
    /// </summary>
    [Id(12)]
    public string? AssignedToId { get; set; }

    /// <summary>
    /// Assigned to name — display name of the assigned technician or team.
    /// </summary>
    [Id(13)]
    public string? AssignedToName { get; set; }

    /// <summary>
    /// Estimated hours (.12) — estimated labor hours to complete the work.
    /// </summary>
    [Id(14)]
    public decimal? EstimatedHours { get; set; }

    /// <summary>
    /// Actual hours — total labor hours recorded via labor entries.
    /// Computed from <see cref="LaborEntries"/> sum.
    /// </summary>
    [Id(15)]
    public decimal ActualHours { get; set; }

    /// <summary>
    /// Estimated parts cost (.13) — estimated cost of materials/parts.
    /// </summary>
    [Id(16)]
    public decimal? EstimatedPartsCost { get; set; }

    /// <summary>
    /// Scheduled date (.14) — planned date for the work to be performed.
    /// </summary>
    [Id(17)]
    public DateTime? ScheduledDate { get; set; }

    /// <summary>
    /// Started date (.15) — date/time work was begun (set when status → InProgress).
    /// </summary>
    [Id(18)]
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// Completed date (.16) — date/time work was completed (set when status → Completed).
    /// </summary>
    [Id(19)]
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Labor entries — list of technician time entries recorded against this work order.
    /// </summary>
    [Id(20)]
    public List<WoLaborEntry> LaborEntries { get; set; } = new();

    /// <summary>
    /// Parts entries — list of parts and materials used on this work order.
    /// </summary>
    [Id(21)]
    public List<WoPartEntry> PartsEntries { get; set; } = new();

    /// <summary>
    /// Notes — progress notes and comments from technicians and supervisors.
    /// </summary>
    [Id(22)]
    public List<WoNoteEntry> Notes { get; set; } = new();

    /// <summary>
    /// Hold reason — reason the work order was placed on hold.
    /// </summary>
    [Id(23)]
    public string? HoldReason { get; set; }

    /// <summary>
    /// Cancelled by ID — pointer to NEW PERSON file (#200) for the cancelling user.
    /// </summary>
    [Id(24)]
    public string? CancelledById { get; set; }

    /// <summary>
    /// Cancelled by name — display name of the person who cancelled the work order.
    /// </summary>
    [Id(25)]
    public string? CancelledByName { get; set; }

    /// <summary>
    /// Cancellation reason — reason given for cancelling the work order.
    /// </summary>
    [Id(26)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Cancelled date — timestamp when the work order was cancelled.
    /// </summary>
    [Id(27)]
    public DateTime? CancelledDate { get; set; }

    /// <summary>
    /// Created date — when this work order was submitted.
    /// </summary>
    [Id(28)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date — updated on every state change.
    /// </summary>
    [Id(29)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
