// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-ward patient census grain.
/// Grain key pattern: "WARD-CENSUS:{wardId}"
/// </summary>
public class WardCensusGrain : Grain, IWardCensusGrain
{
    private readonly IPersistentState<WardCensusState> _state;

    public WardCensusGrain(
        [PersistentState("wardCensusState", "wardCensusStore")] IPersistentState<WardCensusState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.WardId))
        {
            // Key is "WARD-CENSUS:{wardId}" — extract wardId suffix
            string key = this.GetPrimaryKeyString();
            _state.State.WardId = key.StartsWith("WARD-CENSUS:") ? key["WARD-CENSUS:".Length..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdatePatientAsync(WardCensusEntry entry)
    {
        _state.State.Patients.RemoveAll(p => p.PatientId == entry.PatientId);
        _state.State.Patients.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemovePatientAsync(string patientId)
    {
        int removed = _state.State.Patients.RemoveAll(p => p.PatientId == patientId);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<WardCensusEntry>> GetCensusAsync()
        => Task.FromResult(_state.State.Patients.OrderBy(p => p.RoomBed).ToList());

    public Task<int> GetPatientCountAsync()
        => Task.FromResult(_state.State.Patients.Count);
}
