// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of Social Work Assessments.
/// Key: "SW-ASSESSMENT-IDX:{patientId}"
/// </summary>
public interface ISocialWorkAssessmentIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetAllAsync();

    Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetByTypeAsync(
        GrainStates.SocialWorkAssessmentType assessmentType);

    Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetByStatusAsync(
        GrainStates.SocialWorkAssessmentStatus status);

    Task AddEntryAsync(GrainStates.SocialWorkAssessmentIndexEntry entry);

    Task UpdateEntryStatusAsync(
        string assessmentId,
        GrainStates.SocialWorkAssessmentStatus status);
}
