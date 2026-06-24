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
/// Reverse-index shard for one VA drug class. Grain key: the upper-cased class code.
/// Holds the set of patients currently on a medication in this class.
/// </summary>
public class DrugClassCohortIndexGrain : Grain, IDrugClassCohortIndexGrain
{
    private readonly IPersistentState<DrugClassCohortState> _state;

    public DrugClassCohortIndexGrain(
        [PersistentState("drugClassCohortState", "drugClassCohortStore")]
        IPersistentState<DrugClassCohortState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ClassCode))
            _state.State.ClassCode = this.GetPrimaryKeyString();

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddPatientAsync(string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            return;

        if (_state.State.PatientIds.Add(patientId))
            await _state.WriteStateAsync();
    }

    public async Task RemovePatientAsync(string patientId)
    {
        if (_state.State.PatientIds.Remove(patientId))
            await _state.WriteStateAsync();
    }

    public Task<List<string>> GetPatientsAsync() =>
        Task.FromResult(_state.State.PatientIds.OrderBy(p => p).ToList());

    public Task<bool> ContainsAsync(string patientId) =>
        Task.FromResult(_state.State.PatientIds.Contains(patientId));

    public Task<int> GetCountAsync() =>
        Task.FromResult(_state.State.PatientIds.Count);
}
