// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Grain representing a single PT measurement session for one body group, one visit.
/// Mirrors the VitalGrain pattern: write-once with incremental additions.
/// </summary>
public class PTSessionGrain : Grain, IPTSessionGrain
{
    private readonly IPersistentState<PTSessionState> _state;

    public PTSessionGrain(
        [PersistentState("ptSessionState", "physTherapySessionStore")]
        IPersistentState<PTSessionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SessionId))
        {
            _state.State.SessionId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PTSessionState> GetSessionAsync() => Task.FromResult(_state.State);

    public async Task RecordSessionAsync(
        string patientId,
        BodyGroup bodyGroup,
        DateTime sessionDate,
        string? therapistId,
        string? therapistName,
        string? locationId,
        string? locationName,
        Laterality side,
        List<RomMeasurement> romMeasurements,
        List<StrengthMeasurement> strengthMeasurements,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.BodyGroup = bodyGroup;
        _state.State.SessionDate = sessionDate;
        _state.State.TherapistId = therapistId;
        _state.State.TherapistName = therapistName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Side = side;
        _state.State.RomMeasurements = romMeasurements;
        _state.State.StrengthMeasurements = strengthMeasurements;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddRomMeasurementAsync(RomMeasurement measurement)
    {
        if (!_state.State.RomMeasurements.Any(r => r.Movement == measurement.Movement))
            _state.State.RomMeasurements.Add(measurement);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddStrengthMeasurementAsync(StrengthMeasurement measurement)
    {
        if (!_state.State.StrengthMeasurements.Any(s => s.Movement == measurement.Movement))
            _state.State.StrengthMeasurements.Add(measurement);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateNotesAsync(string notes)
    {
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddExerciseLogAsync(ClinicExerciseLog exercise)
    {
        _state.State.Exercises.Add(exercise);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetReferralAsync(string? referralId)
    {
        _state.State.ReferralId = referralId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
