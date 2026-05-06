// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// How this movement is measured — determines the UI fields, units, and tools displayed.
/// </summary>
public enum MeasurementType
{
    /// <summary>Measured in degrees with a goniometer (most ROM measurements).</summary>
    Angle,

    /// <summary>Measured in force (lbs or kg) with a dynamometer (grip, pinch).</summary>
    Force,

    /// <summary>Measured in millimeters or centimeters with a ruler/tape (TMJ opening, finger-to-floor).</summary>
    Distance
}

/// <summary>
/// Rich metadata for a single movement within a body group.
/// Carries everything the UI needs to render the correct form fields,
/// units, positioning instructions, and illustration.
/// </summary>
public record MovementDefinition
{
    /// <summary>The movement enum value.</summary>
    public required Movement Movement { get; init; }

    /// <summary>Human-readable name (e.g., "Lateral Flexion — Left").</summary>
    public required string DisplayName { get; init; }

    /// <summary>How this movement is measured — angle, force, or distance.</summary>
    public required MeasurementType MeasurementType { get; init; }

    /// <summary>Unit label for the measurement (e.g., "degrees", "lbs", "mm").</summary>
    public required string Units { get; init; }

    /// <summary>
    /// Normal value for a healthy adult. Null if no standard reference exists.
    /// For Angle: degrees. For Force: lbs. For Distance: mm.
    /// </summary>
    public decimal? NormalValue { get; init; }

    /// <summary>Measurement tool used (e.g., "Goniometer", "Jamar Dynamometer", "Pinch Gauge", "Ruler").</summary>
    public string? Tool { get; init; }

    /// <summary>Patient positioning for this measurement (e.g., "Seated, arm at side", "Supine, knee extended").</summary>
    public string? PatientPosition { get; init; }

    /// <summary>
    /// Brief instruction for the therapist on how to perform this measurement.
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    /// Illustration key for rendering a diagram of this movement.
    /// Maps to an image asset (e.g., "cervical-flexion", "shoulder-abduction").
    /// The UI resolves this to an image path like "/images/pt/{IllustrationKey}.svg".
    /// </summary>
    public string? IllustrationKey { get; init; }

    /// <summary>
    /// Whether this movement typically has both Active and Passive measurements.
    /// Force-based measurements (grip, pinch) do not have Active/Passive — they are a single value.
    /// </summary>
    public bool HasActivePassive => MeasurementType == MeasurementType.Angle;
}
