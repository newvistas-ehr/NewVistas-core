// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-study index of research subjects.
/// Key pattern: "IRB-SUBJECT-IDX:{studyId}".
/// </summary>
public interface IResearchSubjectIndexGrain : IGrainWithStringKey
{
    Task UpsertSubjectAsync(ResearchSubjectIndexEntry entry);
    Task<List<ResearchSubjectIndexEntry>> GetAllSubjectsAsync();
    Task<List<ResearchSubjectIndexEntry>> GetActiveSubjectsAsync();
    Task<List<ResearchSubjectIndexEntry>> GetSubjectsByStatusAsync(SubjectEnrollmentStatus status);
    Task<List<ResearchSubjectIndexEntry>> GetWithdrawnSubjectsAsync();
}
