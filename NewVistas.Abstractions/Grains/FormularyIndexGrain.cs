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
/// Formulary drug list for a single benefit plan.
/// Grain key: "PBM-FORMULARY:{planId}"
/// </summary>
public class FormularyIndexGrain : Grain, IFormularyIndexGrain
{
    private readonly IPersistentState<FormularyIndexState> _state;

    public FormularyIndexGrain(
        [PersistentState("formularyState", "formularyStore")]
        IPersistentState<FormularyIndexState> state)
    {
        _state = state;
    }

    public Task<List<FormularyEntry>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries.OrderBy(e => e.DrugName).ToList());

    public Task<List<FormularyEntry>> GetByTierAsync(int tier) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Tier == tier)
            .OrderBy(e => e.DrugName)
            .ToList());

    public Task<List<FormularyEntry>> GetRequiringPriorAuthAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.RequiresPriorAuth)
            .OrderBy(e => e.DrugName)
            .ToList());

    public Task<List<FormularyEntry>> SearchAsync(string term) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.DrugName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.DrugName)
            .ToList());

    public Task<FormularyEntry?> GetDrugAsync(string drugId) =>
        Task.FromResult(_state.State.Entries.FirstOrDefault(e => e.DrugId == drugId));

    public async Task AddOrUpdateEntryAsync(FormularyEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.DrugId == entry.DrugId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string drugId)
    {
        _state.State.Entries.RemoveAll(e => e.DrugId == drugId);
        await _state.WriteStateAsync();
    }

    public async Task ClearAsync()
    {
        _state.State.Entries.Clear();
        await _state.WriteStateAsync();
    }
}
