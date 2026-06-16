// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class GpraReportIndexGrain : Grain, IGpraReportIndexGrain
{
    private readonly IPersistentState<GpraReportIndexState> _state;

    public GpraReportIndexGrain(
        [PersistentState("gpraReportIndexState", "gpraReportIndexStore")]
        IPersistentState<GpraReportIndexState> state)
    {
        _state = state;
    }

    public Task<List<GpraReportIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<GpraReportIndexEntry>> GetByFiscalYearAsync(int fiscalYear)
        => Task.FromResult(_state.State.Entries.Where(e => e.FiscalYear == fiscalYear).ToList());

    public async Task AddEntryAsync(GpraReportIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string reportId, GpraReportStatus status)
    {
        GpraReportIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.ReportId == reportId);
        if (entry != null)
            entry.Status = status;
        await _state.WriteStateAsync();
    }
}
