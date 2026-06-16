// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class NursingTriageGrain : Grain, INursingTriageGrain
{
    private readonly IPersistentState<NursingTriageState> _state;

    public NursingTriageGrain(
        [PersistentState("nursingTriageState", "nursingTriageStore")]
        IPersistentState<NursingTriageState> state)
    {
        _state = state;
    }

    public Task<NursingTriageState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(NursingTriageState initialState)
    {
        string triageId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;

        _state.State = initialState;
        _state.State.TriageId        = triageId;
        _state.State.Status          = NursingAssessmentStatus.Draft;
        _state.State.CreatedDate     = now;
        _state.State.LastModifiedDate = now;

        await _state.WriteStateAsync();
        return triageId;
    }

    public async Task AssignTriageLevelAsync(TriageLevel level, int? expectedResources)
    {
        _state.State.TriageLevel       = level;
        _state.State.ExpectedResources = expectedResources;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SignAsync(string nurseId, string nurseName)
    {
        _state.State.Status           = NursingAssessmentStatus.Signed;
        _state.State.SignedDate       = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetDispositionAsync(TriageDisposition disposition)
    {
        _state.State.Disposition      = disposition;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateVitalsAsync(
        decimal? temperature, int? heartRate, int? respiratoryRate,
        int? systolicBP, int? diastolicBP, decimal? spO2, int? painScore)
    {
        if (temperature.HasValue) _state.State.Temperature = temperature;
        if (heartRate.HasValue) _state.State.HeartRate = heartRate;
        if (respiratoryRate.HasValue) _state.State.RespiratoryRate = respiratoryRate;
        if (systolicBP.HasValue) _state.State.SystolicBP = systolicBP;
        if (diastolicBP.HasValue) _state.State.DiastolicBP = diastolicBP;
        if (spO2.HasValue) _state.State.SpO2 = spO2;
        if (painScore.HasValue) _state.State.PainScore = painScore;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
