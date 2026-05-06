// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Nursing shift type.
/// </summary>
[GenerateSerializer]
public enum NursingShift
{
    Day = 0,       // 0700–1500
    Evening = 1,   // 1500–2300
    Night = 2,     // 2300–0700
}

/// <summary>
/// Status of a shift handoff report.
/// </summary>
[GenerateSerializer]
public enum ShiftHandoffStatus
{
    Draft = 0,
    Completed = 1,
    Acknowledged = 2,
}

/// <summary>
/// SBAR-structured patient summary within a shift handoff.
/// SBAR = Situation, Background, Assessment, Recommendation.
/// </summary>
[GenerateSerializer]
public record SbarPatientSummary
{
    /// <summary>S — Situation: current status, chief concern, what's happening now.</summary>
    [Id(0)] public string Situation { get; init; } = string.Empty;

    /// <summary>B — Background: relevant history, diagnoses, recent events.</summary>
    [Id(1)] public string Background { get; init; } = string.Empty;

    /// <summary>A — Assessment: nurse's clinical assessment, current condition, trends.</summary>
    [Id(2)] public string Assessment { get; init; } = string.Empty;

    /// <summary>R — Recommendation: what needs to happen next shift, pending items.</summary>
    [Id(3)] public string Recommendation { get; init; } = string.Empty;
}

/// <summary>
/// Key clinical data snapshot included in handoff.
/// </summary>
[GenerateSerializer]
public record HandoffClinicalSnapshot
{
    /// <summary>Latest vital signs summary.</summary>
    [Id(0)] public string? VitalsSummary { get; init; }

    /// <summary>Current pain score (0-10).</summary>
    [Id(1)] public int? PainScore { get; init; }

    /// <summary>Current acuity level.</summary>
    [Id(2)] public string? AcuityLevel { get; init; }

    /// <summary>Active nursing diagnoses from care plan.</summary>
    [Id(3)] public List<string> ActiveNursingDiagnoses { get; init; } = new();

    /// <summary>Pending tasks from worklist.</summary>
    [Id(4)] public List<string> PendingTasks { get; init; } = new();

    /// <summary>Due medications summary.</summary>
    [Id(5)] public List<string> DueMedications { get; init; } = new();

    /// <summary>Fall risk level.</summary>
    [Id(6)] public string? FallRiskLevel { get; init; }

    /// <summary>Isolation/precaution status.</summary>
    [Id(7)] public string? IsolationStatus { get; init; }

    /// <summary>IV access / lines.</summary>
    [Id(8)] public string? IvAccessNotes { get; init; }

    /// <summary>Diet orders.</summary>
    [Id(9)] public string? DietOrders { get; init; }

    /// <summary>Code status (Full Code, DNR, DNI, Comfort Care).</summary>
    [Id(10)] public string? CodeStatus { get; init; }
}

/// <summary>
/// State for a nursing shift handoff/report (SBAR-based).
/// Captures end-of-shift patient status for seamless nurse-to-nurse transfer.
/// VistA: NUR shift report functionality.
/// Grain key: "NURS-HANDOFF:{guid}"
/// </summary>
[GenerateSerializer]
public class NursingShiftHandoffState
{
    /// <summary>Unique handoff ID.</summary>
    [Id(0)] public string HandoffId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Shift being reported on.</summary>
    [Id(2)] public NursingShift Shift { get; set; }

    /// <summary>Date of the shift.</summary>
    [Id(3)] public DateTime ShiftDate { get; set; }

    /// <summary>Report status.</summary>
    [Id(4)] public ShiftHandoffStatus Status { get; set; }

    /// <summary>Outgoing nurse (giving report).</summary>
    [Id(5)] public string OutgoingNurseId { get; set; } = string.Empty;

    /// <summary>Outgoing nurse display name.</summary>
    [Id(6)] public string OutgoingNurseName { get; set; } = string.Empty;

    /// <summary>Incoming nurse (receiving report).</summary>
    [Id(7)] public string? IncomingNurseId { get; set; }

    /// <summary>Incoming nurse display name.</summary>
    [Id(8)] public string? IncomingNurseName { get; set; }

    /// <summary>Ward/unit location.</summary>
    [Id(9)] public string? LocationId { get; set; }

    /// <summary>Location name.</summary>
    [Id(10)] public string? LocationName { get; set; }

    /// <summary>Bed assignment.</summary>
    [Id(11)] public string? BedNumber { get; set; }

    /// <summary>SBAR-structured patient summary.</summary>
    [Id(12)] public SbarPatientSummary? Sbar { get; set; }

    /// <summary>Clinical data snapshot at time of handoff.</summary>
    [Id(13)] public HandoffClinicalSnapshot? ClinicalSnapshot { get; set; }

    /// <summary>Safety concerns flagged for incoming nurse.</summary>
    [Id(14)] public List<string> SafetyConcerns { get; set; } = new();

    /// <summary>Date/time the report was completed by outgoing nurse.</summary>
    [Id(15)] public DateTime? CompletedDate { get; set; }

    /// <summary>Date/time the report was acknowledged by incoming nurse.</summary>
    [Id(16)] public DateTime? AcknowledgedDate { get; set; }

    /// <summary>Additional notes.</summary>
    [Id(17)] public string? Notes { get; set; }

    /// <summary>When created.</summary>
    [Id(18)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(19)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight handoff index entry.</summary>
[GenerateSerializer]
public record ShiftHandoffIndexEntry
{
    [Id(0)] public string HandoffId { get; init; } = string.Empty;
    [Id(1)] public NursingShift Shift { get; init; }
    [Id(2)] public DateTime ShiftDate { get; init; }
    [Id(3)] public string OutgoingNurseName { get; init; } = string.Empty;
    [Id(4)] public string? IncomingNurseName { get; init; }
    [Id(5)] public ShiftHandoffStatus Status { get; init; }
}

/// <summary>
/// Per-patient shift handoff index.
/// Grain key: "NURS-HANDOFF-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class ShiftHandoffIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<ShiftHandoffIndexEntry> Entries { get; set; } = new();
}
