// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class NewbornNurseryGrain : Grain, INewbornNurseryGrain
{
    private readonly IPersistentState<NewbornNurseryState> _state;

    public NewbornNurseryGrain(
        [PersistentState("newbornNurseryState", "newbornNurseryStore")] IPersistentState<NewbornNurseryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SiteId))
            _state.State.SiteId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task UpsertEntryAsync(NewbornNurseryEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.NewbornId == entry.NewbornId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string newbornId)
    {
        int idx = _state.State.Entries.FindIndex(e => e.NewbornId == newbornId);
        if (idx < 0) return;
        _state.State.Entries.RemoveAt(idx);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<NewbornNurseryEntry>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries.OrderByDescending(e => e.BirthDateTime).ToList());

    public Task<List<NewbornNurseryEntry>> GetActiveAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == NewbornStatus.Admitted)
            .OrderByDescending(e => e.BirthDateTime)
            .ToList());

    public Task<List<NewbornNurseryEntry>> GetByLevelAsync(NurseryLevelOfCare level) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == NewbornStatus.Admitted && e.NurseryLevel == level)
            .OrderByDescending(e => e.BirthDateTime)
            .ToList());

    public Task<List<NewbornNurseryEntry>> GetWithPendingScreensAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == NewbornStatus.Admitted && e.PendingScreenCount > 0)
            .OrderByDescending(e => e.BirthDateTime)
            .ToList());
}
