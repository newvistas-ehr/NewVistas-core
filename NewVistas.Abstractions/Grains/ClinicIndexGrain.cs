// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
            // RADIOLOGY = diagnostic imaging (X-ray/CT/MRI, File #75.1); RADIATION ONCOLOGY =
            // therapeutic cancer radiation — kept as distinct clinics so an imaging order and a
            // radiation-therapy referral each point at the right place.
            new ClinicEntry { ClinicId = "SD-CLINIC-007", Name = "RADIOLOGY",           Division = "MAIN",  StopCode = "105", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-008", Name = "RADIATION ONCOLOGY",  Division = "MAIN",  StopCode = "317", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-009", Name = "HEMATOLOGY/ONCOLOGY", Division = "MAIN",  StopCode = "316", AppointmentLength = 45, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-010", Name = "LABORATORY",          Division = "MAIN",  StopCode = "108", AppointmentLength = 15, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-011", Name = "WOMEN'S HEALTH",      Division = "MAIN",  StopCode = "322", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-012", Name = "NEUROLOGY",           Division = "MAIN",  StopCode = "315", AppointmentLength = 45, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-013", Name = "GASTROENTEROLOGY",    Division = "NORTH", StopCode = "307", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-014", Name = "GENERAL SURGERY",     Division = "MAIN",  StopCode = "401", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-015", Name = "UROLOGY",             Division = "NORTH", StopCode = "314", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-016", Name = "PHYSICAL THERAPY",    Division = "SOUTH", StopCode = "205", AppointmentLength = 45, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-017", Name = "EYE CLINIC",          Division = "MAIN",  StopCode = "408", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-018", Name = "ENT",                 Division = "NORTH", StopCode = "309", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-019", Name = "PULMONARY",           Division = "MAIN",  StopCode = "312", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-020", Name = "ENDOCRINOLOGY",       Division = "MAIN",  StopCode = "305", AppointmentLength = 30, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-021", Name = "EMERGENCY DEPARTMENT",Division = "MAIN",  StopCode = "130", AppointmentLength = 60, Status = "ACTIVE" },
            new ClinicEntry { ClinicId = "SD-CLINIC-022", Name = "NUTRITION",           Division = "SOUTH", StopCode = "124", AppointmentLength = 30, Status = "ACTIVE" },
        });

        await _state.WriteStateAsync();
    }
}
