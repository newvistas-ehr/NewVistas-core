// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ClinicIndexGrain : Grain, IClinicIndexGrain
{
    private readonly IPersistentState<ClinicIndexState> _state;

    public ClinicIndexGrain(
        [PersistentState("clinicIndex", "clinicIndexStore")]
        IPersistentState<ClinicIndexState> state)
    {
        _state = state;
    }

    public async Task<List<ClinicEntry>> GetAllClinicsAsync()
    {
        if (_state.State.Clinics.Count == 0)
            await SeedDemoClinicsAsync();

        return _state.State.Clinics;
    }

    public async Task AddOrUpdateClinicAsync(ClinicEntry entry)
    {
        int idx = _state.State.Clinics.FindIndex(c => c.ClinicId == entry.ClinicId);
        if (idx >= 0)
            _state.State.Clinics[idx] = entry;
        else
            _state.State.Clinics.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<ClinicEntry>> SearchClinicsAsync(string term)
    {
        string lower = term.ToLowerInvariant();
        List<ClinicEntry> results = _state.State.Clinics
            .Where(c => c.Name.ToLowerInvariant().Contains(lower)
                     || (c.StopCode != null && c.StopCode.Contains(lower)))
            .ToList();
        return Task.FromResult(results);
    }

    public async Task SeedDemoClinicsAsync()
    {
        if (_state.State.Clinics.Count > 0) return;

        _state.State.Clinics.AddRange(new[]
        {
            new ClinicEntry { ClinicId = "SD-CLINIC-001", Name = "PRIMARY CARE",   Division = "MAIN",  StopCode = "323", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-002", Name = "MENTAL HEALTH",  Division = "MAIN",  StopCode = "502", AppointmentLength = 60, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-003", Name = "CARDIOLOGY",     Division = "MAIN",  StopCode = "303", AppointmentLength = 45, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-004", Name = "DERMATOLOGY",    Division = "MAIN",  StopCode = "313", AppointmentLength = 20, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-005", Name = "ORTHOPEDICS",    Division = "NORTH", StopCode = "218", AppointmentLength = 45, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-006", Name = "PHARMACY MTAC",  Division = "MAIN",  StopCode = "160", AppointmentLength = 30, Status = "ACTIVE" },
        });

        await _state.WriteStateAsync();
    }
}
