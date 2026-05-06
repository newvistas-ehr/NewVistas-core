// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility grain — represents a physical location, room, or piece of equipment
/// managed by the Engineering service.
/// Key format: "ENG-FAC:{facilityId}".
/// Corresponds to VistA Engineering file (#6914).
/// MUMPS routines: ENSITE.m.
/// </summary>
public interface IFacilityGrain : IGrainWithStringKey
{
    /// <summary>Get the full facility state.</summary>
    Task<FacilityState> GetFacilityAsync();

    /// <summary>
    /// Create or fully update the facility record.
    /// </summary>
    Task UpsertAsync(
        string facilityName,
        FacilityCategory category,
        string? building,
        string? floor,
        string? room,
        string? departmentId,
        string? departmentName,
        string? equipmentType,
        string? serialNumber,
        string? model,
        string? manufacturer,
        DateTime? installationDate,
        DateTime? warrantyExpirationDate,
        string? description);

    /// <summary>
    /// Increment the work order count for this facility.
    /// Called whenever a new work order is filed against this facility.
    /// </summary>
    Task IncrementWorkOrderCountAsync();

    /// <summary>
    /// Transition the facility status to UnderMaintenance.
    /// </summary>
    Task SetUnderMaintenanceAsync();

    /// <summary>
    /// Restore the facility to Active status.
    /// </summary>
    Task SetActiveAsync();

    /// <summary>
    /// Decommission the facility — marks it as no longer in service.
    /// </summary>
    Task DecommissionAsync();
}
