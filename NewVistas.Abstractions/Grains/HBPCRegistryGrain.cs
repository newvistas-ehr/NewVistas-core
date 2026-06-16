// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class HBPCRegistryState
{
    [Id(0)] public List<HBPCRegistryEntry> Patients { get; set; } = new();
}

public class HBPCRegistryGrain : Grain, IHBPCRegistryGrain
{
    private readonly IPersistentState<HBPCRegistryState> _state;

    public HBPCRegistryGrain(
        [PersistentState("hbpcRegistryState", "hbpcRegistryStore")] IPersistentState<HBPCRegistryState> state)
    {
        _state = state;
    }

    public async Task UpsertPatientAsync(HBPCRegistryEntry entry)
    {
        HBPCRegistryEntry? existing = _state.State.Patients.Find(p => p.PatientId == entry.PatientId);
        if (existing is not null)
            _state.State.Patients.Remove(existing);
        _state.State.Patients.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<HBPCRegistryEntry>> GetAllPatientsAsync()
    {
        List<HBPCRegistryEntry> result = _state.State.Patients
            .OrderBy(p => p.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HBPCRegistryEntry>> GetActivePatientsAsync()
    {
        List<HBPCRegistryEntry> result = _state.State.Patients
            .Where(p => p.ProgramStatus == HBPCProgramStatus.Active)
            .OrderBy(p => p.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HBPCRegistryEntry>> GetPatientsByLevelOfCareAsync(HBPCLevelOfCare levelOfCare)
    {
        List<HBPCRegistryEntry> result = _state.State.Patients
            .Where(p => p.LevelOfCare == levelOfCare && p.ProgramStatus == HBPCProgramStatus.Active)
            .OrderBy(p => p.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HBPCRegistryEntry>> GetPatientsWithUpcomingVisitsAsync(int withinDays)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(withinDays);
        List<HBPCRegistryEntry> result = _state.State.Patients
            .Where(p => p.ProgramStatus == HBPCProgramStatus.Active
                && p.NextScheduledVisit.HasValue
                && p.NextScheduledVisit.Value <= cutoff)
            .OrderBy(p => p.NextScheduledVisit)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HBPCRegistryEntry>> GetPatientsWithNoRecentVisitAsync(int daysSinceLastVisit)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-daysSinceLastVisit);
        List<HBPCRegistryEntry> result = _state.State.Patients
            .Where(p => p.ProgramStatus == HBPCProgramStatus.Active
                && (!p.LastVisitDate.HasValue || p.LastVisitDate.Value < cutoff))
            .OrderBy(p => p.LastVisitDate)
            .ToList();
        return Task.FromResult(result);
    }
}
