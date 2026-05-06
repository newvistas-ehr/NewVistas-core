// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// DSS Unit State — represents a Decision Support System (DSS) unit/department
/// definition used for workload tracking in Event Capture.
/// Corresponds to VistA File #724 (DSS UNIT).
/// MUMPS routines: ECPEDSS.m (DSS unit management).
/// </summary>
[GenerateSerializer]
public class DssUnitState
{
    /// <summary>
    /// DSS unit ID — grain key, format "EC-DSS-UNIT:{dssUnitId}".
    /// </summary>
    [Id(0)]
    public string DssUnitId { get; set; } = string.Empty;

    /// <summary>
    /// Unit name (.01) — full descriptive name of the DSS unit
    /// (e.g., "PRIMARY CARE", "PHYSICAL THERAPY").
    /// </summary>
    [Id(1)]
    public string UnitName { get; set; } = string.Empty;

    /// <summary>
    /// Unit code (.02) — 3–6 character abbreviation used in workload reports
    /// (e.g., "PC", "PT", "MH").
    /// </summary>
    [Id(2)]
    public string UnitCode { get; set; } = string.Empty;

    /// <summary>
    /// Division ID (.03) — pointer to INSTITUTION file (#4). The facility division.
    /// </summary>
    [Id(3)]
    public string? DivisionId { get; set; }

    /// <summary>
    /// Division name — display name of the facility division.
    /// </summary>
    [Id(4)]
    public string? DivisionName { get; set; }

    /// <summary>
    /// Primary stop code (.04) — VistA clinic stop code for primary workload credit.
    /// Used in AMIS and Workload reports.
    /// </summary>
    [Id(5)]
    public string? PrimaryStopCode { get; set; }

    /// <summary>
    /// Credit stop code (.05) — secondary stop code for additional workload attribution.
    /// </summary>
    [Id(6)]
    public string? CreditStopCode { get; set; }

    /// <summary>
    /// Treatment code (.06) — clinical treatment category code.
    /// </summary>
    [Id(7)]
    public string? TreatmentCode { get; set; }

    /// <summary>
    /// Description — free-text description of the unit's scope and services.
    /// </summary>
    [Id(8)]
    public string? Description { get; set; }

    /// <summary>
    /// Is active (.07) — false if the unit has been deactivated.
    /// </summary>
    [Id(9)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Created date — when this DSS unit was created.
    /// </summary>
    [Id(10)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date — updated on every state change.
    /// </summary>
    [Id(11)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight index entry for a DSS unit; stored in IDssUnitIndexGrain.
/// </summary>
[GenerateSerializer]
public class DssUnitIndexEntry
{
    [Id(0)]
    public string DssUnitId { get; set; } = string.Empty;

    [Id(1)]
    public string UnitName { get; set; } = string.Empty;

    [Id(2)]
    public string UnitCode { get; set; } = string.Empty;

    [Id(3)]
    public string? DivisionName { get; set; }

    [Id(4)]
    public string? PrimaryStopCode { get; set; }

    [Id(5)]
    public bool IsActive { get; set; } = true;
}
