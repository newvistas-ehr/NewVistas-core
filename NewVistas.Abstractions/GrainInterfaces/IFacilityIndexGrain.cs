// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility Index grain — singleton reference data search index for all facility records.
/// Key: "ENG-FAC-IDX".
/// Supports facility lookup and search used when creating work orders.
/// </summary>
public interface IFacilityIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add or update a facility entry in the index.
    /// Replaces any existing entry with the same FacilityId.
    /// </summary>
    Task AddOrUpdateAsync(FacilityIndexEntry entry);

    /// <summary>
    /// Search facilities by name, building, department, or category.
    /// </summary>
    /// <param name="searchText">Text to match against name, building, or department (case-insensitive).</param>
    /// <param name="category">Filter by facility category.</param>
    /// <param name="activeOnly">When true, excludes Decommissioned facilities.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    Task<List<FacilityIndexEntry>> SearchAsync(
        string? searchText,
        FacilityCategory? category,
        bool activeOnly,
        int maxResults);

    /// <summary>
    /// Return all facility entries in the index.
    /// </summary>
    Task<List<FacilityIndexEntry>> GetAllAsync();
}
