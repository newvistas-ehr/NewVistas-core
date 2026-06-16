// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ChartRequestIndexState
{
    [Id(0)] public List<ChartRequestIndexEntry> Requests { get; set; } = new();
}

public class ChartRequestIndexGrain : Grain, IChartRequestIndexGrain
{
    private readonly IPersistentState<ChartRequestIndexState> _state;

    public ChartRequestIndexGrain(
        [PersistentState("rtRequestIndexState", "rtRequestIndexStore")] IPersistentState<ChartRequestIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertRequestAsync(ChartRequestIndexEntry entry)
    {
        ChartRequestIndexEntry? existing = _state.State.Requests.Find(r => r.RequestId == entry.RequestId);
        if (existing is not null)
            _state.State.Requests.Remove(existing);
        _state.State.Requests.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ChartRequestIndexEntry>> GetAllRequestsAsync()
    {
        List<ChartRequestIndexEntry> result = _state.State.Requests
            .OrderByDescending(r => r.RequestDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartRequestIndexEntry>> GetPendingRequestsAsync()
    {
        List<ChartRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.Status is ChartRequestStatus.Pending or ChartRequestStatus.Pulled or ChartRequestStatus.InTransit)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.NeededBy)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartRequestIndexEntry>> GetUrgentRequestsAsync()
    {
        List<ChartRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.Priority is ChartRequestPriority.Urgent or ChartRequestPriority.STAT
                && r.Status is ChartRequestStatus.Pending or ChartRequestStatus.Pulled or ChartRequestStatus.InTransit)
            .OrderBy(r => r.NeededBy)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartRequestIndexEntry>> GetRequestsByPatientAsync(string patientId, int maxResults = 50)
    {
        List<ChartRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.RequestDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(result);
    }
}
