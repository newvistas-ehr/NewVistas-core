// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PccSurveillanceConfigIndexGrain : Grain, IPccSurveillanceConfigIndexGrain
{
    private readonly IPersistentState<PccSurveillanceConfigIndexState> _state;

    public PccSurveillanceConfigIndexGrain(
        [PersistentState("pccSurvConfigIndexState", "pccSurvConfigIndexStore")]
        IPersistentState<PccSurveillanceConfigIndexState> state)
    {
        _state = state;
    }

    public Task<List<PccSurveillanceConfigIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PccSurveillanceConfigIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.IsActive).ToList());

    public async Task UpsertAsync(PccSurveillanceConfigIndexEntry entry)
    {
        PccSurveillanceConfigIndexEntry? existing = _state.State.Entries
            .FirstOrDefault(e => e.ConfigId == entry.ConfigId);
        if (existing != null)
        {
            existing.ConditionName = entry.ConditionName;
            existing.Classification = entry.Classification;
            existing.CriteriaCount = entry.CriteriaCount;
            existing.IsActive = entry.IsActive;
        }
        else
        {
            _state.State.Entries.Add(entry);
        }
        await _state.WriteStateAsync();
    }
}
