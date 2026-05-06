// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the per-patient vital index grain.
/// Stores vital grain keys sorted by datetime descending.
/// </summary>
[GenerateSerializer]
public class PatientVitalIndexState
{
    /// <summary>
    /// Patient ID this index belongs to
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// All vital index entries, sorted by DateTimeTaken descending (most recent first).
    /// </summary>
    [Id(1)]
    public List<VitalIndexEntry> Entries { get; set; } = new();

    [Id(2)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight index entry for a vital measurement.
/// Stores enough metadata to support filtering without activating the vital grain.
/// </summary>
[GenerateSerializer]
public class VitalIndexEntry
{
    /// <summary>
    /// The grain key of the vital measurement grain.
    /// Format: VITAL:{patientId}:{yyyyMMddHHmmss}:{vitalType}
    /// </summary>
    [Id(0)]
    public string VitalGrainKey { get; set; } = string.Empty;

    /// <summary>
    /// When the vital was taken — for range queries.
    /// </summary>
    [Id(1)]
    public DateTime DateTimeTaken { get; set; }

    /// <summary>
    /// Vital type — TEMPERATURE, PULSE, BLOOD PRESSURE, etc.
    /// </summary>
    [Id(2)]
    public string VitalType { get; set; } = string.Empty;
}
