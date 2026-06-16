// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PatientScheduleIndexGrain : Grain, IPatientScheduleIndexGrain
{
    private readonly IPersistentState<PatientScheduleIndexState> _state;

    public PatientScheduleIndexGrain(
        [PersistentState("patientScheduleIndex", "scheduleIndexStore")]
        IPersistentState<PatientScheduleIndexState> state)
    {
        _state = state;
    }

    public Task<List<AppointmentEntry>> GetAppointmentsAsync(int max = 50)
    {
        List<AppointmentEntry> results = _state.State.Appointments
            .OrderByDescending(a => a.AppointmentDateTime)
            .Take(max)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<AppointmentEntry>> GetUpcomingAsync(int max = 20)
    {
        DateTime now = DateTime.UtcNow;
        List<AppointmentEntry> results = _state.State.Appointments
            .Where(a => a.AppointmentDateTime >= now
                     && (a.Status == "Scheduled" || a.Status == "Checked In"))
            .OrderBy(a => a.AppointmentDateTime)
            .Take(max)
            .ToList();
        return Task.FromResult(results);
    }

    public async Task AddOrUpdateAsync(AppointmentEntry entry)
    {
        int idx = _state.State.Appointments.FindIndex(a => a.AppointmentId == entry.AppointmentId);
        if (idx >= 0)
            _state.State.Appointments[idx] = entry;
        else
            _state.State.Appointments.Add(entry);

        await _state.WriteStateAsync();
    }
}
