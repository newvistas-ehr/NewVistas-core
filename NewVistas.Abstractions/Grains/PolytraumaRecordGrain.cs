// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PolytraumaRecordGrain : Grain, IPolytraumaRecordGrain
{
    private readonly IPersistentState<PolytraumaRecordState> _state;

    public PolytraumaRecordGrain(
        [PersistentState("ptRecordState", "ptRecordStore")] IPersistentState<PolytraumaRecordState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            _state.State.PatientId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PolytraumaRecordState> GetRecordAsync() => Task.FromResult(_state.State);

    public async Task RegisterPatientAsync(
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        TraumaMechanism traumaMechanism,
        DateTime? traumaDate,
        string traumaLocation,
        string polytraumaNetworkSite,
        string referralSource,
        string primaryTeamId,
        string primaryTeamName,
        string caseManagerId,
        string caseManagerName,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.TraumaMechanism = traumaMechanism;
        _state.State.TraumaDate = traumaDate;
        _state.State.TraumaLocation = traumaLocation;
        _state.State.PolytraumaNetworkSite = polytraumaNetworkSite;
        _state.State.ReferralSource = referralSource;
        _state.State.PrimaryPolytraumaTeamId = primaryTeamId;
        _state.State.PrimaryPolytraumaTeamName = primaryTeamName;
        _state.State.CaseManagerId = caseManagerId;
        _state.State.CaseManagerName = caseManagerName;
        _state.State.Notes = notes;
        _state.State.Status = PolytraumaStatus.Active;
        _state.State.RegistrationDate = DateTime.UtcNow;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddInjuryAsync(PolytraumaInjury injury)
    {
        injury.InjuryId = Guid.NewGuid().ToString();
        _state.State.Injuries.Add(injury);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(PolytraumaStatus status, DateTime? deactivationDate)
    {
        _state.State.Status = status;
        _state.State.DeactivationDate = deactivationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateTBIStatusAsync(bool hasTBI, TBISeverity? severity)
    {
        _state.State.HasTBI = hasTBI;
        _state.State.TBISeverity = severity;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateIssScoreAsync(int issScore)
    {
        _state.State.IssTotalScore = issScore;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
