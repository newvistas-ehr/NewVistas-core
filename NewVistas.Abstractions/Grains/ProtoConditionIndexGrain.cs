// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>Directory of all proto-conditions (grain key <c>PROTOCONDITION-INDEX</c>).</summary>
public class ProtoConditionIndexGrain : Grain, IProtoConditionIndexGrain
{
    private readonly IPersistentState<ProtoConditionIndexState> _state;

    public ProtoConditionIndexGrain(
        [PersistentState("protoConditionIndexState", "protoConditionIndexStore")]
        IPersistentState<ProtoConditionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ProtoConditionSummary summary)
    {
        if (summary is null || string.IsNullOrWhiteSpace(summary.ProtoConditionId))
            return;

        _state.State.Entries.RemoveAll(e => e.ProtoConditionId == summary.ProtoConditionId);
        _state.State.Entries.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string protoConditionId)
    {
        if (_state.State.Entries.RemoveAll(e => e.ProtoConditionId == protoConditionId) > 0)
            await _state.WriteStateAsync();
    }

    public Task<List<ProtoConditionSummary>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries.OrderByDescending(e => e.LastModifiedDate).ToList());

    public Task<List<ProtoConditionSummary>> GetActiveAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == ProtoConditionStatus.Active)
            .OrderByDescending(e => e.LastModifiedDate).ToList());
}

/// <summary>Confirmed-cohort shard for one proto-condition (grain key <c>PROTO-COHORT:{id}</c>).</summary>
public class ProtoCohortIndexGrain : Grain, IProtoCohortIndexGrain
{
    private readonly IPersistentState<ProtoCohortState> _state;

    public ProtoCohortIndexGrain(
        [PersistentState("protoCohortState", "protoCohortStore")]
        IPersistentState<ProtoCohortState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProtoConditionId))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.ProtoConditionId = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddAsync(string patientId)
    {
        if (!string.IsNullOrWhiteSpace(patientId) && _state.State.PatientIds.Add(patientId))
            await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string patientId)
    {
        if (_state.State.PatientIds.Remove(patientId))
            await _state.WriteStateAsync();
    }

    public Task<List<string>> GetPatientsAsync() =>
        Task.FromResult(_state.State.PatientIds.OrderBy(p => p).ToList());

    public Task<bool> ContainsAsync(string patientId) =>
        Task.FromResult(_state.State.PatientIds.Contains(patientId));

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.PatientIds.Count);
}
