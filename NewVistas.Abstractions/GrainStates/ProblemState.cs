// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single problem entry embedded in the patient grain.
/// Based on VistA PROBLEM LIST file (#9000011).
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class ProblemEntry
{
    [Id(0)]
    public string ProblemId { get; set; } = string.Empty;

    /// <summary>
    /// Diagnosis (.01) � pointer to ICD DIAGNOSIS file (#80)
    /// </summary>
    [Id(2)]
    public string Diagnosis { get; set; } = string.Empty;

    [Id(3)]
    public string? DiagnosisCode { get; set; }

    /// <summary>
    /// Status � ACTIVE or INACTIVE
    /// </summary>
    [Id(4)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Date of Onset (.13)
    /// </summary>
    [Id(5)]
    public DateTime? DateOfOnset { get; set; }

    /// <summary>
    /// Date Resolved (1.07)
    /// </summary>
    [Id(6)]
    public DateTime? DateResolved { get; set; }

    /// <summary>
    /// Date Recorded (1.09)
    /// </summary>
    [Id(7)]
    public DateTime DateRecorded { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Recording Provider (.04)
    /// </summary>
    [Id(8)]
    public string? RecordingProviderId { get; set; }

    [Id(9)]
    public string? RecordingProviderName { get; set; }

    /// <summary>
    /// Responsible Provider (1.04)
    /// </summary>
    [Id(10)]
    public string? ResponsibleProviderId { get; set; }

    [Id(11)]
    public string? ResponsibleProviderName { get; set; }

    /// <summary>
    /// Service Connected (1.1)
    /// </summary>
    [Id(12)]
    public bool IsServiceConnected { get; set; }

    /// <summary>
    /// Condition � ACUTE, CHRONIC, TRANSCRIBED, PERMANENT, or HIDDEN
    /// </summary>
    [Id(13)]
    public string? Condition { get; set; }

    /// <summary>
    /// Location of encounter (.08)
    /// </summary>
    [Id(14)]
    public string? ClinicId { get; set; }

    [Id(15)]
    public string? ClinicName { get; set; }

    /// <summary>
    /// Priority � ACUTE or CHRONIC
    /// </summary>
    [Id(16)]
    public string? Priority { get; set; }

    [Id(17)]
    public string? Comments { get; set; }

    [Id(18)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// problem on the patient grain does not retroactively mutate the
    /// historical <see cref="ProblemEntry"/> snapshot stored on a clinical
    /// event payload (which would break the hash chain).
    /// </summary>
    public ProblemEntry Clone() => new()
    {
        ProblemId = ProblemId,
        Diagnosis = Diagnosis,
        DiagnosisCode = DiagnosisCode,
        Status = Status,
        DateOfOnset = DateOfOnset,
        DateResolved = DateResolved,
        DateRecorded = DateRecorded,
        RecordingProviderId = RecordingProviderId,
        RecordingProviderName = RecordingProviderName,
        ResponsibleProviderId = ResponsibleProviderId,
        ResponsibleProviderName = ResponsibleProviderName,
        IsServiceConnected = IsServiceConnected,
        Condition = Condition,
        ClinicId = ClinicId,
        ClinicName = ClinicName,
        Priority = Priority,
        Comments = Comments,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate
    };
}
