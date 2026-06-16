// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
