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
/// A single drug safety advisory. Owns the advisory content and lifecycle, keeps
/// the shared index in sync, and orchestrates dispatch to patients (recording a
/// verbatim receipt on each patient's record). Keyed by advisory id.
/// </summary>
public class DrugSafetyAdvisoryGrain : Grain, IDrugSafetyAdvisoryGrain
{
    private const string IndexKey = "DSA-INDEX";

    private readonly IPersistentState<DrugSafetyAdvisoryState> _state;

    public DrugSafetyAdvisoryGrain(
        [PersistentState("drugSafetyAdvisoryState", "drugSafetyAdvisoryStore")]
        IPersistentState<DrugSafetyAdvisoryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AdvisoryId))
            _state.State.AdvisoryId = this.GetPrimaryKeyString();

        return base.OnActivateAsync(cancellationToken);
    }

    private IDrugSafetyAdvisoryIndexGrain Index() =>
        GrainFactory.GetGrain<IDrugSafetyAdvisoryIndexGrain>(IndexKey);

    private IPatientSafetyAdvisoryGrain PatientLog(string patientId) =>
        GrainFactory.GetGrain<IPatientSafetyAdvisoryGrain>(patientId);

    public Task<DrugSafetyAdvisoryState> GetAsync() =>
        Task.FromResult(_state.State);

    public async Task SaveAsync(DrugSafetyAdvisoryState advisory)
    {
        advisory.AdvisoryId = this.GetPrimaryKeyString();
        advisory.LastModifiedDate = DateTime.UtcNow;

        // Preserve create metadata and any dispatch progress already recorded.
        advisory.CreatedDate = _state.State.CreatedDate == default
            ? DateTime.UtcNow
            : _state.State.CreatedDate;
        advisory.CreatedBy = string.IsNullOrEmpty(_state.State.CreatedBy)
            ? advisory.CreatedBy
            : _state.State.CreatedBy;
        advisory.DispatchedPatientIds = _state.State.DispatchedPatientIds;
        advisory.TotalDispatched = _state.State.TotalDispatched;
        advisory.LastDispatchedDate = _state.State.LastDispatchedDate;

        _state.State = advisory;
        await _state.WriteStateAsync();
        await SyncIndexAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.Status = AdvisoryStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await SyncIndexAsync();
    }

    public async Task RetireAsync()
    {
        _state.State.Status = AdvisoryStatus.Retired;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await SyncIndexAsync();
    }

    public async Task<AdvisoryDispatchResult> DispatchAsync(
        string finalMessage,
        List<string> patientIds,
        string providerId,
        string providerName,
        AdvisoryChannel channel)
    {
        if (_state.State.Status != AdvisoryStatus.Active)
            throw new InvalidOperationException(
                $"Advisory '{_state.State.AdvisoryId}' is {_state.State.Status}; only Active advisories can be dispatched.");

        AdvisoryDispatchResult result = new();
        HashSet<string> alreadyReached = new(_state.State.DispatchedPatientIds);

        foreach (string patientId in patientIds.Distinct())
        {
            if (string.IsNullOrWhiteSpace(patientId))
                continue;

            // Skip patients already reached by an earlier dispatch — never double-warn.
            if (alreadyReached.Contains(patientId))
            {
                result.SkippedAlreadySent.Add(patientId);
                continue;
            }

            await PatientLog(patientId).RecordReceiptAsync(
                _state.State.AdvisoryId, _state.State.Title, finalMessage,
                providerId, providerName, channel);

            alreadyReached.Add(patientId);
            _state.State.DispatchedPatientIds.Add(patientId);
            result.SentCount++;
        }

        if (result.SentCount > 0)
        {
            _state.State.TotalDispatched = _state.State.DispatchedPatientIds.Count;
            _state.State.LastDispatchedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
            await SyncIndexAsync();
        }

        return result;
    }

    public Task<bool> HasReachedAsync(string patientId) =>
        Task.FromResult(_state.State.DispatchedPatientIds.Contains(patientId));

    private Task SyncIndexAsync() =>
        Index().UpsertAsync(new DrugSafetyAdvisorySummary
        {
            AdvisoryId = _state.State.AdvisoryId,
            Title = _state.State.Title,
            Severity = _state.State.Severity,
            Status = _state.State.Status,
            ActionType = _state.State.ActionType,
            TargetDrugClassCodes = new List<string>(_state.State.TargetDrugClassCodes),
            SourcePublishedDate = _state.State.SourcePublishedDate,
            TotalDispatched = _state.State.TotalDispatched,
        });
}
