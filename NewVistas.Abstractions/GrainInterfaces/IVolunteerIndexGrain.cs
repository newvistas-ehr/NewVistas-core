// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Volunteer Index Grain — singleton registry index for all VA Voluntary Service volunteers.
///
/// VistA VOLUNTARY SERVICE file (#8810).
///
/// Grain key: "VS-INDEX"
/// </summary>
public interface IVolunteerIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all volunteer index entries.</summary>
    Task<List<VolunteerIndexEntry>> GetAllAsync();

    /// <summary>Returns volunteers filtered by enrollment status.</summary>
    Task<List<VolunteerIndexEntry>> GetByStatusAsync(VolunteerStatus status);

    /// <summary>Returns volunteers filtered by primary service type.</summary>
    Task<List<VolunteerIndexEntry>> GetByServiceTypeAsync(VolunteerServiceType serviceType);

    /// <summary>
    /// Searches volunteers by name fragment (case-insensitive partial match on first or last name).
    /// </summary>
    Task<List<VolunteerIndexEntry>> SearchAsync(string nameFragment);

    /// <summary>Inserts or updates the index entry for a volunteer.</summary>
    Task UpsertEntryAsync(VolunteerIndexEntry entry);

    /// <summary>Removes the index entry for the specified volunteer.</summary>
    Task RemoveEntryAsync(string volunteerId);
}
