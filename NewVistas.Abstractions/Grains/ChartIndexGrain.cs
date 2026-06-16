// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ChartIndexState
{
    [Id(0)] public List<ChartIndexEntry> Charts { get; set; } = new();
}

public class ChartIndexGrain : Grain, IChartIndexGrain
{
    private readonly IPersistentState<ChartIndexState> _state;

    public ChartIndexGrain(
        [PersistentState("rtChartIndexState", "rtChartIndexStore")] IPersistentState<ChartIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertChartAsync(ChartIndexEntry entry)
    {
        ChartIndexEntry? existing = _state.State.Charts.Find(c => c.PatientId == entry.PatientId);
        if (existing is not null)
            _state.State.Charts.Remove(existing);
        _state.State.Charts.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ChartIndexEntry>> GetAllChartsAsync()
    {
        List<ChartIndexEntry> result = _state.State.Charts
            .OrderBy(c => c.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartIndexEntry>> GetCheckedOutChartsAsync()
    {
        List<ChartIndexEntry> result = _state.State.Charts
            .Where(c => c.IsCheckedOut)
            .OrderBy(c => c.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartIndexEntry>> GetChartsOnRequestAsync()
    {
        List<ChartIndexEntry> result = _state.State.Charts
            .Where(c => c.IsOnRequest)
            .OrderBy(c => c.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartIndexEntry>> GetLostChartsAsync()
    {
        List<ChartIndexEntry> result = _state.State.Charts
            .Where(c => c.IsLost)
            .OrderBy(c => c.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ChartIndexEntry>> GetOverdueChartsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<ChartIndexEntry> result = _state.State.Charts
            .Where(c => c.IsCheckedOut && c.ExpectedReturnDate.HasValue && c.ExpectedReturnDate.Value < now)
            .OrderBy(c => c.ExpectedReturnDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<ChartIndexEntry?> GetChartByPatientAsync(string patientId)
    {
        ChartIndexEntry? entry = _state.State.Charts.Find(c => c.PatientId == patientId);
        return Task.FromResult(entry);
    }
}
