// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
    /// FULL REPLACE: clears the index first, then loads the given entries.
    /// Only safe for single-shot loads of a complete code set — batched
    /// imports must use <see cref="AddCodesAsync"/> instead.
    /// </summary>
    Task LoadCodesAsync(List<GrainStates.CptCodeIndexEntry> entries);

    /// <summary>
    /// Batch-safe additive upsert: merges entries into the index by code,
    /// never clearing existing entries. IsLoaded/TotalCodes/ActiveCodes are
    /// recomputed from the resulting dictionary so repeated imports stay
    /// correct. Used by ReferenceDataImportService, which clears once at
    /// the start of an import and then flushes each batch through this.
    /// </summary>
    Task AddCodesAsync(List<GrainStates.CptCodeIndexEntry> entries);

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
