// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Category of a nursing task.
/// </summary>
[GenerateSerializer]
public enum NursingTaskCategory
{
    Medication = 0,
    VitalSigns = 1,
    Assessment = 2,
    Intervention = 3,
    Lab = 4,
    Wound = 5,
    PatientEducation = 6,
    Discharge = 7,
    Other = 8,
}

/// <summary>
/// Status of a nursing task.
/// </summary>
[GenerateSerializer]
public enum NursingTaskStatus
{
    Due = 0,
    Overdue = 1,
    Completed = 2,
    Missed = 3,
    Deferred = 4,
    Cancelled = 5,
}

/// <summary>
/// Priority of a nursing task.
/// </summary>
[GenerateSerializer]
public enum NursingTaskPriority
{
    Routine = 0,
    Urgent = 1,
    STAT = 2,
}

/// <summary>
/// A single nursing task in the worklist.
/// </summary>
[GenerateSerializer]
public record NursingTask
{
    /// <summary>Unique task ID.</summary>
    [Id(0)] public string TaskId { get; init; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; init; } = string.Empty;

    /// <summary>Task category.</summary>
    [Id(2)] public NursingTaskCategory Category { get; init; }

    /// <summary>Task priority.</summary>
    [Id(3)] public NursingTaskPriority Priority { get; init; }

    /// <summary>Current task status.</summary>
    [Id(4)] public NursingTaskStatus Status { get; init; }

    /// <summary>Description of the task.</summary>
    [Id(5)] public string Description { get; init; } = string.Empty;

    /// <summary>Source of the task (e.g., order ID, care plan intervention ID, MAR entry).</summary>
    [Id(6)] public string? SourceId { get; init; }

    /// <summary>Source type (MAR, CAREPLAN, ORDER, VITAL_SCHEDULE, etc.).</summary>
    [Id(7)] public string? SourceType { get; init; }

    /// <summary>When the task is due.</summary>
    [Id(8)] public DateTime DueDateTime { get; init; }

    /// <summary>When the task was completed (null if not yet).</summary>
    [Id(9)] public DateTime? CompletedDateTime { get; init; }

    /// <summary>ID of nurse who completed the task.</summary>
    [Id(10)] public string? CompletedByNurseId { get; init; }

    /// <summary>Name of nurse who completed the task.</summary>
    [Id(11)] public string? CompletedByNurseName { get; init; }

    /// <summary>Additional notes.</summary>
    [Id(12)] public string? Notes { get; init; }
}

/// <summary>
/// Aggregated nursing task worklist for a single patient.
/// Combines tasks from MAR (medications due), vitals schedule, care plan interventions,
/// and other ordered nursing activities.
/// Grain key: "NURS-TASKS:{patientId}"
/// </summary>
[GenerateSerializer]
public class NursingTaskWorklistState
{
    /// <summary>Patient ID.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All tasks for this patient.</summary>
    [Id(1)] public List<NursingTask> Tasks { get; set; } = new();

    /// <summary>When the worklist was last generated/refreshed.</summary>
    [Id(2)] public DateTime LastRefreshedDate { get; set; }
}
