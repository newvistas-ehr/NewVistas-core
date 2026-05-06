// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all registered tumors.
/// Grain key pattern: "ONC-TUMOR-IDX:{patientId}"
/// </summary>
public interface IOncologyTumorIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all tumor index entries for this patient, ordered by diagnosis date descending.</summary>
    Task<List<OncologyTumorIndexEntry>> GetAllTumorsAsync();

    /// <summary>Returns only tumors with Active or Recurrence status.</summary>
    Task<List<OncologyTumorIndexEntry>> GetActiveTumorsAsync();

    /// <summary>Inserts or replaces the index entry for the given tumor.</summary>
    Task UpsertTumorAsync(OncologyTumorIndexEntry entry);

    /// <summary>Removes the index entry for the given tumor ID. No-op if not found.</summary>
    Task RemoveTumorAsync(string tumorId);
}
