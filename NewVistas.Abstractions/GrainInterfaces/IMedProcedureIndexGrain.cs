// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all Medicine procedure records.
/// Grain key pattern: "MED-PROC-IDX:{patientId}"
/// </summary>
public interface IMedProcedureIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all procedure summaries for this patient, ordered by ordered date descending.</summary>
    Task<List<MedProcedureIndexEntry>> GetAllProceduresAsync();

    /// <summary>Returns procedure summaries filtered by category.</summary>
    Task<List<MedProcedureIndexEntry>> GetProceduresByCategoryAsync(MedProcedureCategory category);

    /// <summary>Returns only completed procedure summaries.</summary>
    Task<List<MedProcedureIndexEntry>> GetCompletedProceduresAsync();

    /// <summary>Adds or updates a procedure entry in this index.</summary>
    Task UpsertProcedureAsync(MedProcedureIndexEntry entry);

    /// <summary>Removes a procedure entry from this index. Idempotent.</summary>
    Task RemoveProcedureAsync(string procedureId);
}
