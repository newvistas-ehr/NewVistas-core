// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class CSDispenseLogState
{
    [Id(0)] public List<CSDispenseSummaryEntry> Records { get; set; } = new();
}

public class CSDispenseLogGrain : Grain, ICSDispenseLogGrain
{
    private readonly IPersistentState<CSDispenseLogState> _state;

    public CSDispenseLogGrain(
        [PersistentState("csDispenseLogState", "csDispenseLogStore")] IPersistentState<CSDispenseLogState> state)
    {
        _state = state;
    }

    public Task<List<CSDispenseSummaryEntry>> GetAllRecordsAsync() =>
        Task.FromResult(_state.State.Records
            .OrderByDescending(r => r.DispenseDateTime)
            .ToList());

    public Task<List<CSDispenseSummaryEntry>> GetRecordsByDrugAsync(string drugId) =>
        Task.FromResult(_state.State.Records
            .Where(r => r.DrugId == drugId)
            .OrderByDescending(r => r.DispenseDateTime)
            .ToList());

    public Task<List<CSDispenseSummaryEntry>> GetRecordsByDateRangeAsync(DateTime from, DateTime to) =>
        Task.FromResult(_state.State.Records
            .Where(r => r.DispenseDateTime >= from && r.DispenseDateTime <= to)
            .OrderByDescending(r => r.DispenseDateTime)
            .ToList());

    public Task<List<CSDispenseSummaryEntry>> GetRecordsByScheduleAsync(DEADrugSchedule schedule) =>
        Task.FromResult(_state.State.Records
            .Where(r => r.DrugSchedule == schedule)
            .OrderByDescending(r => r.DispenseDateTime)
            .ToList());

    public async Task UpsertRecordAsync(CSDispenseSummaryEntry entry)
    {
        int idx = _state.State.Records.FindIndex(r => r.RecordId == entry.RecordId);
        if (idx >= 0)
            _state.State.Records[idx] = entry;
        else
            _state.State.Records.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveRecordAsync(string recordId)
    {
        int idx = _state.State.Records.FindIndex(r => r.RecordId == recordId);
        if (idx >= 0)
        {
            _state.State.Records.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
