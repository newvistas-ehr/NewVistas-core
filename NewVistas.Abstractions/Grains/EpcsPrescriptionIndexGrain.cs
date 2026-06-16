// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EpcsPrescriptionIndexGrain : Grain, IEpcsPrescriptionIndexGrain
{
    private readonly IPersistentState<EpcsPrescriptionIndexState> _state;

    public EpcsPrescriptionIndexGrain(
        [PersistentState("epcsRxIndexState", "epcsRxIndexStore")]
        IPersistentState<EpcsPrescriptionIndexState> state)
    {
        _state = state;
    }

    public Task<List<EpcsPrescriptionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<EpcsPrescriptionIndexEntry>> GetByStatusAsync(EpcsTransmissionStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public async Task AddEntryAsync(EpcsPrescriptionIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryAsync(string epcsId, EpcsTransmissionStatus status, bool isSigned)
    {
        EpcsPrescriptionIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.EpcsId == epcsId);
        if (entry != null)
        {
            entry.Status = status;
            entry.IsSigned = isSigned;
        }
        await _state.WriteStateAsync();
    }
}
