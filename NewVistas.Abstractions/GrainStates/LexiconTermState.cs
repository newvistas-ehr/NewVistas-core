// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single clinical terminology entry in the Lexicon Utility.
/// Maps to VistA EXPRESSIONS file (#757.01) and related Lexicon files.
///
/// The Lexicon Utility (LEX*.m routines) provides a unified terminology
/// search layer across multiple coding systems:
///   - SNOMED CT (Systematized Nomenclature of Medicine)
///   - ICD-10-CM (International Classification of Diseases)
///   - CPT (Current Procedural Terminology)
///   - LOINC (Logical Observation Identifiers Names and Codes)
///
/// Key VistA files:
///   #757     - EXPRESSION TYPE file (coding system registry)
///   #757.01  - EXPRESSIONS file (individual terms)
///   #757.02  - CODES file (code mappings per expression)
///   #757.1   - SEMANTIC TYPE file
///
/// Related VistA APIs:
///   $$LKUP^LEXSRCH  — primary term search
///   $$LOOK^LEXA     — lookup by code
///   $$DEF^LEXU      — term definition retrieval
///   LEXSRC2.m       — coding system filter logic
/// </summary>
[GenerateSerializer]
public class LexiconTermState
{
    /// <summary>
    /// The code value within the coding system (e.g., "73211009" for SNOMED,
    /// "E11.9" for ICD-10, "99213" for CPT, "2345-7" for LOINC).
    /// Maps to CODES file (#757.02) field .01.
    /// </summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The coding system identifier: SNOMED, ICD10, CPT, or LOINC.
    /// Maps to EXPRESSION TYPE file (#757) pointer.
    /// </summary>
    [Id(1)]
    public string CodingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Preferred display name / designation for this term.
    /// Maps to EXPRESSIONS file (#757.01) field .01.
    /// </summary>
    [Id(2)]
    public string Designation { get; set; } = string.Empty;

    /// <summary>
    /// Fully specified name with semantic tag (SNOMED style).
    /// e.g., "Diabetes mellitus (disorder)".
    /// Null for coding systems that don't use FSN.
    /// </summary>
    [Id(3)]
    public string? FullySpecifiedName { get; set; }

    /// <summary>
    /// Whether this term is currently active in the coding system.
    /// Maps to status field in the source vocabulary.
    /// </summary>
    [Id(4)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Semantic type classification (e.g., "disorder", "finding",
    /// "procedure", "observable entity", "substance").
    /// Maps to SEMANTIC TYPE file (#757.1).
    /// </summary>
    [Id(5)]
    public string? SemanticType { get; set; }

    /// <summary>
    /// SNOMED CT concept identifier. For SNOMED terms this is the
    /// same as Code; null for other coding systems.
    /// </summary>
    [Id(6)]
    public string? ConceptId { get; set; }

    /// <summary>
    /// Alternate names / synonyms for this term.
    /// Maps to SYNONYMS multiple in EXPRESSIONS file (#757.01).
    /// </summary>
    [Id(7)]
    public List<string> Synonyms { get; set; } = new();

    /// <summary>
    /// Date this term was first loaded into the Lexicon.
    /// </summary>
    [Id(8)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date this term was last modified.
    /// </summary>
    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
