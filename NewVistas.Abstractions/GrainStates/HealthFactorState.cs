// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Health Factor grain based on VistA V HEALTH FACTORS file (#9000010.23)
/// </summary>
[GenerateSerializer]
public class HealthFactorState
{
    [Id(0)]
    public string HealthFactorId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Health Factor (.01) � pointer to HEALTH FACTORS file (#9999999.64)
    /// </summary>
    [Id(2)]
    public string HealthFactorName { get; set; } = string.Empty;

    [Id(3)]
    public string? HealthFactorDefId { get; set; }

    /// <summary>
    /// Category � SOCIAL HISTORY, SCREENING, BEHAVIORAL HEALTH, SUBSTANCE USE, etc.
    /// </summary>
    [Id(4)]
    public string? Category { get; set; }

    /// <summary>
    /// Event Date and Time
    /// </summary>
    [Id(5)]
    public DateTime EventDateTime { get; set; }

    /// <summary>
    /// Level/Severity � MINIMAL, MODERATE, HEAVY/SEVERE
    /// </summary>
    [Id(6)]
    public string? LevelSeverity { get; set; }

    /// <summary>
    /// Visit � pointer to VISIT file (#9000010)
    /// </summary>
    [Id(7)]
    public string? VisitId { get; set; }

    [Id(8)]
    public string? LocationId { get; set; }

    [Id(9)]
    public string? LocationName { get; set; }

    [Id(10)]
    public string? EnteredById { get; set; }

    [Id(11)]
    public string? EnteredByName { get; set; }

    [Id(12)]
    public string? Comments { get; set; }

    /// <summary>
    /// Subcategory — e.g., CURRENT SMOKER, FORMER SMOKER
    /// </summary>
    [Id(13)]
    public string? Subcategory { get; set; }

    /// <summary>
    /// Measurable value — e.g., "2 packs/day"
    /// </summary>
    [Id(14)]
    public string? Value { get; set; }

    /// <summary>
    /// Numeric magnitude if applicable
    /// </summary>
    [Id(15)]
    public string? Magnitude { get; set; }

    /// <summary>
    /// Evaluation Status — CURRENT, RESOLVED, INACTIVE
    /// </summary>
    [Id(16)]
    public string? EvaluationStatus { get; set; }

    /// <summary>
    /// Date the health factor was resolved
    /// </summary>
    [Id(17)]
    public DateTime? ResolutionDate { get; set; }

    /// <summary>
    /// Name of the person who resolved the health factor
    /// </summary>
    [Id(18)]
    public string? ResolvedByName { get; set; }

    /// <summary>
    /// Tracking changes over time
    /// </summary>
    [Id(19)]
    public List<HealthFactorHistoryEntry> History { get; set; } = new();

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A history entry tracking changes to a health factor over time
/// </summary>
[GenerateSerializer]
public class HealthFactorHistoryEntry
{
    /// <summary>
    /// Date the entry was recorded
    /// </summary>
    [Id(0)]
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Value at time of recording
    /// </summary>
    [Id(1)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Severity level at time of recording
    /// </summary>
    [Id(2)]
    public string? SeverityLevel { get; set; }

    /// <summary>
    /// Comment at time of recording
    /// </summary>
    [Id(3)]
    public string? Comment { get; set; }

    /// <summary>
    /// Name of the person who recorded the entry
    /// </summary>
    [Id(4)]
    public string? RecordedByName { get; set; }
}
