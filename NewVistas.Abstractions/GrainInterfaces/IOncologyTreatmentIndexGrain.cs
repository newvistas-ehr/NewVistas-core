// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all oncology treatment episodes.
/// Grain key pattern: "ONC-TX-IDX:{patientId}"
/// </summary>
public interface IOncologyTreatmentIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all treatment index entries for this patient, ordered by start date descending.</summary>
    Task<List<OncologyTreatmentIndexEntry>> GetAllTreatmentsAsync();

    /// <summary>Returns treatment entries linked to a specific tumor.</summary>
    Task<List<OncologyTreatmentIndexEntry>> GetTreatmentsByTumorAsync(string tumorId);

    /// <summary>Inserts or replaces the index entry for the given treatment.</summary>
    Task UpsertTreatmentAsync(OncologyTreatmentIndexEntry entry);

    /// <summary>Removes the index entry for the given treatment ID. No-op if not found.</summary>
    Task RemoveTreatmentAsync(string treatmentId);
}
