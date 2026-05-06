// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Exam status in the radiology tech workflow — more granular than RadiologyState.Status.
/// Maps to VistA RARTE.m exam status progression.
/// </summary>
[GenerateSerializer]
public enum RadExamStatus
{
    Ordered = 0,
    Scheduled = 1,
    PatientPrepped = 2,
    InProgress = 3,
    ExamComplete = 4,
    ImagesSentToPacs = 5,
    PendingRead = 6,
    ReadComplete = 7,
    Cancelled = 8,
}

/// <summary>
/// A single radiology tech worklist item.
/// </summary>
[GenerateSerializer]
public record RadWorklistItem
{
    [Id(0)] public string RadiologyId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string ProcedureName { get; init; } = string.Empty;
    [Id(3)] public string? ImagingType { get; init; }
    [Id(4)] public string? CptCode { get; init; }
    [Id(5)] public string? Urgency { get; init; }
    [Id(6)] public RadExamStatus ExamStatus { get; init; }
    [Id(7)] public string? BodyPart { get; init; }
    [Id(8)] public string? Laterality { get; init; }
    [Id(9)] public string? ProtocolName { get; init; }
    [Id(10)] public string? TechnicianName { get; init; }
    [Id(11)] public DateTime OrderDateTime { get; init; }
    [Id(12)] public DateTime? ScheduledDateTime { get; init; }
    [Id(13)] public string? Room { get; init; }
}

/// <summary>
/// Rad tech worklist state — per-location queue of pending exams.
/// Grain key: "RAD-WORKLIST:{locationId}"
/// </summary>
[GenerateSerializer]
public class RadWorklistState
{
    [Id(0)] public string LocationId { get; set; } = string.Empty;
    [Id(1)] public List<RadWorklistItem> Items { get; set; } = new();
    [Id(2)] public DateTime LastRefreshedDate { get; set; }
}

/// <summary>
/// Radiology exam tracking state — detailed exam status and protocol assignment.
/// Extends the base RadiologyGrain with tech-specific workflow fields.
/// Grain key: "RAD-EXAM:{radiologyId}"
/// </summary>
[GenerateSerializer]
public class RadExamTrackingState
{
    /// <summary>Radiology order ID (same as RadiologyGrain key).</summary>
    [Id(0)] public string RadiologyId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Current detailed exam status.</summary>
    [Id(2)] public RadExamStatus ExamStatus { get; set; }

    /// <summary>Scheduled exam date/time.</summary>
    [Id(3)] public DateTime? ScheduledDateTime { get; set; }

    /// <summary>Room/modality assigned.</summary>
    [Id(4)] public string? Room { get; set; }

    /// <summary>Assigned imaging protocol.</summary>
    [Id(5)] public string? ProtocolId { get; set; }

    /// <summary>Protocol name.</summary>
    [Id(6)] public string? ProtocolName { get; set; }

    /// <summary>Protocol parameters (e.g., kVp, mA, slice thickness, contrast timing).</summary>
    [Id(7)] public string? ProtocolParameters { get; set; }

    /// <summary>Date/time patient was prepped.</summary>
    [Id(8)] public DateTime? PatientPreppedDateTime { get; set; }

    /// <summary>Date/time exam started.</summary>
    [Id(9)] public DateTime? ExamStartDateTime { get; set; }

    /// <summary>Date/time exam completed.</summary>
    [Id(10)] public DateTime? ExamCompleteDateTime { get; set; }

    /// <summary>Date/time images sent to PACS.</summary>
    [Id(11)] public DateTime? ImagesSentDateTime { get; set; }

    /// <summary>Number of images acquired.</summary>
    [Id(12)] public int? ImageCount { get; set; }

    /// <summary>Linked image IDs from the ImagingGrain.</summary>
    [Id(13)] public List<string> LinkedImageIds { get; set; } = new();

    /// <summary>Tech notes during exam.</summary>
    [Id(14)] public string? TechNotes { get; set; }

    /// <summary>Patient prep notes (NPO, contrast allergies checked, IV access confirmed).</summary>
    [Id(15)] public string? PrepNotes { get; set; }

    /// <summary>When created.</summary>
    [Id(16)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Imaging protocol definition.
/// Grain key: "RAD-PROTOCOL:{protocolId}"
/// </summary>
[GenerateSerializer]
public class RadProtocolState
{
    [Id(0)] public string ProtocolId { get; set; } = string.Empty;
    [Id(1)] public string ProtocolName { get; set; } = string.Empty;
    [Id(2)] public string ImagingType { get; set; } = string.Empty;
    [Id(3)] public string? BodyPart { get; set; }
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public string? Parameters { get; set; }
    [Id(6)] public bool IsActive { get; set; } = true;
    [Id(7)] public DateTime CreatedDate { get; set; }
    [Id(8)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight protocol index entry.</summary>
[GenerateSerializer]
public record RadProtocolIndexEntry
{
    [Id(0)] public string ProtocolId { get; init; } = string.Empty;
    [Id(1)] public string ProtocolName { get; init; } = string.Empty;
    [Id(2)] public string ImagingType { get; init; } = string.Empty;
    [Id(3)] public string? BodyPart { get; init; }
    [Id(4)] public bool IsActive { get; init; }
}

/// <summary>
/// Singleton protocol index.
/// Grain key: "RAD-PROTOCOL-INDEX"
/// </summary>
[GenerateSerializer]
public class RadProtocolIndexState
{
    [Id(0)] public List<RadProtocolIndexEntry> Entries { get; set; } = new();
}
