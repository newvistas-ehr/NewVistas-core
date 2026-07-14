// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>One SDOH screening event (grain key <c>SDOH:{guid}</c>).</summary>
public class SdohScreeningGrain : Grain, ISdohScreeningGrain
{
    private readonly IPersistentState<SdohScreeningState> _state;

    public SdohScreeningGrain(
        [PersistentState("sdohScreeningState", "sdohScreeningStore")]
        IPersistentState<SdohScreeningState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ScreeningId))
            _state.State.ScreeningId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordScreeningAsync(string patientId, string instrumentName, List<SdohScreeningResponse> responses, string recordedBy)
    {
        _state.State.PatientId = patientId;
        _state.State.InstrumentName = string.IsNullOrWhiteSpace(instrumentName) ? SdohScreeningCatalog.DefaultInstrument : instrumentName;
        _state.State.ScreeningDate = DateTime.UtcNow;
        _state.State.Responses = responses ?? new();
        _state.State.Findings = SdohScreeningCatalog.Evaluate(_state.State.Responses);
        _state.State.RecordedBy = recordedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordActionAsync(SdohDomain domain, SdohActionType actionType, string targetId, string byUser)
    {
        _state.State.Actions.Add(new SdohActionRecord
        {
            Domain = domain,
            ActionType = actionType,
            TargetId = targetId,
            Date = DateTime.UtcNow,
            By = byUser
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<SdohScreeningState> GetAsync() => Task.FromResult(_state.State);
}

/// <summary>Per-patient index of SDOH screenings (grain key <c>SDOH-IDX:{patientId}</c>).</summary>
public class SdohScreeningIndexGrain : Grain, ISdohScreeningIndexGrain
{
    private readonly IPersistentState<SdohScreeningIndexState> _state;

    public SdohScreeningIndexGrain(
        [PersistentState("sdohScreeningIndexState", "sdohScreeningIndexStore")]
        IPersistentState<SdohScreeningIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.PatientId = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddEntryAsync(SdohScreeningSummary summary)
    {
        if (summary is null || string.IsNullOrWhiteSpace(summary.ScreeningId))
            return;
        _state.State.Entries.RemoveAll(e => e.ScreeningId == summary.ScreeningId);
        _state.State.Entries.Add(summary);
        await _state.WriteStateAsync();
    }

    public Task<List<SdohScreeningSummary>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries.OrderByDescending(e => e.ScreeningDate).ToList());
}

/// <summary>Reverse-index shard for one SDOH domain (grain key <c>SDOH-COHORT:{domain}</c>).</summary>
public class SdohCohortIndexGrain : Grain, ISdohCohortIndexGrain
{
    private readonly IPersistentState<SdohCohortState> _state;

    public SdohCohortIndexGrain(
        [PersistentState("sdohCohortState", "sdohCohortStore")]
        IPersistentState<SdohCohortState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Domain))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.Domain = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddPatientAsync(string patientId)
    {
        if (!string.IsNullOrWhiteSpace(patientId) && _state.State.PatientIds.Add(patientId))
            await _state.WriteStateAsync();
    }

    public Task<List<string>> GetPatientsAsync() =>
        Task.FromResult(_state.State.PatientIds.OrderBy(p => p).ToList());

    public Task<bool> ContainsAsync(string patientId) =>
        Task.FromResult(_state.State.PatientIds.Contains(patientId));

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.PatientIds.Count);
}
