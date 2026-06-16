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
/// Per-patient index of PCE encounter visits.
/// Grain key: "PCE-VISITS:{patientId}"
/// </summary>
public class PatientVisitIndexGrain : Grain, IPatientVisitIndexGrain
{
    private readonly IPersistentState<PatientVisitIndexState> _state;

    public PatientVisitIndexGrain(
        [PersistentState("patientVisitIndex", "visitIndexStore")] IPersistentState<PatientVisitIndexState> state)
    {
        _state = state;
    }

    public Task<List<PceVisitEntry>> GetVisitsAsync(int maxResults)
    {
        List<PceVisitEntry> results = _state.State.Visits
            .OrderByDescending(v => v.VisitDateTime)
            .Take(maxResults > 0 ? maxResults : int.MaxValue)
            .ToList();
        return Task.FromResult(results);
    }

    public async Task AddOrUpdateVisitAsync(PceVisitEntry entry)
    {
        PceVisitEntry? existing = _state.State.Visits.FirstOrDefault(v => v.VisitId == entry.VisitId);
        if (existing is not null)
            _state.State.Visits.Remove(existing);
        _state.State.Visits.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveVisitAsync(string visitId)
    {
        PceVisitEntry? existing = _state.State.Visits.FirstOrDefault(v => v.VisitId == visitId);
        if (existing is not null)
        {
            _state.State.Visits.Remove(existing);
            await _state.WriteStateAsync();
        }
    }
}
