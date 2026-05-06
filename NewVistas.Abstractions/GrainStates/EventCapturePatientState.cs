// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight entry linking a patient to a single encounter record.
/// Stored in the per-patient encounter list.
/// </summary>
[GenerateSerializer]
public class EcPatientEncounterEntry
{
    /// <summary>Encounter ID — grain key of the encounter.</summary>
    [Id(0)]
    public string EncounterId { get; set; } = string.Empty;

    /// <summary>Encounter date/time — used for sorting in reverse-chronological order.</summary>
    [Id(1)]
    public DateTime EncounterDateTime { get; set; }
}

/// <summary>
/// Event Capture Patient State — per-patient index of all encounter IDs.
/// Corresponds to VistA File #721 (EC PATIENT) header record.
/// </summary>
[GenerateSerializer]
public class EventCapturePatientState
{
    /// <summary>
    /// Patient ID (.01) — grain key suffix, matches PATIENT file (#2).
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Encounter entries — list of encounters for this patient in reverse-chronological order.
    /// </summary>
    [Id(1)]
    public List<EcPatientEncounterEntry> EncounterEntries { get; set; } = new();

    /// <summary>
    /// Total visit count — running count of all encounters (including deleted).
    /// </summary>
    [Id(2)]
    public int TotalEncounters { get; set; }

    /// <summary>
    /// Last visit date — date of the most recent encounter.
    /// </summary>
    [Id(3)]
    public DateTime? LastEncounterDate { get; set; }

    /// <summary>Created date — when this patient EC record was first created.</summary>
    [Id(4)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Last modified date — updated when an encounter is added.</summary>
    [Id(5)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Searchable index entry for the application-wide Event Capture encounter index.
/// Stored in IEventCaptureEncounterIndexGrain.
/// </summary>
[GenerateSerializer]
public class EventCaptureIndexEntry
{
    [Id(0)]
    public string EncounterId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public DateTime EncounterDateTime { get; set; }

    [Id(3)]
    public string DssUnitId { get; set; } = string.Empty;

    [Id(4)]
    public string DssUnitName { get; set; } = string.Empty;

    [Id(5)]
    public string? DssUnitCode { get; set; }

    [Id(6)]
    public string? ClinicName { get; set; }

    [Id(7)]
    public string PrimaryProviderId { get; set; } = string.Empty;

    [Id(8)]
    public string PrimaryProviderName { get; set; } = string.Empty;

    [Id(9)]
    public EcEncounterType EncounterType { get; set; }

    [Id(10)]
    public EcEncounterStatus Status { get; set; }

    [Id(11)]
    public int ProcedureCount { get; set; }
}

/// <summary>
/// Index state wrapper stored by IEventCaptureEncounterIndexGrain.
/// </summary>
[GenerateSerializer]
public class EventCaptureIndexState
{
    [Id(0)]
    public List<EventCaptureIndexEntry> Entries { get; set; } = new();
}

/// <summary>
/// Index state wrapper stored by IDssUnitIndexGrain.
/// </summary>
[GenerateSerializer]
public class DssUnitIndexState
{
    [Id(0)]
    public List<DssUnitIndexEntry> Units { get; set; } = new();
}
