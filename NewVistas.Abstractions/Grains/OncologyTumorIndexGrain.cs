// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class OncologyTumorIndexGrain : Grain, IOncologyTumorIndexGrain
{
    private readonly IPersistentState<OncologyTumorIndexState> _state;

    public OncologyTumorIndexGrain(
        [PersistentState("oncTumorIndexState", "oncTumorIndexStore")] IPersistentState<OncologyTumorIndexState> state)
    {
        _state = state;
    }

    public Task<List<OncologyTumorIndexEntry>> GetAllTumorsAsync() =>
        Task.FromResult(
            _state.State.Tumors
                .OrderByDescending(t => t.DateOfDiagnosis)
                .ToList());

    public Task<List<OncologyTumorIndexEntry>> GetActiveTumorsAsync() =>
        Task.FromResult(
            _state.State.Tumors
                .Where(t => t.Status == OncologyStatus.Active || t.Status == OncologyStatus.Recurrence)
                .OrderByDescending(t => t.DateOfDiagnosis)
                .ToList());

    public async Task UpsertTumorAsync(OncologyTumorIndexEntry entry)
    {
        int idx = _state.State.Tumors.FindIndex(t => t.TumorId == entry.TumorId);
        if (idx >= 0)
            _state.State.Tumors[idx] = entry;
        else
            _state.State.Tumors.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTumorAsync(string tumorId)
    {
        int idx = _state.State.Tumors.FindIndex(t => t.TumorId == tumorId);
        if (idx < 0) return;
        _state.State.Tumors.RemoveAt(idx);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
