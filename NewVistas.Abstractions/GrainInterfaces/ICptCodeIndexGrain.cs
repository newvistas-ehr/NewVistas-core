// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// CPT Code Index Grain — singleton that holds the searchable catalog
/// of all CPT codes in memory for fast lookup without activating
/// individual code grains.
///
/// Grain Key: "CPT-INDEX" (singleton)
///
/// This mirrors VistA's ^ICPT global cross-references for fast
/// code and text-based lookup of CPT procedure codes.
/// </summary>
public interface ICptCodeIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds or updates a single CPT code entry in the index.
    /// </summary>
    Task AddOrUpdateEntryAsync(GrainStates.CptCodeIndexEntry entry);

    /// <summary>
    /// Bulk-loads CPT code entries into the index.
    /// Replaces any previously loaded entries.
    /// </summary>
    Task LoadCodesAsync(List<GrainStates.CptCodeIndexEntry> entries);

    /// <summary>
    /// Looks up a single code. Returns null if not found.
    /// Mirrors $$CPT^ICPTCOD.
    /// </summary>
    Task<GrainStates.CptCodeIndexEntry?> GetCodeAsync(string code);

    /// <summary>
    /// Searches codes by text (description) or code prefix.
    /// Mirrors CPTSRCH^ICPT.
    /// </summary>
    Task<List<GrainStates.CptCodeIndexEntry>> SearchAsync(
        string searchText,
        int maxResults);

    /// <summary>
    /// Returns all codes within a category.
    /// </summary>
    Task<List<GrainStates.CptCodeIndexEntry>> GetByCategoryAsync(
        string category,
        int maxResults);

    /// <summary>
    /// Returns all active codes. If maxResults is 0, returns all.
    /// </summary>
    Task<List<GrainStates.CptCodeIndexEntry>> GetActiveCodesAsync(int maxResults);

    /// <summary>
    /// Returns index load status and statistics.
    /// </summary>
    Task<CptCodeIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the index. Used before a reload.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the CPT code index.
/// </summary>
[GenerateSerializer]
public class CptCodeIndexStatus
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    [Id(2)]
    public int TotalCodes { get; set; }

    [Id(3)]
    public int ActiveCodes { get; set; }
}
