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
/// Grain for a completed/in-progress encounter form instance.
/// Keyed by "EF-INST:{guid}".
/// </summary>
public class EncounterFormInstanceGrain : Grain, IEncounterFormInstanceGrain
{
    private readonly IPersistentState<EncounterFormInstanceState> _state;

    public EncounterFormInstanceGrain(
        [PersistentState("encounterFormInstanceState", "encounterFormInstanceStore")]
        IPersistentState<EncounterFormInstanceState> state)
    {
        _state = state;
    }

    public Task<EncounterFormInstanceState> GetInstanceAsync() => Task.FromResult(_state.State);

    public async Task<EncounterFormInstanceState> CreateInstanceAsync(
        string templateId, string templateName,
        string patientId, string patientName,
        string? encounterId,
        string createdByProviderId, string createdByProviderName)
    {
        _state.State.InstanceId = this.GetPrimaryKeyString();
        _state.State.TemplateId = templateId;
        _state.State.TemplateName = templateName;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.EncounterId = encounterId;
        _state.State.Status = "DRAFT";
        _state.State.CreatedByProviderId = createdByProviderId;
        _state.State.CreatedByProviderName = createdByProviderName;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateInstanceIndexAsync();
        return _state.State;
    }

    public async Task SetFieldValueAsync(string fieldId, string? value)
    {
        if (_state.State.Status is "VOIDED")
            throw new InvalidOperationException("Cannot modify a voided form.");

        _state.State.FieldValues[fieldId] = value;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetFieldValuesAsync(Dictionary<string, string?> fieldValues)
    {
        if (_state.State.Status is "VOIDED")
            throw new InvalidOperationException("Cannot modify a voided form.");

        foreach (var kvp in fieldValues)
            _state.State.FieldValues[kvp.Key] = kvp.Value;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SubmitAsync(string submittedByName)
    {
        if (_state.State.Status is "VOIDED")
            throw new InvalidOperationException("Cannot submit a voided form.");

        _state.State.Status = "SUBMITTED";
        _state.State.SubmittedDate = DateTime.UtcNow;
        _state.State.SubmittedByName = submittedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateInstanceIndexAsync();
    }

    public async Task AmendAsync(string amendedByName, string reason)
    {
        if (_state.State.Status != "SUBMITTED")
            throw new InvalidOperationException("Only submitted forms can be amended.");

        _state.State.Status = "AMENDED";
        _state.State.AmendReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateInstanceIndexAsync();
    }

    public async Task VoidAsync(string voidedByName, string reason)
    {
        _state.State.Status = "VOIDED";
        _state.State.AmendReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateInstanceIndexAsync();
    }

    private async Task UpdateInstanceIndexAsync()
    {
        IEncounterFormInstanceIndexGrain index =
            GrainFactory.GetGrain<IEncounterFormInstanceIndexGrain>("EF-INST-IDX");

        await index.AddOrUpdateAsync(new EncounterFormInstanceIndexEntry
        {
            InstanceId = _state.State.InstanceId,
            TemplateId = _state.State.TemplateId,
            TemplateName = _state.State.TemplateName,
            PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName,
            Status = _state.State.Status,
            CreatedByProviderName = _state.State.CreatedByProviderName,
            CreatedDate = _state.State.CreatedDate,
            SubmittedDate = _state.State.SubmittedDate
        });
    }
}
