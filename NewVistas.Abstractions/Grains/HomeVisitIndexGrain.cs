// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeVisitIndexGrain : Grain, IHomeVisitIndexGrain
{
    private readonly IPersistentState<HomeVisitIndexState> _state;

    public HomeVisitIndexGrain(
        [PersistentState("homeVisitIndexState", "homeVisitIndexStore")] IPersistentState<HomeVisitIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertVisitAsync(HomeVisitIndexEntry entry)
    {
        int idx = _state.State.Visits.FindIndex(v => v.VisitId == entry.VisitId);
        if (idx >= 0)
            _state.State.Visits[idx] = entry;
        else
            _state.State.Visits.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveVisitAsync(string visitId)
    {
        int idx = _state.State.Visits.FindIndex(v => v.VisitId == visitId);
        if (idx < 0) return;
        _state.State.Visits.RemoveAt(idx);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<HomeVisitIndexEntry>> GetVisitsByEpisodeAsync(string episodeId) =>
        Task.FromResult(_state.State.Visits
            .Where(v => v.EpisodeId == episodeId)
            .OrderByDescending(v => v.ScheduledDateTime)
            .ToList());

    public Task<List<HomeVisitIndexEntry>> GetVisitsByClinicianAsync(string clinicianId) =>
        Task.FromResult(_state.State.Visits
            .Where(v => v.ClinicianId == clinicianId)
            .OrderBy(v => v.ScheduledDateTime)
            .ToList());

    public Task<List<HomeVisitIndexEntry>> GetVisitsInRangeAsync(DateTime start, DateTime end) =>
        Task.FromResult(_state.State.Visits
            .Where(v => v.ScheduledDateTime >= start && v.ScheduledDateTime <= end)
            .OrderBy(v => v.ScheduledDateTime)
            .ToList());

    public Task<List<HomeVisitIndexEntry>> GetUpcomingVisitsAsync(int withinDays)
    {
        DateTime now = DateTime.UtcNow;
        DateTime until = now.AddDays(withinDays);
        return Task.FromResult(_state.State.Visits
            .Where(v => v.Status == HomeVisitStatus.Scheduled
                        && v.ScheduledDateTime >= now && v.ScheduledDateTime <= until)
            .OrderBy(v => v.ScheduledDateTime)
            .ToList());
    }
}
