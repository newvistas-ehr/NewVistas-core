// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ResearchStudyIndexState
{
    [Id(0)] public List<IrbStudyIndexEntry> Studies { get; set; } = new();
}

public class ResearchStudyIndexGrain : Grain, IResearchStudyIndexGrain
{
    private readonly IPersistentState<ResearchStudyIndexState> _state;

    public ResearchStudyIndexGrain(
        [PersistentState("irbStudyIndexState", "irbStudyIndexStore")] IPersistentState<ResearchStudyIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertStudyAsync(IrbStudyIndexEntry entry)
    {
        IrbStudyIndexEntry? existing = _state.State.Studies.Find(s => s.StudyId == entry.StudyId);
        if (existing is not null)
            _state.State.Studies.Remove(existing);
        _state.State.Studies.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<IrbStudyIndexEntry>> GetAllStudiesAsync()
    {
        List<IrbStudyIndexEntry> result = _state.State.Studies
            .OrderBy(s => s.Title)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<IrbStudyIndexEntry>> GetOpenStudiesAsync()
    {
        List<IrbStudyIndexEntry> result = _state.State.Studies
            .Where(s => s.Status == IrbStudyStatus.OpenForEnrollment)
            .OrderBy(s => s.Title)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<IrbStudyIndexEntry>> GetStudiesByTypeAsync(IrbStudyType studyType)
    {
        List<IrbStudyIndexEntry> result = _state.State.Studies
            .Where(s => s.StudyType == studyType)
            .OrderBy(s => s.Title)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<IrbStudyIndexEntry>> GetStudiesByPIAsync(string principalInvestigator)
    {
        List<IrbStudyIndexEntry> result = _state.State.Studies
            .Where(s => s.PrincipalInvestigator.Contains(principalInvestigator, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Title)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<IrbStudyIndexEntry>> GetStudiesExpiringAsync(int withinDays)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(withinDays);
        List<IrbStudyIndexEntry> result = _state.State.Studies
            .Where(s => s.Status == IrbStudyStatus.OpenForEnrollment
                && s.CurrentExpirationDate.HasValue
                && s.CurrentExpirationDate.Value <= cutoff)
            .OrderBy(s => s.CurrentExpirationDate)
            .ToList();
        return Task.FromResult(result);
    }
}
