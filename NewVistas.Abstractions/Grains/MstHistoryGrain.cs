// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class MstHistoryGrain : Grain, IMstHistoryGrain
{
    private readonly IPersistentState<MstHistoryState> _state;

    public MstHistoryGrain(
        [PersistentState("mstHistoryState", "mstHistoryStore")]
        IPersistentState<MstHistoryState> state)
    {
        _state = state;
    }

    public Task<MstHistoryState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task RecordScreeningAsync(
        DateTime screeningDate,
        MstStatus status,
        string screenedByUserId,
        string screenedByUserName,
        string? location,
        string? notes)
    {
        string screeningId = Guid.NewGuid().ToString();

        _state.State.Screenings.Add(new MstScreeningEntry
        {
            ScreeningId       = screeningId,
            ScreeningDate     = screeningDate,
            ScreeningStatus   = status,
            ScreenedByUserId  = screenedByUserId,
            ScreenedByUserName = screenedByUserName,
            Location          = location,
            Notes             = notes,
        });

        _state.State.LastScreeningDate = screeningDate;
        _state.State.CurrentStatus     = status;

        if (status == MstStatus.Verified)
            _state.State.MstPositive = true;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCurrentStatusAsync(MstStatus status)
    {
        _state.State.CurrentStatus    = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetDisclosureAsync(string location, DateTime date)
    {
        _state.State.DisclosureLocation = location;
        _state.State.DisclosureDate     = date;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
