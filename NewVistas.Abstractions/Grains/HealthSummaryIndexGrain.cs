// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Health Summary Index Grain — per-patient list of generated health summary reports.
/// </summary>
public class HealthSummaryIndexGrain : Grain, IHealthSummaryIndexGrain
{
    private readonly IPersistentState<HealthSummaryIndexState> _state;

    public HealthSummaryIndexGrain(
        [PersistentState("healthSummaryIndexState", "healthSummaryIndexStore")]
        IPersistentState<HealthSummaryIndexState> state)
    {
        _state = state;
    }

    public Task<List<HealthSummaryIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<HealthSummaryIndexEntry>> GetByTypeAsync(string typeId)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.TypeId == typeId)
            .ToList());

    public async Task AddEntryAsync(HealthSummaryIndexEntry entry)
    {
        // Insert newest first
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }
}
