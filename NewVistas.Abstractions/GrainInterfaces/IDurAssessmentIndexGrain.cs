// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for Drug Utilization Review assessments.
///
/// Provides efficient querying of all DUR assessments for a patient
/// without activating individual assessment grains.
///
/// Key format: DUR-IDX:{patientId}
/// Store: durAssessmentIndexStore
/// </summary>
public interface IDurAssessmentIndexGrain : IGrainWithStringKey
{
    /// <summary>Gets all DUR assessment index entries for this patient.</summary>
    Task<List<GrainStates.DurAssessmentIndexEntry>> GetAllAsync();

    /// <summary>Gets DUR assessments that are pending review (status = Pending or Failed).</summary>
    Task<List<GrainStates.DurAssessmentIndexEntry>> GetPendingReviewAsync();

    /// <summary>Gets DUR assessments filtered by status.</summary>
    Task<List<GrainStates.DurAssessmentIndexEntry>> GetByStatusAsync(GrainStates.DurAssessmentStatus status);

    /// <summary>Gets the DUR assessment for a specific prescription.</summary>
    Task<GrainStates.DurAssessmentIndexEntry?> GetByPrescriptionAsync(string prescriptionId);

    /// <summary>Adds a new entry to the index. Called by the workflow grain after creating an assessment.</summary>
    Task AddEntryAsync(GrainStates.DurAssessmentIndexEntry entry);

    /// <summary>Updates the status of an existing entry in the index.</summary>
    Task UpdateEntryStatusAsync(
        string assessmentId,
        GrainStates.DurAssessmentStatus status,
        GrainStates.DurOutcome overallOutcome,
        int failedCheckCount,
        int warningCheckCount);
}
