// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// SCI Registry Index Grain — singleton listing all patients enrolled in the SCI/D registry.
///
/// Grain key: "SCI-INDEX"
/// </summary>
public interface ISCIIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all entries in the SCI/D registry index.</summary>
    Task<List<SCIIndexEntry>> GetAllAsync();

    /// <summary>Returns entries filtered by enrollment status.</summary>
    Task<List<SCIIndexEntry>> GetByStatusAsync(SCIRegistryStatus status);

    /// <summary>Returns entries filtered by neurological level (case-insensitive prefix match, e.g., "C" returns C1-C8).</summary>
    Task<List<SCIIndexEntry>> GetByNeurologicalLevelAsync(string levelPrefix);

    /// <summary>Adds a new entry when a patient is enrolled in the registry.</summary>
    Task AddEntryAsync(SCIIndexEntry entry);

    /// <summary>Updates the index entry status and most recent clinical summary fields.</summary>
    Task UpdateEntryAsync(
        string patientId,
        SCIRegistryStatus status,
        string neurologicalLevel,
        SCIAisGrade aisGrade);
}
