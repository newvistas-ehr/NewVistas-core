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
/// Per-patient prescription index grain.
/// Grain key: "PSO-INDEX:{patientId}"
/// </summary>
public class PatientPrescriptionIndexGrain : Grain, IPatientPrescriptionIndexGrain
{
    private readonly IPersistentState<PatientPrescriptionIndexState> _state;

    public PatientPrescriptionIndexGrain(
        [PersistentState("prescriptionIndexState", "prescriptionIndexStore")]
        IPersistentState<PatientPrescriptionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateEntryAsync(PrescriptionIndexEntry entry)
    {
        int existing = _state.State.Prescriptions
            .FindIndex(e => e.PrescriptionId == entry.PrescriptionId);

        if (existing >= 0)
            _state.State.Prescriptions[existing] = entry;
        else
            _state.State.Prescriptions.Add(entry);

        _state.State.TotalPrescriptions = _state.State.Prescriptions.Count;
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string prescriptionId)
    {
        _state.State.Prescriptions.RemoveAll(e => e.PrescriptionId == prescriptionId);
        _state.State.TotalPrescriptions = _state.State.Prescriptions.Count;
        await _state.WriteStateAsync();
    }

    public Task<List<PrescriptionIndexEntry>> GetAllAsync() =>
        Task.FromResult(_state.State.Prescriptions
            .OrderByDescending(e => e.IssueDate)
            .ToList());

    public Task<List<PrescriptionIndexEntry>> GetActiveAsync() =>
        Task.FromResult(_state.State.Prescriptions
            .Where(e => e.Status == "ACTIVE")
            .OrderByDescending(e => e.IssueDate)
            .ToList());

    public Task<List<PrescriptionIndexEntry>> GetByStatusAsync(string status) =>
        Task.FromResult(_state.State.Prescriptions
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.IssueDate)
            .ToList());

    public Task<int> GetTotalCountAsync() =>
        Task.FromResult(_state.State.TotalPrescriptions);

    public async Task ClearAsync()
    {
        _state.State.Prescriptions.Clear();
        _state.State.TotalPrescriptions = 0;
        await _state.WriteStateAsync();
    }
}
