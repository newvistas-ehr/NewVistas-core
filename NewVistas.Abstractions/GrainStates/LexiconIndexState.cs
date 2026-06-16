// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lexicon Search index grain.
/// Holds a unified searchable catalog of clinical terms across
/// SNOMED CT, ICD-10-CM, CPT, and LOINC vocabularies.
///
/// This mirrors VistA's Lexicon Utility cross-references:
///   ^LEX(757.01,"B",...)  — "B" cross-reference for text lookup
///   ^LEX(757.02,"ASRC",...) — source-filtered code lookup
/// </summary>
[GenerateSerializer]
public class LexiconIndexState
{
    /// <summary>
    /// All loaded terms, keyed by "{CodingSystem}:{Code}"
    /// (e.g., "SNOMED:73211009", "ICD10:E11.9", "CPT:99213", "LOINC:2345-7").
    /// </summary>
    [Id(0)]
    public Dictionary<string, LexiconIndexEntry> Terms { get; set; } = new();

    /// <summary>
    /// Whether the index has been loaded/seeded.
    /// </summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// When the index was last loaded or seeded.
    /// </summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>
    /// Total number of terms in the index.
    /// </summary>
    [Id(3)]
    public int TotalTerms { get; set; }
}

/// <summary>
/// Lightweight index entry for Lexicon search — no grain activation needed.
/// Represents a single clinical term from any supported coding system.
/// </summary>
[GenerateSerializer]
public class LexiconIndexEntry
{
    /// <summary>
    /// The code value (e.g., "73211009", "E11.9", "99213", "2345-7").
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The coding system: SNOMED, ICD10, CPT, or LOINC.
    /// </summary>
    [Id(1)]
    public string CodingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Preferred display name for this term.
    /// </summary>
    [Id(2)]
    public string Designation { get; set; } = string.Empty;

    /// <summary>
    /// Fully specified name (SNOMED-style, with semantic tag).
    /// Null for systems that don't use FSN.
    /// </summary>
    [Id(3)]
    public string? FullySpecifiedName { get; set; }

    /// <summary>
    /// Whether this term is currently active.
    /// </summary>
    [Id(4)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Semantic type classification (disorder, finding, procedure, etc.).
    /// </summary>
    [Id(5)]
    public string? SemanticType { get; set; }
}
