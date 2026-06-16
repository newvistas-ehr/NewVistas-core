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
/// Index grain for all Home Telehealth alerts belonging to a single patient.
/// Grain key: "HT-ALERT-IDX:{patientId}"
/// </summary>
public class HomeTelehealthAlertIndexGrain : Grain, IHomeTelehealthAlertIndexGrain
{
    private readonly IPersistentState<HomeTelehealthAlertIndexState> _state;

    public HomeTelehealthAlertIndexGrain(
        [PersistentState("htAlertIndexState", "htAlertIndexStore")] IPersistentState<HomeTelehealthAlertIndexState> state)
    {
        _state = state;
    }

    public async Task AddAsync(HtAlertIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<List<HtAlertIndexEntry>> GetAsync(HtAlertStatus? status)
    {
        IEnumerable<HtAlertIndexEntry> query = _state.State.Entries;

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        List<HtAlertIndexEntry> results = query
            .OrderByDescending(e => e.AlertDateTime)
            .ToList();

        return Task.FromResult(results);
    }

    public async Task UpdateStatusAsync(string alertId, HtAlertStatus status)
    {
        HtAlertIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.AlertId == alertId);
        if (entry != null)
        {
            entry.Status = status;
            await _state.WriteStateAsync();
        }
    }
}
