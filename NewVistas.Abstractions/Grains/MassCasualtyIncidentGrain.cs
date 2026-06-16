// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MassCasualtyIncidentGrain : Grain, IMassCasualtyIncidentGrain
{
    private readonly IPersistentState<MassCasualtyIncidentState> _state;

    public MassCasualtyIncidentGrain(
        [PersistentState("mciState", "mciIncidentStore")]
        IPersistentState<MassCasualtyIncidentState> state) { _state = state; }

    public Task<MassCasualtyIncidentState> GetIncidentAsync() => Task.FromResult(_state.State);

    public async Task<MassCasualtyIncidentState> ActivateAsync(
        string incidentName, string incidentType, string severity,
        string activatedByName, string? description, int? estimatedCasualties)
    {
        _state.State.IncidentId = this.GetPrimaryKeyString();
        _state.State.IncidentName = incidentName;
        _state.State.IncidentType = incidentType;
        _state.State.Severity = severity;
        _state.State.Status = "ACTIVE";
        _state.State.Description = description;
        _state.State.EstimatedCasualties = estimatedCasualties ?? 0;
        _state.State.RegisteredCasualtyCount = 0;
        _state.State.ActivatedByName = activatedByName;
        _state.State.ActivatedDate = DateTime.UtcNow;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.StatusUpdates.Add(new MciStatusUpdate
        {
            Timestamp = DateTime.UtcNow,
            Message = $"MCI ACTIVATED: {incidentName} ({incidentType}, {severity})",
            AuthorName = activatedByName
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
        return _state.State;
    }

    public async Task UpdateSeverityAsync(string severity, string updatedByName)
    {
        string old = _state.State.Severity;
        _state.State.Severity = severity;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        _state.State.StatusUpdates.Add(new MciStatusUpdate
        {
            Timestamp = DateTime.UtcNow,
            Message = $"Severity changed from {old} to {severity}",
            AuthorName = updatedByName
        });
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task UpdateEstimatedCasualtiesAsync(int estimatedCasualties, string updatedByName)
    {
        _state.State.EstimatedCasualties = estimatedCasualties;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        _state.State.StatusUpdates.Add(new MciStatusUpdate
        {
            Timestamp = DateTime.UtcNow,
            Message = $"Estimated casualties updated to {estimatedCasualties}",
            AuthorName = updatedByName
        });
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task AddStatusUpdateAsync(string message, string authorName)
    {
        _state.State.StatusUpdates.Add(new MciStatusUpdate
        {
            Timestamp = DateTime.UtcNow, Message = message, AuthorName = authorName
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync(string deactivatedByName, string? afterActionNotes)
    {
        _state.State.Status = "DEACTIVATED";
        _state.State.DeactivatedDate = DateTime.UtcNow;
        _state.State.DeactivatedByName = deactivatedByName;
        _state.State.AfterActionNotes = afterActionNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        _state.State.StatusUpdates.Add(new MciStatusUpdate
        {
            Timestamp = DateTime.UtcNow,
            Message = "MCI DEACTIVATED",
            AuthorName = deactivatedByName
        });
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    private async Task UpdateIndexAsync()
    {
        var index = GrainFactory.GetGrain<IMassCasualtyIncidentIndexGrain>("MCI-IDX");
        await index.AddOrUpdateAsync(new MassCasualtyIncidentIndexEntry
        {
            IncidentId = _state.State.IncidentId, IncidentName = _state.State.IncidentName,
            IncidentType = _state.State.IncidentType, Severity = _state.State.Severity,
            Status = _state.State.Status, EstimatedCasualties = _state.State.EstimatedCasualties,
            RegisteredCasualtyCount = _state.State.RegisteredCasualtyCount,
            ActivatedDate = _state.State.ActivatedDate
        });
    }
}
