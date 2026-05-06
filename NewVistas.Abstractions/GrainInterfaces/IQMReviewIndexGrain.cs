// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// System-wide index of all quality management peer reviews and RCA records.
/// Grain key: "QM-REVIEW-IDX" (singleton)
/// </summary>
public interface IQMReviewIndexGrain : IGrainWithStringKey
{
    /// <summary>Inserts or updates a review summary entry in the index.</summary>
    Task UpsertReviewAsync(GrainStates.QMReviewIndexEntry entry);

    /// <summary>Returns all review summaries, newest first.</summary>
    Task<List<GrainStates.QMReviewIndexEntry>> GetAllReviewsAsync();

    /// <summary>Returns all reviews linked to a specific incident.</summary>
    Task<List<GrainStates.QMReviewIndexEntry>> GetReviewsForIncidentAsync(string incidentId);

    /// <summary>Returns reviews with Pending or InProgress status.</summary>
    Task<List<GrainStates.QMReviewIndexEntry>> GetPendingReviewsAsync();

    /// <summary>Returns reviews whose due date has passed and are not yet Completed or Approved.</summary>
    Task<List<GrainStates.QMReviewIndexEntry>> GetOverdueReviewsAsync();
}
