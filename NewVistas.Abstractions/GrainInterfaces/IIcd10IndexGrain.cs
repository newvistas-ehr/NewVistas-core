// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    /// </summary>
    Task LoadCodesAsync(List<GrainStates.Icd10IndexEntry> entries);

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
