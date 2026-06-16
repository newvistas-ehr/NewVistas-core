// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class OutbreakIndexState
{
    [Id(0)] public List<OutbreakSummary> Outbreaks { get; set; } = new();
}

public class OutbreakIndexGrain : Grain, IOutbreakIndexGrain
{
    private readonly IPersistentState<OutbreakIndexState> _state;

    public OutbreakIndexGrain(
        [PersistentState("outbreakIndexState", "haiOutbreakIndexStore")] IPersistentState<OutbreakIndexState> state)
    {
        _state = state;
    }

    public Task<List<OutbreakSummary>> GetAllOutbreaksAsync() =>
        Task.FromResult(_state.State.Outbreaks
            .OrderByDescending(o => o.StartDate)
            .ToList());

    public Task<List<OutbreakSummary>> GetActiveAsync() =>
        Task.FromResult(_state.State.Outbreaks
            .Where(o => o.Status == OutbreakStatus.Active)
            .OrderByDescending(o => o.StartDate)
            .ToList());

    public async Task UpsertOutbreakAsync(OutbreakSummary summary)
    {
        int idx = _state.State.Outbreaks.FindIndex(o => o.OutbreakId == summary.OutbreakId);
        if (idx >= 0)
            _state.State.Outbreaks[idx] = summary;
        else
            _state.State.Outbreaks.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveOutbreakAsync(string outbreakId)
    {
        int idx = _state.State.Outbreaks.FindIndex(o => o.OutbreakId == outbreakId);
        if (idx >= 0)
        {
            _state.State.Outbreaks.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
