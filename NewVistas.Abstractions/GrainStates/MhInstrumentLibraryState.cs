// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Mental Health Instrument Library singleton grain.
/// Stores instrument definitions, scoring rules, and question sets.
/// </summary>
[GenerateSerializer]
public class MhInstrumentLibraryState
{
    /// <summary>
    /// Collection of instrument definitions
    /// </summary>
    [Id(0)]
    public List<MhInstrumentDefinition> Instruments { get; set; } = new();

    /// <summary>
    /// Last modification timestamp
    /// </summary>
    [Id(1)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Definition of a mental health screening instrument including scoring ranges and items.
/// </summary>
[GenerateSerializer]
public class MhInstrumentDefinition
{
    /// <summary>
    /// Instrument name (e.g., PHQ-9, GAD-7, AUDIT-C)
    /// </summary>
    [Id(0)]
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the instrument and its purpose
    /// </summary>
    [Id(1)]
    public string? Description { get; set; }

    /// <summary>
    /// Number of items/questions in the instrument
    /// </summary>
    [Id(2)]
    public int ItemCount { get; set; }

    /// <summary>
    /// Maximum possible score
    /// </summary>
    [Id(3)]
    public int MaxScore { get; set; }

    /// <summary>
    /// Score at or above which the screen is considered positive
    /// </summary>
    [Id(4)]
    public int PositiveScreenThreshold { get; set; }

    /// <summary>
    /// Score interpretation ranges
    /// </summary>
    [Id(5)]
    public List<MhScoreRange> ScoreRanges { get; set; } = new();

    /// <summary>
    /// Individual items/questions for the instrument
    /// </summary>
    [Id(6)]
    public List<MhInstrumentItem> Items { get; set; } = new();

    /// <summary>
    /// Clinical domain — DEPRESSION, ANXIETY, SUBSTANCE_USE, PTSD, SUICIDE_RISK
    /// </summary>
    [Id(7)]
    public string? ClinicalDomain { get; set; }

    /// <summary>
    /// Recommended screening frequency — ANNUAL, QUARTERLY, EVERY_VISIT, AS_NEEDED
    /// </summary>
    [Id(8)]
    public string? Frequency { get; set; }
}

/// <summary>
/// Score range with interpretation and clinical recommendation.
/// </summary>
[GenerateSerializer]
public class MhScoreRange
{
    /// <summary>
    /// Minimum score for this range (inclusive)
    /// </summary>
    [Id(0)]
    public int MinScore { get; set; }

    /// <summary>
    /// Maximum score for this range (inclusive)
    /// </summary>
    [Id(1)]
    public int MaxScore { get; set; }

    /// <summary>
    /// Interpretation label (e.g., MINIMAL, MILD, MODERATE, SEVERE)
    /// </summary>
    [Id(2)]
    public string Interpretation { get; set; } = string.Empty;

    /// <summary>
    /// Clinical recommendation for this score range
    /// </summary>
    [Id(3)]
    public string? Recommendation { get; set; }
}

/// <summary>
/// Individual item/question within an instrument definition.
/// </summary>
[GenerateSerializer]
public class MhInstrumentItem
{
    /// <summary>
    /// Item number within the instrument
    /// </summary>
    [Id(0)]
    public int ItemNumber { get; set; }

    /// <summary>
    /// Question text
    /// </summary>
    [Id(1)]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Minimum allowed response value
    /// </summary>
    [Id(2)]
    public int MinValue { get; set; }

    /// <summary>
    /// Maximum allowed response value
    /// </summary>
    [Id(3)]
    public int MaxValue { get; set; }

    /// <summary>
    /// Response option labels (e.g., "Not at all", "Several days", etc.)
    /// </summary>
    [Id(4)]
    public List<string> ResponseOptions { get; set; } = new();
}
