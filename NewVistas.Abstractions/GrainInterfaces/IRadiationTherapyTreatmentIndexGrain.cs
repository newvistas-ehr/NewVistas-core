// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-course index of all radiation therapy treatment fractions.
/// Grain key pattern: "RT-TX-IDX:{courseId}"
/// </summary>
public interface IRadiationTherapyTreatmentIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all fraction summaries for this course, ordered by fraction number ascending.</summary>
    Task<List<RtTreatmentIndexEntry>> GetAllTreatmentsAsync();

    /// <summary>Returns only delivered fractions for this course.</summary>
    Task<List<RtTreatmentIndexEntry>> GetDeliveredTreatmentsAsync();

    /// <summary>Returns the cumulative delivered dose in cGy for this course.</summary>
    Task<int> GetTotalDeliveredDoseCgyAsync();

    /// <summary>Returns the count of delivered fractions for this course.</summary>
    Task<int> GetDeliveredFractionCountAsync();

    /// <summary>Adds or updates a fraction entry in this index.</summary>
    Task UpsertTreatmentAsync(RtTreatmentIndexEntry entry);

    /// <summary>Removes a fraction entry from this index. Idempotent.</summary>
    Task RemoveTreatmentAsync(string treatmentId);
}
