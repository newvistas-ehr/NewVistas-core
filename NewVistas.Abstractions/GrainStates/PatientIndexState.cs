// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight patient summary held in the singleton PatientIndexGrain.
/// Mirrors the output of the VistA ORWPT LOOKUP RPC (ORWPT.m):
///   NAME^DOB^SEX^DFN^ICN
/// Only the last 4 digits of SSN are stored here — never the full SSN.
/// </summary>
[GenerateSerializer]
public record PatientIndexEntry
{
    /// <summary>Orleans grain key for IPatientGrain / IPatientWorkflowGrain.</summary>
    [Id(0)]
    public string PatientId { get; init; } = string.Empty;

    /// <summary>Patient full name, VistA format: LAST,FIRST MI (e.g., "SMITH,JOHN A").</summary>
    [Id(1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Date of birth — used to disambiguate patients with the same name.</summary>
    [Id(2)]
    public DateTime? DateOfBirth { get; init; }

    /// <summary>Patient sex: M or F.</summary>
    [Id(3)]
    public string Sex { get; init; } = string.Empty;

    /// <summary>
    /// Last 4 digits of SSN only — supports SSN-last-4 search (VistA BS5 cross-reference).
    /// Full SSN is never stored in the index.
    /// </summary>
    [Id(4)]
    public string SsnLast4 { get; init; } = string.Empty;

    /// <summary>
    /// VistA Data File Number — local facility IEN from PATIENT file (#2).
    /// Null until DFN is assigned via SetDfnAsync.
    /// </summary>
    [Id(5)]
    public string? Dfn { get; init; }

    /// <summary>
    /// Integration Control Number from the VA Master Patient Index.
    /// Format: {10-digit}V{6-digit}. Null until MPI correlation completes.
    /// </summary>
    [Id(6)]
    public string? Icn { get; init; }

    /// <summary>False for deceased or deactivated patients.</summary>
    [Id(7)]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Singleton state for the PatientIndexGrain (key: "PATIENT-INDEX").
///
/// Maintains a cross-reference of all registered patients keyed by PatientId
/// for O(1) upsert. Searches iterate over .Values with LINQ, which is acceptable
/// at facility scale (10,000–200,000 patients ≈ 2–40 MB in memory).
///
/// Mirrors VistA cross-references on ^DPT:
///   "B"  — by last name (ORWPT LOOKUP name-prefix search)
///   "BS5" — by SSN last-4
///   Direct IEN access — by DFN
/// </summary>
[GenerateSerializer]
public class PatientIndexState
{
    /// <summary>
    /// All patient index entries keyed by PatientId (Orleans grain key).
    /// AddOrUpdate is O(1); searches are O(n) LINQ over .Values.
    /// </summary>
    [Id(0)]
    public Dictionary<string, PatientIndexEntry> Patients { get; set; } = new();
}
