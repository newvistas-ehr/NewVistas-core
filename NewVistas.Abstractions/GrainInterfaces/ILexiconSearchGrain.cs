// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lexicon Search Grain — singleton index that provides unified clinical
/// term lookup across SNOMED CT, ICD-10-CM, CPT, and LOINC vocabularies.
///
/// Grain Key: "LEX-INDEX" (singleton)
///
/// This mirrors VistA's Lexicon Utility (File #757) search functionality:
///   $$LKUP^LEXSRCH  — primary search across all coding systems
///   $$LOOK^LEXA     — lookup by specific code + source
///   LEXSRC2.m       — coding system filter logic
///   ^LEX(757.01,"B",...) — "B" cross-reference for text lookup
///   ^LEX(757.02,"ASRC",...) — source-filtered code lookup
///
/// The index is self-seeding with ~100 common clinical terms on first
/// activation. Additional terms can be loaded via LoadTermsAsync.
///
/// Search strategy:
///   - Text search: case-insensitive substring on Designation and
///     FullySpecifiedName fields
///   - Code lookup: exact match by "{CodingSystem}:{Code}" key
///   - System filter: optional coding system restriction
/// </summary>
public interface ILexiconSearchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Searches terms by text (designation/FSN) with optional coding system filter.
    /// Mirrors $$LKUP^LEXSRCH with source filter.
    /// </summary>
    Task<List<GrainStates.LexiconIndexEntry>> SearchAsync(
        string searchText,
        string? codingSystem,
        int maxResults);

    /// <summary>
    /// Looks up a single term by exact code and coding system.
    /// Returns null if not found. Mirrors $$LOOK^LEXA.
    /// </summary>
    Task<GrainStates.LexiconIndexEntry?> LookupCodeAsync(
        string code,
        string codingSystem);

    /// <summary>
    /// Returns terms filtered by coding system.
    /// </summary>
    Task<List<GrainStates.LexiconIndexEntry>> GetByCodingSystemAsync(
        string codingSystem,
        int maxResults);

    /// <summary>
    /// Bulk-loads terms into the index.
    /// </summary>
    Task LoadTermsAsync(List<GrainStates.LexiconIndexEntry> entries);

    /// <summary>
    /// Returns index load status and statistics.
    /// </summary>
    Task<LexiconIndexStatus> GetStatusAsync();

    /// <summary>
    /// Seeds the index with ~100 common clinical terms across
    /// SNOMED CT, ICD-10-CM, CPT, and LOINC vocabularies.
    /// Idempotent — clears existing data before seeding.
    /// </summary>
    Task SeedDemoDataAsync();

    /// <summary>
    /// Clears the entire index. Used before a reload.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the Lexicon search index.
/// </summary>
[GenerateSerializer]
public class LexiconIndexStatus
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    [Id(2)]
    public int TotalTerms { get; set; }

    [Id(3)]
    public int SnomedCount { get; set; }

    [Id(4)]
    public int Icd10Count { get; set; }

    [Id(5)]
    public int CptCount { get; set; }

    [Id(6)]
    public int LoincCount { get; set; }
}
