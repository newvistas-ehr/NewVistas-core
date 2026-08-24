// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// LOINC Code Index Grain — singleton that holds the searchable catalog
/// of all LOINC codes in memory for fast lookup without activating
/// individual code grains.
///
/// Grain Key: "LOINC-INDEX" (singleton)
///
/// This mirrors how VistA cross-references WKLD CODE (File #64)
/// and LABORATORY TEST (File #60) for fast LOINC-based lookup.
/// </summary>
public interface ILoincCodeIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds or updates a single LOINC code entry in the index.
    /// </summary>
    Task AddOrUpdateEntryAsync(GrainStates.LoincCodeIndexEntry entry);

    /// <summary>
    /// Bulk-loads LOINC code entries into the index.
    /// FULL REPLACE: clears the index first, then loads the given entries.
    /// Only safe for single-shot loads of a complete code set — batched
    /// imports must use <see cref="AddCodesAsync"/> instead.
    /// </summary>
    Task LoadCodesAsync(List<GrainStates.LoincCodeIndexEntry> entries);

    /// <summary>
    /// Batch-safe additive upsert: merges entries into the index by code,
    /// never clearing existing entries. IsLoaded/TotalCodes/ActiveCodes are
    /// recomputed from the resulting dictionary so repeated imports stay
    /// correct. Used by ReferenceDataImportService, which clears once at
    /// the start of an import and then flushes each batch through this.
    /// </summary>
    Task AddCodesAsync(List<GrainStates.LoincCodeIndexEntry> entries);

    /// <summary>
    /// Looks up a single code. Returns null if not found.
    /// </summary>
    Task<GrainStates.LoincCodeIndexEntry?> GetCodeAsync(string code);

    /// <summary>
    /// Searches codes by text (component, short name, or long common name).
    /// </summary>
    Task<List<GrainStates.LoincCodeIndexEntry>> SearchAsync(
        string searchText,
        int maxResults);

    /// <summary>
    /// Returns all codes within a system (e.g., "Ser", "Bld", "Urine").
    /// </summary>
    Task<List<GrainStates.LoincCodeIndexEntry>> GetBySystemAsync(
        string system,
        int maxResults);

    /// <summary>
    /// Returns all active codes. If maxResults is 0, returns all.
    /// </summary>
    Task<List<GrainStates.LoincCodeIndexEntry>> GetActiveCodesAsync(int maxResults);

    /// <summary>
    /// Returns index load status and statistics.
    /// </summary>
    Task<LoincCodeIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the index. Used before a reload.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the LOINC code index.
/// </summary>
[GenerateSerializer]
public class LoincCodeIndexStatus
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
