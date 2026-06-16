// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class RadWorklistGrain : Grain, IRadWorklistGrain
{
    private readonly IPersistentState<RadWorklistState> _state;
    public RadWorklistGrain([PersistentState("radWorklistState", "radWorklistStore")] IPersistentState<RadWorklistState> state) { _state = state; }

    public Task<RadWorklistState> GetAsync() => Task.FromResult(_state.State);

    public async Task RefreshAsync(List<RadWorklistItem> items)
    {
        _state.State.Items = items.OrderBy(i => i.Urgency == "STAT" ? 0 : i.Urgency == "URGENT" ? 1 : 2)
            .ThenBy(i => i.OrderDateTime).ToList();
        _state.State.LastRefreshedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddItemAsync(RadWorklistItem item)
    {
        _state.State.Items.RemoveAll(i => i.RadiologyId == item.RadiologyId);
        _state.State.Items.Add(item);
        await _state.WriteStateAsync();
    }

    public async Task RemoveItemAsync(string radiologyId)
    {
        _state.State.Items.RemoveAll(i => i.RadiologyId == radiologyId);
        await _state.WriteStateAsync();
    }

    public Task<List<RadWorklistItem>> GetByStatusAsync(RadExamStatus status)
        => Task.FromResult(_state.State.Items.Where(i => i.ExamStatus == status).ToList());

    public Task<List<RadWorklistItem>> GetUrgentAsync()
        => Task.FromResult(_state.State.Items.Where(i => i.Urgency == "STAT" || i.Urgency == "URGENT").ToList());
}
