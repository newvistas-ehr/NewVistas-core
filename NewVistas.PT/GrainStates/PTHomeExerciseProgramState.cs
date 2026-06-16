// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainStates;

/// <summary>
/// State for the per-patient home exercise program grain.
/// Holds all prescribed exercises and completion logs.
/// </summary>
[GenerateSerializer]
public class PTHomeExerciseProgramState
{
    /// <summary>Patient identifier (from grain key).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All exercise prescriptions for this patient.</summary>
    [Id(1)] public List<HepPrescription> Prescriptions { get; set; } = new();

    /// <summary>Completion logs for prescribed exercises.</summary>
    [Id(2)] public List<HepCompletionLog> CompletionLogs { get; set; } = new();

    /// <summary>Last modification timestamp.</summary>
    [Id(3)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A single home exercise prescription assigned by a therapist.
/// </summary>
[GenerateSerializer]
public class HepPrescription
{
    /// <summary>Unique prescription identifier (GUID).</summary>
    [Id(0)] public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Name of the exercise (e.g., "Quad Sets", "Hamstring Stretch").</summary>
    [Id(1)] public string ExerciseName { get; set; } = string.Empty;

    /// <summary>Detailed instructions for performing the exercise.</summary>
    [Id(2)] public string Instructions { get; set; } = string.Empty;

    /// <summary>How often the exercise should be performed (e.g., "2x daily", "3x weekly").</summary>
    [Id(3)] public string Frequency { get; set; } = string.Empty;

    /// <summary>Number of sets per session, if applicable.</summary>
    [Id(4)] public int? Sets { get; set; }

    /// <summary>Number of repetitions per set, if applicable.</summary>
    [Id(5)] public int? Reps { get; set; }

    /// <summary>Duration in seconds per session, for time-based exercises.</summary>
    [Id(6)] public int? DurationSeconds { get; set; }

    /// <summary>Target body group.</summary>
    [Id(7)] public BodyGroup BodyGroup { get; set; }

    /// <summary>Specific movement targeted, if applicable.</summary>
    [Id(8)] public Movement? Movement { get; set; }

    /// <summary>Which side (Bilateral, Left, Right).</summary>
    [Id(9)] public Laterality Side { get; set; } = Laterality.Bilateral;

    /// <summary>Category of exercise.</summary>
    [Id(10)] public ExerciseCategory Category { get; set; }

    /// <summary>Current prescription status.</summary>
    [Id(11)] public HepStatus Status { get; set; } = HepStatus.Active;

    /// <summary>Date the exercise was prescribed.</summary>
    [Id(12)] public DateTime PrescribedDate { get; set; }

    /// <summary>Name of the prescribing therapist.</summary>
    [Id(13)] public string PrescribedBy { get; set; } = string.Empty;

    /// <summary>Free-text notes.</summary>
    [Id(14)] public string? Notes { get; set; }

    /// <summary>Last modification timestamp.</summary>
    [Id(15)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// A log entry recording completion of a home exercise.
/// </summary>
[GenerateSerializer]
public class HepCompletionLog
{
    /// <summary>Unique log entry identifier (GUID).</summary>
    [Id(0)] public string LogId { get; set; } = string.Empty;

    /// <summary>The prescription this completion is for.</summary>
    [Id(1)] public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>When the exercise was completed.</summary>
    [Id(2)] public DateTime CompletedDate { get; set; }

    /// <summary>Who logged the completion (patient name or therapist name).</summary>
    [Id(3)] public string CompletedBy { get; set; } = string.Empty;

    /// <summary>Number of sets actually completed.</summary>
    [Id(4)] public int? SetsCompleted { get; set; }

    /// <summary>Number of reps actually completed.</summary>
    [Id(5)] public int? RepsCompleted { get; set; }

    /// <summary>Duration in seconds actually completed.</summary>
    [Id(6)] public int? DurationSecondsCompleted { get; set; }

    /// <summary>Pain level during exercise (0-10 scale).</summary>
    [Id(7)] public int? PainLevel { get; set; }

    /// <summary>Free-text notes about the completion.</summary>
    [Id(8)] public string? Notes { get; set; }
}
