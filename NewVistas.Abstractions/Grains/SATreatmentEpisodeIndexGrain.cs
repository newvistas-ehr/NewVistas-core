// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class SATreatmentEpisodeIndexGrain : Grain, ISATreatmentEpisodeIndexGrain
{
    private readonly IPersistentState<SATreatmentEpisodeIndexState> _state;

    public SATreatmentEpisodeIndexGrain(
        [PersistentState("saEpisodeIndexState", "saEpisodeIndexStore")]
        IPersistentState<SATreatmentEpisodeIndexState> state)
    {
        _state = state;
    }

    public Task<List<SATreatmentEpisodeIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<SATreatmentEpisodeIndexEntry>> GetByStatusAsync(SATreatmentStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public Task<SATreatmentEpisodeIndexEntry?> GetActiveAsync()
        => Task.FromResult(_state.State.Entries.FirstOrDefault(
            e => e.Status == SATreatmentStatus.Active || e.Status == SATreatmentStatus.Reopened));

    public async Task AddEntryAsync(SATreatmentEpisodeIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryAsync(string episodeId, SATreatmentStatus status, DateTime? dischargeDate)
    {
        SATreatmentEpisodeIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.EpisodeId == episodeId);
        if (entry != null)
        {
            entry.Status = status;
            entry.DischargeDate = dischargeDate;
        }
        await _state.WriteStateAsync();
    }
}
