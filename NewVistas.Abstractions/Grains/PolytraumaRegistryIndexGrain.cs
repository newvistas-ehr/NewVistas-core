// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class PolytraumaRegistryIndexState
{
    [Id(0)] public List<PolytraumaRegistrySummaryEntry> Patients { get; set; } = new();
}

public class PolytraumaRegistryIndexGrain : Grain, IPolytraumaRegistryIndexGrain
{
    private readonly IPersistentState<PolytraumaRegistryIndexState> _state;

    public PolytraumaRegistryIndexGrain(
        [PersistentState("ptRegistryIndexState", "ptRegistryIndexStore")] IPersistentState<PolytraumaRegistryIndexState> state)
    {
        _state = state;
    }

    public Task<List<PolytraumaRegistrySummaryEntry>> GetAllPatientsAsync() =>
        Task.FromResult(_state.State.Patients
            .OrderByDescending(p => p.RegistrationDate)
            .ToList());

    public Task<List<PolytraumaRegistrySummaryEntry>> GetActivePatientAsync() =>
        Task.FromResult(_state.State.Patients
            .Where(p => p.Status == PolytraumaStatus.Active)
            .OrderByDescending(p => p.RegistrationDate)
            .ToList());

    public Task<List<PolytraumaRegistrySummaryEntry>> GetPatientsByStatusAsync(PolytraumaStatus status) =>
        Task.FromResult(_state.State.Patients
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.RegistrationDate)
            .ToList());

    public async Task UpsertPatientAsync(PolytraumaRegistrySummaryEntry entry)
    {
        int idx = _state.State.Patients.FindIndex(p => p.PatientId == entry.PatientId);
        if (idx >= 0)
            _state.State.Patients[idx] = entry;
        else
            _state.State.Patients.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemovePatientAsync(string patientId)
    {
        int idx = _state.State.Patients.FindIndex(p => p.PatientId == patientId);
        if (idx >= 0)
        {
            _state.State.Patients.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
