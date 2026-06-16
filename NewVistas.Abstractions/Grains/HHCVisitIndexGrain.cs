// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class HHCVisitIndexState
{
    [Id(0)] public List<HHCVisitIndexEntry> Visits { get; set; } = new();
}

public class HHCVisitIndexGrain : Grain, IHHCVisitIndexGrain
{
    private readonly IPersistentState<HHCVisitIndexState> _state;

    public HHCVisitIndexGrain(
        [PersistentState("hhcVisitIndexState", "hhcVisitIndexStore")] IPersistentState<HHCVisitIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertVisitAsync(HHCVisitIndexEntry entry)
    {
        HHCVisitIndexEntry? existing = _state.State.Visits.Find(v => v.VisitId == entry.VisitId);
        if (existing is not null)
            _state.State.Visits.Remove(existing);
        _state.State.Visits.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<HHCVisitIndexEntry>> GetAllVisitsAsync()
    {
        List<HHCVisitIndexEntry> result = _state.State.Visits
            .OrderByDescending(v => v.VisitDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HHCVisitIndexEntry>> GetVisitsByDisciplineAsync(HHCVisitDiscipline discipline)
    {
        List<HHCVisitIndexEntry> result = _state.State.Visits
            .Where(v => v.Discipline == discipline)
            .OrderByDescending(v => v.VisitDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HHCVisitIndexEntry>> GetUpcomingVisitsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<HHCVisitIndexEntry> result = _state.State.Visits
            .Where(v => v.Status == HHCVisitStatus.Scheduled && v.VisitDate >= now)
            .OrderBy(v => v.VisitDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HHCVisitIndexEntry>> GetCompletedVisitsAsync()
    {
        List<HHCVisitIndexEntry> result = _state.State.Visits
            .Where(v => v.Status == HHCVisitStatus.Completed)
            .OrderByDescending(v => v.VisitDate)
            .ToList();
        return Task.FromResult(result);
    }
}
