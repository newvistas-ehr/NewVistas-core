// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary entry stored in the patient's AP case index
/// </summary>
[GenerateSerializer]
public class APCaseIndexEntry
{
    /// <summary>Case grain key</summary>
    [Id(0)]
    public string CaseId { get; set; } = string.Empty;

    /// <summary>Lab-assigned accession number (e.g. SP-2024-00123)</summary>
    [Id(1)]
    public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Case type: SurgicalPathology, Cytology, or Autopsy</summary>
    [Id(2)]
    public APCaseType CaseType { get; set; }

    /// <summary>Workflow status of the case</summary>
    [Id(3)]
    public APCaseStatus Status { get; set; }

    /// <summary>Date specimen was received</summary>
    [Id(4)]
    public DateTime? DateReceived { get; set; }

    /// <summary>Date final report was issued</summary>
    [Id(5)]
    public DateTime? DateReported { get; set; }

    /// <summary>Anatomical source of specimen</summary>
    [Id(6)]
    public string? SpecimenSource { get; set; }

    /// <summary>Primary diagnosis (abbreviated for display)</summary>
    [Id(7)]
    public string? PrimaryDiagnosis { get; set; }

    /// <summary>Name of pathologist who signed out the case</summary>
    [Id(8)]
    public string? PathologistName { get; set; }
}

/// <summary>
/// Patient-level index grain state listing all AP cases for a patient.
/// Grain key: "AP-CASE-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class AnatomicPathologyCaseIndexState
{
    /// <summary>Patient IEN</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>All AP cases for this patient, ordered by DateReceived descending</summary>
    [Id(1)]
    public List<APCaseIndexEntry> Cases { get; set; } = new();

    /// <summary>Date/time the index was last modified</summary>
    [Id(2)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
