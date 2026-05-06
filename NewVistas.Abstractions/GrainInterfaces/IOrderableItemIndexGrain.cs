// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for the Pharmacy Orderable Item catalog (VistA File #50.7).
/// Grain key: "OI-INDEX"
///
/// Provides fast search across all orderable items for CPRS order entry and
/// pharmacy management UIs. Follows the same two-grain pattern used throughout
/// NewVistas reference data domains.
/// </summary>
public interface IOrderableItemIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads the orderable item index from a list of index entries.
    /// Replaces the entire existing index. Sets IsLoaded = true.
    /// </summary>
    Task LoadItemsAsync(List<OrderableItemIndexEntry> entries);

    /// <summary>
    /// Returns the index entry for the given IEN, or null if not found.
    /// </summary>
    Task<OrderableItemIndexEntry?> GetItemAsync(string ien);

    /// <summary>
    /// Full-text search across orderable item names and synonyms.
    /// </summary>
    /// <param name="searchText">Substring to match (case-insensitive).</param>
    /// <param name="type">Filter by pharmacy type; null = all types.</param>
    /// <param name="activeOnly">When true, excludes inactive items.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    Task<List<OrderableItemIndexEntry>> SearchAsync(
        string searchText,
        PharmacyType? type = null,
        bool activeOnly = true,
        int maxResults = 50);

    /// <summary>
    /// Returns all orderable items linked to a specific VA Generic IEN.
    /// Supports clinical order lookup by generic drug name.
    /// </summary>
    Task<List<OrderableItemIndexEntry>> GetByVaGenericAsync(string vaGenericIen);

    /// <summary>
    /// Returns the current status of the orderable item index.
    /// </summary>
    Task<OrderableItemIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the entire index. Used during data refresh or testing.
    /// </summary>
    Task ClearAsync();
}
