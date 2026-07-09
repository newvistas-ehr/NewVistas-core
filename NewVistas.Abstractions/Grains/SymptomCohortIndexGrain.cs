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
/// Reverse-index shard for one coded symptom (grain key <c>SYMPTOM-COHORT:{code}</c>). Tracks the
/// Present and Assessed patient sets; <see cref="RecordPresenceAsync"/> keeps Present ⊆ Assessed.
/// </summary>
public class SymptomCohortIndexGrain : Grain, ISymptomCohortIndexGrain
{
    private readonly IPersistentState<SymptomCohortState> _state;

    public SymptomCohortIndexGrain(
        [PersistentState("symptomCohortState", "symptomCohortStore")]
        IPersistentState<SymptomCohortState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Code))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.Code = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordPresenceAsync(string patientId, bool present)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            return;

        bool changed = _state.State.Assessed.Add(patientId);
        changed |= present ? _state.State.Present.Add(patientId)
                           : _state.State.Present.Remove(patientId);

        if (changed)
            await _state.WriteStateAsync();
    }

    public async Task MarkAssessedAsync(string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            return;

        if (_state.State.Assessed.Add(patientId))
            await _state.WriteStateAsync();
    }

    public Task<List<string>> GetPresentAsync() =>
        Task.FromResult(_state.State.Present.OrderBy(p => p).ToList());

    public Task<List<string>> GetAssessedAsync() =>
        Task.FromResult(_state.State.Assessed.OrderBy(p => p).ToList());

    public Task<int> GetPresentCountAsync() => Task.FromResult(_state.State.Present.Count);

    public Task<int> GetAssessedCountAsync() => Task.FromResult(_state.State.Assessed.Count);

    public Task<bool> ContainsPresentAsync(string patientId) =>
        Task.FromResult(_state.State.Present.Contains(patientId));
}
