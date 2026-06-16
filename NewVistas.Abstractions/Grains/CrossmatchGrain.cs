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
/// Crossmatch Grain — grain key: "BB-XM:{crossmatchId}"
/// </summary>
public class CrossmatchGrain : Grain, ICrossmatchGrain
{
    private readonly IPersistentState<CrossmatchState> _state;

    public CrossmatchGrain(
        [PersistentState("bbCrossmatchState", "bbCrossmatchStore")]
        IPersistentState<CrossmatchState> state)
    {
        _state = state;
    }

    public Task<CrossmatchState> GetCrossmatchAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        string unitId,
        CrossmatchUrgency urgency,
        string requestedByUserId,
        string requestedByUserName,
        string? patientAboType,
        string? patientRhType,
        string? unitAboType,
        string? unitRhType,
        string? notes)
    {
        _state.State.CrossmatchId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.UnitId = unitId;
        _state.State.Urgency = urgency;
        _state.State.RequestedDate = DateTime.UtcNow;
        _state.State.RequestedByUserId = requestedByUserId;
        _state.State.RequestedByUserName = requestedByUserName;
        _state.State.PatientAboType = patientAboType;
        _state.State.PatientRhType = patientRhType;
        _state.State.UnitAboType = unitAboType;
        _state.State.UnitRhType = unitRhType;
        _state.State.Result = CrossmatchResult.Pending;
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordResultAsync(
        CrossmatchResult result,
        CrossmatchMethod method,
        string technicianId,
        string technicianName,
        string? antibodyIdentification)
    {
        _state.State.Result = result;
        _state.State.CrossmatchMethod = method;
        _state.State.TechnicianId = technicianId;
        _state.State.TechnicianName = technicianName;
        _state.State.AntibodyIdentification = antibodyIdentification;
        _state.State.ResultDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task IssueUnitAsync(string issuedByUserId, string issuedByUserName, string transfusionId)
    {
        _state.State.IssuedDate = DateTime.UtcNow;
        _state.State.IssuedByUserId = issuedByUserId;
        _state.State.IssuedByUserName = issuedByUserName;
        _state.State.TransfusionId = transfusionId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string cancelledByUserId, string? reason)
    {
        _state.State.Result = CrossmatchResult.Cancelled;
        _state.State.Notes = string.IsNullOrEmpty(reason)
            ? _state.State.Notes
            : $"Cancelled by {cancelledByUserId}: {reason}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
