// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lexicon Term Grain — represents a single clinical terminology entry
/// from VistA's Lexicon Utility (File #757, LEX*.m routines).
///
/// Grain Key: "LEX:{CodingSystem}:{Code}"
///   e.g., "LEX:SNOMED:73211009", "LEX:ICD10:E11.9",
///         "LEX:CPT:99213", "LEX:LOINC:2345-7"
///
/// This grain stores the full detail for an individual term, including
/// synonyms and semantic type. It is populated during term loading and
/// read when full detail is needed beyond what the index provides.
///
/// Related VistA APIs:
///   $$DEF^LEXU     — full definition retrieval
///   $$LOOK^LEXA    — lookup by code + source
///   LEXSRCH.m      — term search entry point
/// </summary>
public interface ILexiconTermGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the full term state.
    /// </summary>
    Task<GrainStates.LexiconTermState> GetTermAsync();

    /// <summary>
    /// Loads or updates the term data.
    /// Called during bulk import or demo seeding.
    /// </summary>
    Task LoadTermAsync(
        string code,
        string codingSystem,
        string designation,
        string? fullySpecifiedName,
        bool isActive,
        string? semanticType,
        string? conceptId,
        List<string>? synonyms);
}
