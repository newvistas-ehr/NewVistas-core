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
/// Blood Bank Patient Grain — grain key: "BB-PATIENT:{patientId}"
/// </summary>
public class BloodBankPatientGrain : Grain, IBloodBankPatientGrain
{
    private readonly IPersistentState<BloodBankPatientState> _state;

    public BloodBankPatientGrain(
        [PersistentState("bbPatientState", "bbPatientStore")]
        IPersistentState<BloodBankPatientState> state)
    {
        _state = state;
    }

    public Task<BloodBankPatientState> GetAsync() => Task.FromResult(_state.State);

    public async Task InitializeAsync(string patientId)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = patientId;
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateBloodTypeAsync(
        AboBloodType aboType,
        RhBloodType rhType,
        AntibodyScreenResult antibodyScreenResult,
        DateTime? antibodyScreenDate,
        string? directAntibodyTest,
        string? specialRequirements,
        string? notes)
    {
        _state.State.AboType = aboType;
        _state.State.RhType = rhType;
        _state.State.AntibodyScreenResult = antibodyScreenResult;
        _state.State.AntibodyScreenDate = antibodyScreenDate;
        _state.State.LastTypedDate = DateTime.UtcNow;
        if (directAntibodyTest is not null) _state.State.DirectAntibodyTest = directAntibodyTest;
        if (specialRequirements is not null) _state.State.SpecialRequirements = specialRequirements;
        if (notes is not null) _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task IncrementTransfusionCountAsync()
    {
        _state.State.TransfusionCount++;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
