// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient TBI screening index grain — key: "TBI-SCREEN-IDX:{patientId}"
/// Maintains a list of all TBI screening summaries for a given patient.
/// </summary>
public interface ITBIScreeningIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all screenings for this patient, newest first.</summary>
    Task<List<TBIScreeningSummaryEntry>> GetAllScreeningsAsync();

    /// <summary>Returns only screenings with result PositiveRequiresEvaluation.</summary>
    Task<List<TBIScreeningSummaryEntry>> GetPositiveScreeningsAsync();

    Task UpsertScreeningAsync(TBIScreeningSummaryEntry entry);

    Task RemoveScreeningAsync(string screeningId);
}
