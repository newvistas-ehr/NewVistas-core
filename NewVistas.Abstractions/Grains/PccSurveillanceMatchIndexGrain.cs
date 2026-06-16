// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PccSurveillanceMatchIndexGrain : Grain, IPccSurveillanceMatchIndexGrain
{
    private readonly IPersistentState<PccSurveillanceMatchIndexState> _state;

    public PccSurveillanceMatchIndexGrain(
        [PersistentState("pccSurvMatchIndexState", "pccSurvMatchIndexStore")]
        IPersistentState<PccSurveillanceMatchIndexState> state)
    {
        _state = state;
    }

    public Task<List<PccSurveillanceMatchIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PccSurveillanceMatchIndexEntry>> GetByStatusAsync(PccSurveillanceMatchStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public Task<List<PccSurveillanceMatchIndexEntry>> GetByConditionAsync(string conditionName)
        => Task.FromResult(_state.State.Entries
            .Where(e => string.Equals(e.ConditionName, conditionName, StringComparison.OrdinalIgnoreCase)).ToList());

    public async Task AddEntryAsync(PccSurveillanceMatchIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string matchId, PccSurveillanceMatchStatus status)
    {
        PccSurveillanceMatchIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.MatchId == matchId);
        if (entry != null)
            entry.Status = status;
        await _state.WriteStateAsync();
    }
}
