// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single oncology treatment episode.
/// Maps to VistA Oncology Treatment file (#165.x).
/// MUMPS routine: ONCTREAT.m
/// Grain key pattern: "ONC-TX:{guid}"
/// </summary>
public interface IOncologyTreatmentGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this treatment record.</summary>
    Task<OncologyTreatmentState> GetTreatmentAsync();

    /// <summary>
    /// Creates the treatment record with planned details.
    /// Status is set to Planned until StartTreatmentAsync is called.
    /// ONCTREAT.m CREATE.
    /// </summary>
    Task CreateTreatmentAsync(
        string tumorId,
        string patientId,
        OncologyTreatmentType treatmentType,
        string agentName,
        string? doseDescription,
        string? providerId,
        string? providerName,
        string? facilityName,
        string? notes);

    /// <summary>Marks the treatment as Active and records the actual start date. ONCTREAT.m START.</summary>
    Task StartTreatmentAsync(DateTime startDate);

    /// <summary>
    /// Completes the treatment, records end date and final response assessment.
    /// Status transitions to Completed. ONCTREAT.m COMPLETE.
    /// </summary>
    Task CompleteTreatmentAsync(
        DateTime endDate,
        TreatmentResponseAssessment responseAssessment,
        string? notes);

    /// <summary>
    /// Discontinues the treatment early, records end date and reason.
    /// Status transitions to Discontinued. ONCTREAT.m DISCONTINUE.
    /// </summary>
    Task DiscontinueTreatmentAsync(
        DateTime endDate,
        string discontinuationReason,
        string? notes);

    /// <summary>Records a response assessment without ending the treatment. ONCTREAT.m RESPONSE.</summary>
    Task RecordResponseAsync(
        TreatmentResponseAssessment responseAssessment,
        DateTime assessmentDate,
        string? notes);

    /// <summary>Updates the number of chemotherapy/immunotherapy cycles completed.</summary>
    Task UpdateCyclesAsync(int cyclesCompleted);
}
