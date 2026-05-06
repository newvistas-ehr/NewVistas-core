// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainStates;

/// <summary>
/// State for the per-patient, per-body-group PT goal grain.
/// Holds all therapeutic goals for one body group.
/// </summary>
[GenerateSerializer]
public class PTGoalState
{
    /// <summary>Patient identifier (from grain key).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Body group these goals apply to (from grain key).</summary>
    [Id(1)] public BodyGroup BodyGroup { get; set; }

    /// <summary>All goals for this patient and body group.</summary>
    [Id(2)] public List<PTGoal> Goals { get; set; } = new();

    /// <summary>Last modification timestamp.</summary>
    [Id(3)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A single physical therapy goal with progress tracking.
/// </summary>
[GenerateSerializer]
public class PTGoal
{
    /// <summary>Unique goal identifier (GUID).</summary>
    [Id(0)] public string GoalId { get; set; } = string.Empty;

    /// <summary>Type of goal (ROM, Strength, PainReduction, etc.).</summary>
    [Id(1)] public GoalType GoalType { get; set; }

    /// <summary>Body group this goal targets.</summary>
    [Id(2)] public BodyGroup BodyGroup { get; set; }

    /// <summary>Specific movement targeted, if applicable.</summary>
    [Id(3)] public Movement? Movement { get; set; }

    /// <summary>Which side (Bilateral, Left, Right).</summary>
    [Id(4)] public Laterality Side { get; set; } = Laterality.Bilateral;

    /// <summary>Human-readable goal description (e.g., "Achieve 120 degrees shoulder flexion").</summary>
    [Id(5)] public string Description { get; set; } = string.Empty;

    /// <summary>Target value to achieve (degrees for ROM, MMT grade for strength, pain level 0-10 for pain).</summary>
    [Id(6)] public decimal TargetValue { get; set; }

    /// <summary>Baseline value when goal was established.</summary>
    [Id(7)] public decimal BaselineValue { get; set; }

    /// <summary>Current value, updated as progress is recorded.</summary>
    [Id(8)] public decimal CurrentValue { get; set; }

    /// <summary>Target date for achieving the goal.</summary>
    [Id(9)] public DateTime? TargetDate { get; set; }

    /// <summary>Current goal status.</summary>
    [Id(10)] public GoalStatus Status { get; set; } = GoalStatus.Active;

    /// <summary>Progress entries tracking value over time.</summary>
    [Id(11)] public List<PTGoalProgressEntry> ProgressEntries { get; set; } = new();

    /// <summary>Free-text notes.</summary>
    [Id(12)] public string? Notes { get; set; }

    /// <summary>When the goal was created.</summary>
    [Id(13)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modification timestamp.</summary>
    [Id(14)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A single progress measurement toward a PT goal.
/// </summary>
[GenerateSerializer]
public class PTGoalProgressEntry
{
    /// <summary>Date the progress was recorded.</summary>
    [Id(0)] public DateTime Date { get; set; }

    /// <summary>Measured value at this point in time.</summary>
    [Id(1)] public decimal Value { get; set; }

    /// <summary>Optional notes about this measurement.</summary>
    [Id(2)] public string? Notes { get; set; }
}
