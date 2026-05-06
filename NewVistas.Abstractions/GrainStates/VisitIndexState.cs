// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight summary of a single PCE encounter visit, stored in the patient visit index.
/// Mirrors the key display fields from the VistA PCE VISIT file (#9000010).
/// </summary>
[GenerateSerializer]
public class PceVisitEntry
{
    /// <summary>Visit grain key suffix (guid)</summary>
    [Id(0)]
    public string VisitId { get; set; } = string.Empty;

    /// <summary>Visit/Admit Date &amp; Time (.01)</summary>
    [Id(1)]
    public DateTime VisitDateTime { get; set; }

    /// <summary>Service Category (.07): A=Ambulatory, H=Inpatient, T=Telehealth, etc.</summary>
    [Id(2)]
    public string ServiceCategory { get; set; } = string.Empty;

    /// <summary>Hospital Location name (.22)</summary>
    [Id(3)]
    public string? LocationName { get; set; }

    /// <summary>Primary provider display name</summary>
    [Id(4)]
    public string? PrimaryProviderName { get; set; }

    /// <summary>Visit status: OPEN, CHECKED OUT, CANCELLED</summary>
    [Id(5)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Number of diagnoses (V POV) attached to this encounter</summary>
    [Id(6)]
    public int DiagnosisCount { get; set; }

    /// <summary>Number of procedures (V CPT) attached to this encounter</summary>
    [Id(7)]
    public int ProcedureCount { get; set; }
}

/// <summary>
/// State for the per-patient PCE visit index grain.
/// Grain key: "PCE-VISITS:{patientId}"
/// </summary>
[GenerateSerializer]
public class PatientVisitIndexState
{
    [Id(0)]
    public List<PceVisitEntry> Visits { get; set; } = new();
}
