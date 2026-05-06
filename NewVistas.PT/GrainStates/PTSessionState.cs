// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainStates;

/// <summary>
/// State for a single physical therapy measurement session.
/// One session captures all ROM and strength measurements for one body group, one visit.
/// </summary>
[GenerateSerializer]
public class PTSessionState
{
    /// <summary>Grain key, set on activation.</summary>
    [Id(0)] public string SessionId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Anatomical body group evaluated.</summary>
    [Id(2)] public BodyGroup BodyGroup { get; set; }

    /// <summary>Date and time the session was recorded.</summary>
    [Id(3)] public DateTime SessionDate { get; set; }

    /// <summary>Therapist user ID (IEN or identity ID).</summary>
    [Id(4)] public string? TherapistId { get; set; }

    /// <summary>Therapist display name.</summary>
    [Id(5)] public string? TherapistName { get; set; }

    /// <summary>Facility/clinic location ID.</summary>
    [Id(6)] public string? LocationId { get; set; }

    /// <summary>Facility/clinic display name.</summary>
    [Id(7)] public string? LocationName { get; set; }

    /// <summary>Range of motion measurements (active and/or passive) for each movement.</summary>
    [Id(8)] public List<RomMeasurement> RomMeasurements { get; set; } = new();

    /// <summary>Manual muscle testing strength measurements for each movement.</summary>
    [Id(9)] public List<StrengthMeasurement> StrengthMeasurements { get; set; } = new();

    /// <summary>Free-text session notes.</summary>
    [Id(10)] public string? Notes { get; set; }

    /// <summary>When the grain was first created.</summary>
    [Id(11)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modification timestamp.</summary>
    [Id(12)] public DateTime LastModifiedDate { get; set; }

    /// <summary>Which side was evaluated (Bilateral, Left, or Right).</summary>
    [Id(13)] public Laterality Side { get; set; } = Laterality.Bilateral;

    /// <summary>Exercises performed during this clinic session.</summary>
    [Id(14)] public List<ClinicExerciseLog> Exercises { get; set; } = new();

    /// <summary>Optional PT referral grain key this session is associated with.</summary>
    [Id(15)] public string? ReferralId { get; set; }
}

/// <summary>
/// A single range-of-motion measurement for one movement.
/// </summary>
[GenerateSerializer]
public class RomMeasurement
{
    /// <summary>The movement being measured (e.g., Flexion, Extension).</summary>
    [Id(0)] public Movement Movement { get; set; }

    /// <summary>Active ROM in degrees — what the patient achieves independently.</summary>
    [Id(1)] public decimal? ActiveRom { get; set; }

    /// <summary>Passive ROM in degrees — what the therapist achieves.</summary>
    [Id(2)] public decimal? PassiveRom { get; set; }

    /// <summary>Pain notation, e.g., "pain at end range", "sharp pain at 90 degrees".</summary>
    [Id(3)] public string? PainOnMotion { get; set; }

    /// <summary>Additional comments for this measurement.</summary>
    [Id(4)] public string? Comments { get; set; }
}

/// <summary>
/// A single manual muscle testing (MMT) strength measurement.
/// Grade scale: 0 (no contraction) to 5 (normal), with +/- modifiers mapped to ±0.33.
/// </summary>
[GenerateSerializer]
public class StrengthMeasurement
{
    /// <summary>The movement being tested.</summary>
    [Id(0)] public Movement Movement { get; set; }

    /// <summary>Numeric grade (0-5). +/- modifiers map to ±0.33 (e.g., 3+ = 3.33).</summary>
    [Id(1)] public decimal Grade { get; set; }

    /// <summary>Display string preserving original notation (e.g., "3+", "4-", "5").</summary>
    [Id(2)] public string GradeDisplay { get; set; } = string.Empty;

    /// <summary>Additional comments for this measurement.</summary>
    [Id(3)] public string? Comments { get; set; }
}

/// <summary>
/// Laterality for bilateral body groups (shoulders, elbows, wrists, hands, hips, knees, ankles, feet).
/// </summary>
[GenerateSerializer]
public enum Laterality
{
    Bilateral,
    Left,
    Right
}

/// <summary>
/// Lightweight index entry pointing to a PT session grain.
/// Stored in the PTSessionIndexGrain for fast lookups without activating session grains.
/// </summary>
[GenerateSerializer]
public class PTSessionIndexEntry
{
    /// <summary>The session grain key (e.g., "PTSESSION:PAT001:Shoulder:Left:20260403140000").</summary>
    [Id(0)] public string SessionGrainKey { get; set; } = string.Empty;

    /// <summary>When the session was recorded, for date-range queries.</summary>
    [Id(1)] public DateTime SessionDate { get; set; }

    /// <summary>Body group, for filtering.</summary>
    [Id(2)] public BodyGroup BodyGroup { get; set; }

    /// <summary>Which side was evaluated.</summary>
    [Id(3)] public Laterality Side { get; set; }
}

/// <summary>
/// State for the per-patient, per-body-group session index grain.
/// Maintains entries sorted by SessionDate descending (most recent first).
/// </summary>
[GenerateSerializer]
public class PTSessionIndexState
{
    /// <summary>Patient identifier (from grain key).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Body group (from grain key).</summary>
    [Id(1)] public BodyGroup BodyGroup { get; set; }

    /// <summary>Index entries sorted by SessionDate descending.</summary>
    [Id(2)] public List<PTSessionIndexEntry> Entries { get; set; } = new();

    /// <summary>Last modification timestamp.</summary>
    [Id(3)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A single exercise performed during a clinic PT session.
/// </summary>
[GenerateSerializer]
public class ClinicExerciseLog
{
    /// <summary>Name of the exercise (e.g., "Quad Sets", "Wall Slides").</summary>
    [Id(0)] public string ExerciseName { get; set; } = string.Empty;

    /// <summary>Optional description or instructions.</summary>
    [Id(1)] public string? Description { get; set; }

    /// <summary>Category of exercise.</summary>
    [Id(2)] public ExerciseCategory Category { get; set; }

    /// <summary>Target body group.</summary>
    [Id(3)] public BodyGroup BodyGroup { get; set; }

    /// <summary>Specific movement targeted, if applicable.</summary>
    [Id(4)] public Movement? Movement { get; set; }

    /// <summary>Number of sets performed.</summary>
    [Id(5)] public int? Sets { get; set; }

    /// <summary>Number of repetitions per set.</summary>
    [Id(6)] public int? Reps { get; set; }

    /// <summary>Weight or resistance in pounds.</summary>
    [Id(7)] public decimal? WeightLbs { get; set; }

    /// <summary>Duration in seconds, for time-based exercises (stretches, holds).</summary>
    [Id(8)] public int? DurationSeconds { get; set; }

    /// <summary>Distance in feet, for walking or endurance exercises.</summary>
    [Id(9)] public decimal? DistanceFeet { get; set; }

    /// <summary>Free-text notes about this exercise.</summary>
    [Id(10)] public string? Notes { get; set; }
}
