// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-registry-type population index grain — key: "CCR-IDX:{RegistryType}" (e.g., "CCR-IDX:HIV")
/// One instance per registry type; lists all enrolled patients for that registry.
/// </summary>
public interface IClinicalRegistryIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all entries for this registry, newest enrolled first.</summary>
    Task<List<CCREntrySummary>> GetAllEntriesAsync();

    /// <summary>Returns only Active enrollments.</summary>
    Task<List<CCREntrySummary>> GetActiveEntriesAsync();

    Task<List<CCREntrySummary>> GetByStatusAsync(CCREnrollmentStatus status);

    Task UpsertEntryAsync(CCREntrySummary entry);

    Task RemoveEntryAsync(string patientId);
}
