// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class DentalPatientGrain : Grain, IDentalPatientGrain
{
    private readonly IPersistentState<DentalPatientState> _state;

    public DentalPatientGrain(
        [PersistentState("dentalPatientState", "dentalPatientStore")]
        IPersistentState<DentalPatientState> state)
    {
        _state = state;
    }

    public Task<DentalPatientState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task EnsureInitializedAsync(string patientId)
    {
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId        = patientId;
        _state.State.EligibilityStatus = DentalEligibilityStatus.Unknown;
        _state.State.PeriodontalStatus = DentalPeriodontalStatus.Healthy;
        _state.State.CreatedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateEligibilityAsync(
        DentalEligibilityStatus eligibilityStatus,
        string? eligibilityBasisCode,
        string? eligibilityBasisDescription)
    {
        _state.State.EligibilityStatus          = eligibilityStatus;
        _state.State.EligibilityBasisCode        = eligibilityBasisCode;
        _state.State.EligibilityBasisDescription = eligibilityBasisDescription;
        _state.State.LastModifiedDate            = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPrimaryDentistAsync(string dentistId, string dentistName)
    {
        _state.State.PrimaryDentistId   = dentistId;
        _state.State.PrimaryDentistName = dentistName;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateClinicalStatusAsync(
        DentalPeriodontalStatus periodontalStatus,
        string? prostheticStatus,
        int? remainingTeethCount,
        bool onFluoride,
        string? clinicalNotes)
    {
        _state.State.PeriodontalStatus  = periodontalStatus;
        _state.State.ProstheticStatus   = prostheticStatus;
        _state.State.RemainingTeethCount = remainingTeethCount;
        _state.State.OnFluoride         = onFluoride;
        _state.State.ClinicalNotes      = clinicalNotes;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordVisitDatesAsync(
        DateTime? lastExamDate,
        DateTime? lastXRayDate,
        DateTime? lastCleaningDate)
    {
        if (lastExamDate.HasValue)    _state.State.LastExamDate    = lastExamDate;
        if (lastXRayDate.HasValue)    _state.State.LastXRayDate    = lastXRayDate;
        if (lastCleaningDate.HasValue) _state.State.LastCleaningDate = lastCleaningDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
