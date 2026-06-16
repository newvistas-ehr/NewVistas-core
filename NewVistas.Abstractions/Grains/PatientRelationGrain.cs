// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PatientRelationGrain : Grain, IPatientRelationGrain
{
    private readonly IPersistentState<PatientRelationState> _state;

    public PatientRelationGrain(
        [PersistentState("patientRelationState", "patientRelationStore")]
        IPersistentState<PatientRelationState> state)
    {
        _state = state;
    }

    public Task<PatientRelationState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task<string> AddOrUpdateRelationAsync(PatientRelation relation)
    {
        string relationId = string.IsNullOrEmpty(relation.RelationId)
            ? Guid.NewGuid().ToString()
            : relation.RelationId;

        PatientRelation entry = relation with { RelationId = relationId };

        int idx = _state.State.Relations.FindIndex(r => r.RelationId == relationId);
        if (idx >= 0)
            _state.State.Relations[idx] = entry;
        else
            _state.State.Relations.Add(entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return relationId;
    }

    public async Task RemoveRelationAsync(string relationId)
    {
        int idx = _state.State.Relations.FindIndex(r => r.RelationId == relationId);
        if (idx >= 0)
        {
            _state.State.Relations.RemoveAt(idx);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<PatientRelation>> GetByTypeAsync(RelationshipType type)
        => Task.FromResult(_state.State.Relations
            .Where(r => r.RelationshipType == type)
            .ToList());
}
