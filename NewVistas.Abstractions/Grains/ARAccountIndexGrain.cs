// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARAccountIndexGrain : Grain, IARAccountIndexGrain
{
    private static readonly HashSet<string> ActiveStatuses = new()
    {
        nameof(ARAccountStatus.Active),
        nameof(ARAccountStatus.OnPaymentPlan),
        nameof(ARAccountStatus.InCollection),
    };

    private readonly IPersistentState<ARAccountIndexState> _state;

    public ARAccountIndexGrain(
        [PersistentState("arAccountIndexState", "arAccountIndexStore")]
        IPersistentState<ARAccountIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ARAccountIndexEntry entry)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            _state.State.PatientId = entry.PatientId;

        int idx = _state.State.Entries.FindIndex(e => e.ARAccountId == entry.ARAccountId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<ARAccountIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<ARAccountIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => ActiveStatuses.Contains(e.ARStatus))
            .ToList());

    public Task<List<ARAccountIndexEntry>> GetByStatusAsync(string status)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.ARStatus == status)
            .ToList());
}
