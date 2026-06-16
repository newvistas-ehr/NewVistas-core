// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HaiCaseGrain : Grain, IHAICaseGrain
{
    private readonly IPersistentState<HAICaseState> _state;

    public HaiCaseGrain(
        [PersistentState("haiCaseState", "haiCaseStore")] IPersistentState<HAICaseState> state)
    {
        _state = state;
    }

    public Task<HAICaseState> GetCaseAsync() =>
        Task.FromResult(_state.State);

    public async Task CreateCaseAsync(
        string caseId,
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        string locationId,
        string locationName,
        HAIType haiType,
        DateTime? infectionDate,
        string pathogen,
        string reportedById,
        string reportedByName,
        string? notes)
    {
        _state.State.CaseId = caseId;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.HAIType = haiType;
        _state.State.Status = HAICaseStatus.Suspected;
        _state.State.InfectionDate = infectionDate;
        _state.State.Pathogen = pathogen ?? string.Empty;
        _state.State.ReportedById = reportedById;
        _state.State.ReportedByName = reportedByName;
        _state.State.Notes = notes ?? string.Empty;
        _state.State.ReportedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(HAICaseStatus status, DateTime? confirmedDate)
    {
        _state.State.Status = status;
        if (confirmedDate.HasValue)
            _state.State.ConfirmedDate = confirmedDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateClinicalDataAsync(
        string cultureSource,
        DateTime? cultureDate,
        string gramStain,
        string cultureResult,
        string deviceType,
        int? deviceInDays,
        DateTime? surgeryDate,
        string surgeryProcedure)
    {
        _state.State.CultureSource = cultureSource ?? string.Empty;
        _state.State.CultureDate = cultureDate;
        _state.State.GramStain = gramStain ?? string.Empty;
        _state.State.CultureResult = cultureResult ?? string.Empty;
        _state.State.DeviceType = deviceType ?? string.Empty;
        _state.State.DeviceInDays = deviceInDays;
        _state.State.SurgeryDate = surgeryDate;
        _state.State.SurgeryProcedure = surgeryProcedure ?? string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSusceptibilityResultAsync(AntibioticSusceptibilityResult result)
    {
        int idx = _state.State.SusceptibilityResults
            .FindIndex(r => r.AntibioticName == result.AntibioticName);
        if (idx >= 0)
            _state.State.SusceptibilityResults[idx] = result;
        else
            _state.State.SusceptibilityResults.Add(result);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LinkToOutbreakAsync(string outbreakId)
    {
        _state.State.OutbreakId = outbreakId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UnlinkFromOutbreakAsync()
    {
        _state.State.OutbreakId = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
