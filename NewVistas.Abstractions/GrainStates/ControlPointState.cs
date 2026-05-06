// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>VistA IFCAP Control Point status.</summary>
[GenerateSerializer]
public enum ControlPointStatus
{
    /// <summary>Control point is active and accepting requests.</summary>
    Active = 0,
    /// <summary>Control point is inactive.</summary>
    Inactive = 1,
    /// <summary>Control point is suspended (no new obligations).</summary>
    Suspended = 2,
}

/// <summary>
/// State for an IFCAP Control Point — the organizational unit that holds
/// appropriated funds and sponsors purchase requests.
/// Corresponds to VistA File #410 (CONTROL POINT ACTIVITY).
/// </summary>
[GenerateSerializer]
public class ControlPointState
{
    /// <summary>Unique identifier for this control point. (.01)</summary>
    [Id(0)] public string ControlPointId { get; set; } = string.Empty;

    /// <summary>Human-readable name of the control point. (.02)</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>Station/facility identifier. (.03)</summary>
    [Id(2)] public string FacilityId { get; set; } = string.Empty;

    /// <summary>Service or section code. (.04)</summary>
    [Id(3)] public string ServiceId { get; set; } = string.Empty;

    /// <summary>Federal fiscal year (e.g. 2025). (.05)</summary>
    [Id(4)] public int FiscalYear { get; set; }

    /// <summary>Budget object code. (.06)</summary>
    [Id(5)] public string BudgetCode { get; set; } = string.Empty;

    /// <summary>Total funds allocated to this control point. (.07)</summary>
    [Id(6)] public decimal AllocatedAmount { get; set; }

    /// <summary>Funds currently obligated against approved purchase requests. (.08)</summary>
    [Id(7)] public decimal ObligatedAmount { get; set; }

    /// <summary>Funds expended (purchase orders issued). (.09)</summary>
    [Id(8)] public decimal ExpendedAmount { get; set; }

    /// <summary>Remaining unobligated balance (AllocatedAmount - ObligatedAmount). (.10)</summary>
    [Id(9)] public decimal RemainingBalance { get; set; }

    /// <summary>Current status of the control point.</summary>
    [Id(10)] public ControlPointStatus Status { get; set; } = ControlPointStatus.Active;

    /// <summary>User ID of the designated control point officer. (.11)</summary>
    [Id(11)] public string ControlPointOfficerId { get; set; } = string.Empty;

    /// <summary>Name of the designated control point officer. (.12)</summary>
    [Id(12)] public string ControlPointOfficerName { get; set; } = string.Empty;

    /// <summary>IDs of purchase requests associated with this control point.</summary>
    [Id(13)] public List<string> RequestIds { get; set; } = new();

    /// <summary>Date the control point was created.</summary>
    [Id(14)] public DateTime CreatedDate { get; set; }

    /// <summary>Date the control point was last modified.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight summary of a control point for index queries.</summary>
[GenerateSerializer]
public record ControlPointIndexEntry(
    [property: Id(0)] string ControlPointId,
    [property: Id(1)] string Name,
    [property: Id(2)] string FacilityId,
    [property: Id(3)] int FiscalYear,
    [property: Id(4)] decimal RemainingBalance,
    [property: Id(5)] ControlPointStatus Status);

/// <summary>Global index state for all control points.</summary>
[GenerateSerializer]
public class ControlPointIndexState
{
    [Id(0)] public List<ControlPointIndexEntry> Entries { get; set; } = new();
}
