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
/// Provider Patient Index Grain — the "My Patients" list for a provider.
/// Denormalized patient demographics for fast search without fan-out reads.
///
/// Key: "PROV-PAT-IDX:{providerId}"
/// </summary>
public class ProviderPatientIndexGrain : Grain, IProviderPatientIndexGrain
{
    private readonly IPersistentState<ProviderPatientIndexState> _state;

    public ProviderPatientIndexGrain(
        [PersistentState("providerPatientIndex", "providerPatientIndexStore")] IPersistentState<ProviderPatientIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProviderId))
        {
            _state.State.ProviderId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdatePatientAsync(ProviderPatientEntry entry)
    {
        ProviderPatientEntry? existing = _state.State.Patients
            .FirstOrDefault(p => p.PatientId == entry.PatientId);

        if (existing != null)
        {
            existing.PatientName = entry.PatientName;
            existing.DateOfBirth = entry.DateOfBirth;
            existing.SsnLast4 = entry.SsnLast4;
            existing.Relationship = entry.Relationship;
            existing.IsActive = entry.IsActive;
            if (entry.LastSeenDate.HasValue)
                existing.LastSeenDate = entry.LastSeenDate;
            if (entry.NextAppointmentDate.HasValue)
                existing.NextAppointmentDate = entry.NextAppointmentDate;
        }
        else
        {
            _state.State.Patients.Add(entry);
        }

        await _state.WriteStateAsync();
    }

    public async Task RemovePatientAsync(string patientId)
    {
        _state.State.Patients.RemoveAll(p => p.PatientId == patientId);
        await _state.WriteStateAsync();
    }

    public async Task DeactivatePatientAsync(string patientId)
    {
        ProviderPatientEntry? patient = _state.State.Patients
            .FirstOrDefault(p => p.PatientId == patientId);

        if (patient != null)
        {
            patient.IsActive = false;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<ProviderPatientEntry>> GetAllPatientsAsync()
        => Task.FromResult(_state.State.Patients);

    public Task<List<ProviderPatientEntry>> GetActivePatientsAsync()
        => Task.FromResult(_state.State.Patients
            .Where(p => p.IsActive)
            .OrderBy(p => p.PatientName)
            .ToList());

    public Task<List<ProviderPatientEntry>> SearchPatientsAsync(string nameFilter)
        => Task.FromResult(_state.State.Patients
            .Where(p => p.IsActive &&
                        p.PatientName.StartsWith(nameFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.PatientName)
            .ToList());

    public Task<List<ProviderPatientEntry>> GetPatientsByRoleAsync(string role)
        => Task.FromResult(_state.State.Patients
            .Where(p => p.IsActive && p.Relationship == role)
            .OrderBy(p => p.PatientName)
            .ToList());

    public async Task UpdateLastSeenAsync(string patientId, DateTime lastSeenDate)
    {
        ProviderPatientEntry? patient = _state.State.Patients
            .FirstOrDefault(p => p.PatientId == patientId);

        if (patient != null)
        {
            patient.LastSeenDate = lastSeenDate;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateNextAppointmentAsync(string patientId, DateTime? nextAppointmentDate)
    {
        ProviderPatientEntry? patient = _state.State.Patients
            .FirstOrDefault(p => p.PatientId == patientId);

        if (patient != null)
        {
            patient.NextAppointmentDate = nextAppointmentDate;
            await _state.WriteStateAsync();
        }
    }
}
