// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class NursingShiftHandoffGrain : Grain, INursingShiftHandoffGrain
{
    private readonly IPersistentState<NursingShiftHandoffState> _state;

    public NursingShiftHandoffGrain(
        [PersistentState("nursingShiftHandoffState", "nursingShiftHandoffStore")]
        IPersistentState<NursingShiftHandoffState> state)
    { _state = state; }

    public Task<NursingShiftHandoffState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId, NursingShift shift, DateTime shiftDate,
        string outgoingNurseId, string outgoingNurseName,
        string? locationId, string? locationName, string? bedNumber,
        SbarPatientSummary sbar, HandoffClinicalSnapshot? clinicalSnapshot,
        List<string>? safetyConcerns, string? notes)
    {
        string id = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;
        _state.State.HandoffId         = id;
        _state.State.PatientId         = patientId;
        _state.State.Shift             = shift;
        _state.State.ShiftDate         = shiftDate;
        _state.State.OutgoingNurseId   = outgoingNurseId;
        _state.State.OutgoingNurseName = outgoingNurseName;
        _state.State.LocationId        = locationId;
        _state.State.LocationName      = locationName;
        _state.State.BedNumber         = bedNumber;
        _state.State.Sbar              = sbar;
        _state.State.ClinicalSnapshot  = clinicalSnapshot;
        _state.State.SafetyConcerns    = safetyConcerns ?? new();
        _state.State.Notes             = notes;
        _state.State.Status            = ShiftHandoffStatus.Draft;
        _state.State.CreatedDate       = now;
        _state.State.LastModifiedDate  = now;
        await _state.WriteStateAsync();
        return id;
    }

    public async Task CompleteAsync()
    {
        _state.State.Status           = ShiftHandoffStatus.Completed;
        _state.State.CompletedDate    = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeAsync(string incomingNurseId, string incomingNurseName)
    {
        _state.State.IncomingNurseId   = incomingNurseId;
        _state.State.IncomingNurseName = incomingNurseName;
        _state.State.Status            = ShiftHandoffStatus.Acknowledged;
        _state.State.AcknowledgedDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
