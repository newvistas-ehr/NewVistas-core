// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// ICD-10-CM Index Grain — singleton that holds the searchable catalog
/// of all ICD-10-CM codes in memory for fast lookup without activating
/// individual code grains.
///
/// Grain Key: "ICD10-INDEX" (singleton)
///
/// This mirrors VistA's ^ICD9 "B" cross-reference (lookup by code)
/// and "BA" cross-reference (lookup by text), plus the DGICD.m/LEX
/// search functionality used in Registration and Problem List entry.
///
/// The index is loaded from the CMS ICD-10-CM order file via the
/// bulk load endpoint. Once loaded, searches are purely in-memory.
/// </summary>
public interface IIcd10IndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads ICD-10 codes from parsed entries.
    /// Called by the controller after parsing the CMS order file.
    ///
    /// FULL REPLACE: clears the index before loading, so it is only safe
    /// for single-shot loads of a complete code set. Batched imports must
    /// use <see cref="AddCodesAsync"/> instead — calling this per batch
    /// wipes all previously flushed batches.
    /// </summary>
    Task LoadCodesAsync(List<GrainStates.Icd10IndexEntry> entries);

    /// <summary>
    /// Batch-safe additive upsert: merges entries into the index by code,
    /// never clearing existing entries. Re-adding a code replaces that
    /// entry only. IsLoaded/TotalCodes/BillableCodes are recomputed from
    /// the resulting dictionary so repeated imports stay correct.
    /// Used by ReferenceDataImportService, which clears once at the start
    /// of an import and then flushes each batch through this method.
    /// </summary>
    Task AddCodesAsync(List<GrainStates.Icd10IndexEntry> entries);

    /// <summary>
    /// Looks up a single code. Returns null if not found.
    /// Mirrors $$ICDDX^ICDCODE.
    /// </summary>
    Task<GrainStates.Icd10IndexEntry?> GetCodeAsync(string code);

    /// <summary>
    /// Searches codes by text (description) or code prefix.
    /// Mirrors the DGICD.m LEX lookup and $$ICDDATA^ICDXCODE search.
    /// </summary>
    Task<List<GrainStates.Icd10IndexEntry>> SearchAsync(
        string searchText,
        bool billableOnly,
        int maxResults);

    /// <summary>
    /// Text search ranked for TERM→CODE RESOLUTION rather than for browsing. Matches are
    /// ordered by (description starts with the search text, then SHORTEST code — the
    /// less-specific tier — then OrderNumber) BEFORE <paramref name="maxResults"/> is
    /// applied.
    ///
    /// Why this exists: <see cref="SearchAsync"/> pages in code order, so a term matching
    /// more rows than the fetch window ("fracture" matches 20,365 descriptions) returned a
    /// pool of the earliest chapters only, and no downstream ranking could ever reach the
    /// honest generic code — the window starved it. Here the resolver's first two ranking
    /// tiers run inside the fetch, so a bounded window is safe at any corpus frequency:
    /// whatever survives the cap is already the best-ranked slice of ALL matches, and
    /// <see cref="Clinical.ClaimToCodeResolver"/>.SelectCandidates stays the final arbiter
    /// over it.
    ///
    /// Descriptions-only — no code-prefix heuristic. Code lookups and typeahead browsing
    /// keep the code-ordered <see cref="SearchAsync"/> / <see cref="GetByPrefixAsync"/>.
    /// </summary>
    Task<List<GrainStates.Icd10IndexEntry>> SearchRankedAsync(
        string searchText,
        bool billableOnly,
        int maxResults);

    /// <summary>
    /// Returns all codes within a chapter/category range.
    /// e.g., prefix "A0" returns A00-A09 codes.
    /// </summary>
    Task<List<GrainStates.Icd10IndexEntry>> GetByPrefixAsync(
        string codePrefix,
        bool billableOnly,
        int maxResults);

    /// <summary>
    /// Returns index load status and statistics.
    /// </summary>
    Task<Icd10IndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the index. Used before a reload.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the ICD-10 index.
/// </summary>
[GenerateSerializer]
public class Icd10IndexStatus
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    [Id(2)]
    public int TotalCodes { get; set; }

    [Id(3)]
    public int BillableCodes { get; set; }
}
