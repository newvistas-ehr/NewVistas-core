// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient-level index of all Anatomic Pathology cases.
/// Grain key pattern: "AP-CASE-IDX:{patientId}"
/// </summary>
public interface IAnatomicPathologyCaseIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all AP case index entries for this patient.</summary>
    Task<List<APCaseIndexEntry>> GetAllCasesAsync();

    /// <summary>Returns only cases of a specific type (SP, CY, or AU).</summary>
    Task<List<APCaseIndexEntry>> GetCasesByTypeAsync(APCaseType caseType);

    /// <summary>Adds or updates a case entry in the index.</summary>
    Task UpsertCaseAsync(APCaseIndexEntry entry);

    /// <summary>Removes a case entry from the index (used when a case is cancelled).</summary>
    Task RemoveCaseAsync(string caseId);
}
