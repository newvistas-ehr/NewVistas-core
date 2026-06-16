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
/// Grain for encounter form template management.
/// Maps to IHS RPMS PCC encounter form templates.
/// Keyed by "EF-TPL:{guid}".
/// </summary>
public class EncounterFormTemplateGrain : Grain, IEncounterFormTemplateGrain
{
    private readonly IPersistentState<EncounterFormTemplateState> _state;

    public EncounterFormTemplateGrain(
        [PersistentState("encounterFormTemplateState", "encounterFormTemplateStore")]
        IPersistentState<EncounterFormTemplateState> state)
    {
        _state = state;
    }

    public Task<EncounterFormTemplateState> GetTemplateAsync() => Task.FromResult(_state.State);

    public async Task<EncounterFormTemplateState> CreateTemplateAsync(
        string name, string description, string formType, string? clinicId,
        List<EncounterFormFieldDefinition> fields, string createdByName)
    {
        _state.State.TemplateId = this.GetPrimaryKeyString();
        _state.State.Name = name;
        _state.State.Description = description;
        _state.State.FormType = formType;
        _state.State.ClinicId = clinicId;
        _state.State.Fields = fields;
        _state.State.Status = "DRAFT";
        _state.State.Version = 1;
        _state.State.CreatedByName = createdByName;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateTemplateIndexAsync();
        return _state.State;
    }

    public async Task UpdateTemplateAsync(
        string name, string description,
        List<EncounterFormFieldDefinition> fields, string updatedByName)
    {
        if (_state.State.Status == "RETIRED")
            throw new InvalidOperationException("Cannot update a retired template.");

        _state.State.Name = name;
        _state.State.Description = description;
        _state.State.Fields = fields;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateTemplateIndexAsync();
    }

    public async Task PublishAsync(string publishedByName)
    {
        if (_state.State.Status == "RETIRED")
            throw new InvalidOperationException("Cannot publish a retired template.");

        if (_state.State.Status == "PUBLISHED")
            _state.State.Version++;

        _state.State.Status = "PUBLISHED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateTemplateIndexAsync();
    }

    public async Task RetireAsync(string retiredByName)
    {
        _state.State.Status = "RETIRED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateTemplateIndexAsync();
    }

    public async Task AddFieldAsync(EncounterFormFieldDefinition field, string updatedByName)
    {
        if (_state.State.Status == "RETIRED")
            throw new InvalidOperationException("Cannot modify a retired template.");

        if (!_state.State.Fields.Any(f => f.FieldId == field.FieldId))
        {
            _state.State.Fields.Add(field);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
            await UpdateTemplateIndexAsync();
        }
    }

    public async Task RemoveFieldAsync(string fieldId, string updatedByName)
    {
        if (_state.State.Status == "RETIRED")
            throw new InvalidOperationException("Cannot modify a retired template.");

        int removed = _state.State.Fields.RemoveAll(f => f.FieldId == fieldId);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
            await UpdateTemplateIndexAsync();
        }
    }

    private async Task UpdateTemplateIndexAsync()
    {
        IEncounterFormTemplateIndexGrain index =
            GrainFactory.GetGrain<IEncounterFormTemplateIndexGrain>("EF-TPL-IDX");

        await index.AddOrUpdateAsync(new EncounterFormTemplateIndexEntry
        {
            TemplateId = _state.State.TemplateId,
            Name = _state.State.Name,
            FormType = _state.State.FormType,
            Status = _state.State.Status,
            ClinicId = _state.State.ClinicId,
            Version = _state.State.Version,
            FieldCount = _state.State.Fields.Count,
            CreatedDate = _state.State.CreatedDate
        });
    }
}
