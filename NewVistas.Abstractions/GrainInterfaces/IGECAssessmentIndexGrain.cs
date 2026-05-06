// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient GEC assessment history index.
/// Key pattern: "GEC-ASSESS-IDX:{patientId}".
/// </summary>
public interface IGECAssessmentIndexGrain : IGrainWithStringKey
{
    Task UpsertAssessmentAsync(GECAssessmentIndexEntry entry);
    Task<List<GECAssessmentIndexEntry>> GetAllAssessmentsAsync();
    Task<List<GECAssessmentIndexEntry>> GetAssessmentsByTypeAsync(GECAssessmentType type);
    Task<GECAssessmentIndexEntry?> GetLatestAssessmentAsync();
}
