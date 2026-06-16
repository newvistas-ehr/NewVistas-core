// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EdiTransmissionIndexGrain : Grain, IEdiTransmissionIndexGrain
{
    private readonly IPersistentState<EdiTransmissionIndexState> _state;

    public EdiTransmissionIndexGrain(
        [PersistentState("ediTransmissionIndexState", "ediTransmissionIndexStore")]
        IPersistentState<EdiTransmissionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EdiTransmissionIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.TransmissionId == entry.TransmissionId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<EdiTransmissionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<EdiTransmissionIndexEntry>> GetOpenAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == "Open").ToList());
}
