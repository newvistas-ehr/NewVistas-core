// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ResearchSubjectIndexState
{
    [Id(0)] public List<ResearchSubjectIndexEntry> Subjects { get; set; } = new();
}

public class ResearchSubjectIndexGrain : Grain, IResearchSubjectIndexGrain
{
    private readonly IPersistentState<ResearchSubjectIndexState> _state;

    public ResearchSubjectIndexGrain(
        [PersistentState("irbSubjectIndexState", "irbSubjectIndexStore")] IPersistentState<ResearchSubjectIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertSubjectAsync(ResearchSubjectIndexEntry entry)
    {
        ResearchSubjectIndexEntry? existing = _state.State.Subjects.Find(s => s.SubjectId == entry.SubjectId);
        if (existing is not null)
            _state.State.Subjects.Remove(existing);
        _state.State.Subjects.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ResearchSubjectIndexEntry>> GetAllSubjectsAsync()
    {
        List<ResearchSubjectIndexEntry> result = _state.State.Subjects
            .OrderBy(s => s.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ResearchSubjectIndexEntry>> GetActiveSubjectsAsync()
    {
        List<ResearchSubjectIndexEntry> result = _state.State.Subjects
            .Where(s => s.EnrollmentStatus == SubjectEnrollmentStatus.Enrolled
                || s.EnrollmentStatus == SubjectEnrollmentStatus.Active)
            .OrderBy(s => s.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ResearchSubjectIndexEntry>> GetSubjectsByStatusAsync(SubjectEnrollmentStatus status)
    {
        List<ResearchSubjectIndexEntry> result = _state.State.Subjects
            .Where(s => s.EnrollmentStatus == status)
            .OrderBy(s => s.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ResearchSubjectIndexEntry>> GetWithdrawnSubjectsAsync()
    {
        List<ResearchSubjectIndexEntry> result = _state.State.Subjects
            .Where(s => s.EnrollmentStatus == SubjectEnrollmentStatus.Withdrawn)
            .OrderBy(s => s.PatientName)
            .ToList();
        return Task.FromResult(result);
    }
}
