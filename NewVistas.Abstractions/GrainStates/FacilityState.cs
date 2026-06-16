// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Category of a facility record — whether it represents a physical space or an equipment item.
/// Maps to VistA ENGINEERING file (#6914) structure.
/// </summary>
[GenerateSerializer]
public enum FacilityCategory
{
    /// <summary>A whole building or structure.</summary>
    Building = 0,
    /// <summary>A room or suite within a building.</summary>
    Room = 1,
    /// <summary>A piece of equipment (clinical, industrial, or administrative).</summary>
    Equipment = 2,
    /// <summary>A vehicle or mobile asset.</summary>
    Vehicle = 3,
    /// <summary>A utility system (electrical panel, water line, etc.).</summary>
    Utility = 4,
    /// <summary>A large system spanning multiple locations (HVAC, fire suppression, etc.).</summary>
    System = 5,
    /// <summary>Other / uncategorized facility record.</summary>
    Other = 6,
}

/// <summary>
/// Operational status of a facility.
/// </summary>
[GenerateSerializer]
public enum FacilityStatus
{
    /// <summary>Facility is active and in normal operation.</summary>
    Active = 0,
    /// <summary>Facility is temporarily out of service for maintenance.</summary>
    UnderMaintenance = 1,
    /// <summary>Facility has been decommissioned and is no longer in use.</summary>
    Decommissioned = 2,
}

/// <summary>
/// Lightweight projection of a facility for index search results.
/// Used by <see cref="IFacilityIndexGrain"/> to support facility lookup and search.
/// </summary>
[GenerateSerializer]
public class FacilityIndexEntry
{
    /// <summary>Facility grain key, format "ENG-FAC:{facilityId}".</summary>
    [Id(0)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>Display name of the facility or equipment.</summary>
    [Id(1)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>Category of the facility record.</summary>
    [Id(2)]
    public FacilityCategory Category { get; set; }

    /// <summary>Building where the facility is located.</summary>
    [Id(3)]
    public string? Building { get; set; }

    /// <summary>Floor or level within the building.</summary>
    [Id(4)]
    public string? Floor { get; set; }

    /// <summary>Room number or identifier.</summary>
    [Id(5)]
    public string? Room { get; set; }

    /// <summary>Department that owns or primarily uses this facility.</summary>
    [Id(6)]
    public string? DepartmentName { get; set; }

    /// <summary>Current operational status of the facility.</summary>
    [Id(7)]
    public FacilityStatus Status { get; set; }

    /// <summary>Total number of work orders filed against this facility.</summary>
    [Id(8)]
    public int WorkOrderCount { get; set; }
}

/// <summary>
/// Facility State — represents a physical location, room, or piece of equipment
/// managed by the Engineering service.
/// Corresponds to VistA Engineering file (#6914).
/// MUMPS routines: ENSITE.m (site/facility management), ENWRPT.m (engineering reports).
/// </summary>
[GenerateSerializer]
public class FacilityState
{
    /// <summary>
    /// Facility ID (.01) — grain key, format "ENG-FAC:{facilityId}".
    /// </summary>
    [Id(0)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// Facility name (.02) — descriptive name for the facility or equipment item.
    /// </summary>
    [Id(1)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>
    /// Category (.03) — whether this record represents a building, room, equipment, etc.
    /// </summary>
    [Id(2)]
    public FacilityCategory Category { get; set; } = FacilityCategory.Room;

    /// <summary>
    /// Building (.04) — building name or number where the facility is located.
    /// </summary>
    [Id(3)]
    public string? Building { get; set; }

    /// <summary>
    /// Floor (.05) — floor or level within the building.
    /// </summary>
    [Id(4)]
    public string? Floor { get; set; }

    /// <summary>
    /// Room (.06) — room number or identifier.
    /// </summary>
    [Id(5)]
    public string? Room { get; set; }

    /// <summary>
    /// Department ID (.07) — pointer to HOSPITAL LOCATION file (#44).
    /// </summary>
    [Id(6)]
    public string? DepartmentId { get; set; }

    /// <summary>
    /// Department name — display name of the owning department.
    /// </summary>
    [Id(7)]
    public string? DepartmentName { get; set; }

    /// <summary>
    /// Equipment type (.08) — type of equipment (for Category = Equipment records).
    /// Examples: "MRI Scanner", "Patient Lift", "Air Compressor".
    /// </summary>
    [Id(8)]
    public string? EquipmentType { get; set; }

    /// <summary>
    /// Serial number (.09) — manufacturer serial number (equipment records only).
    /// </summary>
    [Id(9)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Model (.10) — manufacturer model name/number (equipment records only).
    /// </summary>
    [Id(10)]
    public string? Model { get; set; }

    /// <summary>
    /// Manufacturer (.11) — manufacturer or vendor name (equipment records only).
    /// </summary>
    [Id(11)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Installation date (.12) — date the equipment or facility was placed in service.
    /// </summary>
    [Id(12)]
    public DateTime? InstallationDate { get; set; }

    /// <summary>
    /// Warranty expiration date (.13) — date the manufacturer warranty expires.
    /// </summary>
    [Id(13)]
    public DateTime? WarrantyExpirationDate { get; set; }

    /// <summary>
    /// Status (.14) — current operational status of the facility.
    /// </summary>
    [Id(14)]
    public FacilityStatus Status { get; set; } = FacilityStatus.Active;

    /// <summary>
    /// Description (.15) — free-text description of the facility or equipment.
    /// </summary>
    [Id(15)]
    public string? Description { get; set; }

    /// <summary>
    /// Work order count — running total of work orders filed against this facility.
    /// </summary>
    [Id(16)]
    public int WorkOrderCount { get; set; }

    /// <summary>
    /// Created date — when this facility record was added to the system.
    /// </summary>
    [Id(17)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date — updated on every state change.
    /// </summary>
    [Id(18)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
