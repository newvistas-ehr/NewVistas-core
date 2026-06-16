// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class LabAccessionIndexGrain : Grain, ILabAccessionIndexGrain
{
    private readonly IPersistentState<LabAccessionIndexState> _state;

    public LabAccessionIndexGrain(
        [PersistentState("labAccessionIndexState", "labAccessionIndexStore")]
        IPersistentState<LabAccessionIndexState> state)
    { _state = state; }

    public Task<List<LabAccessionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(LabAccessionIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.AccessionNumber == entry.AccessionNumber);
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public Task<List<LabAccessionIndexEntry>> GetPendingAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status != SpecimenStatus.Completed && e.Status != SpecimenStatus.Rejected)
            .ToList());
}
