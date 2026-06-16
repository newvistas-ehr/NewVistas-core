// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PharmacyPosInsurerIndexGrain : Grain, IPharmacyPosInsurerIndexGrain
{
    private readonly IPersistentState<PosInsurerIndexState> _state;

    public PharmacyPosInsurerIndexGrain(
        [PersistentState("posInsurerIndexState", "posInsurerIndexStore")]
        IPersistentState<PosInsurerIndexState> state)
    {
        _state = state;
    }

    public Task<List<PosInsurerIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PosInsurerIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.IsActive).ToList());

    public async Task UpsertAsync(PosInsurerIndexEntry entry)
    {
        PosInsurerIndexEntry? existing = _state.State.Entries.FirstOrDefault(e => e.InsurerId == entry.InsurerId);
        if (existing != null)
        {
            existing.InsurerName = entry.InsurerName;
            existing.Bin = entry.Bin;
            existing.Pcn = entry.Pcn;
            existing.IsActive = entry.IsActive;
        }
        else
        {
            _state.State.Entries.Add(entry);
        }
        await _state.WriteStateAsync();
    }
}
