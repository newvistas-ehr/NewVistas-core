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
/// System-level singleton index for appointment wait list entries.
/// Keyed by "SD-WL-IDX". Supports queries by clinic, patient, status, and priority.
/// </summary>
public class AppointmentWaitListIndexGrain : Grain, IAppointmentWaitListIndexGrain
{
    private readonly IPersistentState<AppointmentWaitListIndexState> _state;

    public AppointmentWaitListIndexGrain(
        [PersistentState("appointmentWaitListIndexState", "appointmentWaitListIndexStore")]
        IPersistentState<AppointmentWaitListIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(AppointmentWaitListIndexEntry entry)
    {
        _state.State.Entries[entry.EntryId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string entryId)
    {
        _state.State.Entries.Remove(entryId);
        await _state.WriteStateAsync();
    }

    public Task<List<AppointmentWaitListIndexEntry>> GetByClinicAsync(string clinicId, int maxResults = 50)
    {
        List<AppointmentWaitListIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.ClinicId == clinicId)
            .OrderByDescending(e => e.WaitListDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<AppointmentWaitListIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50)
    {
        List<AppointmentWaitListIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.WaitListDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<AppointmentWaitListIndexEntry>> GetByStatusAsync(string status, int maxResults = 50)
    {
        List<AppointmentWaitListIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.WaitListDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<AppointmentWaitListIndexEntry>> GetPendingByClinicAsync(string clinicId, int maxResults = 50)
    {
        List<AppointmentWaitListIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.ClinicId == clinicId && e.Status == "WAITING")
            .OrderBy(e => e.Priority == "STAT" ? 0 : e.Priority == "URGENT" ? 1 : 2)
            .ThenBy(e => e.WaitListDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<AppointmentWaitListIndexEntry>> SearchAsync(
        string? clinicId, string? status, string? priority, int maxResults = 50)
    {
        IEnumerable<AppointmentWaitListIndexEntry> query = _state.State.Entries.Values;

        if (!string.IsNullOrWhiteSpace(clinicId))
            query = query.Where(e => e.ClinicId == clinicId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(e => e.Priority == priority);

        List<AppointmentWaitListIndexEntry> results = query
            .OrderBy(e => e.Priority == "STAT" ? 0 : e.Priority == "URGENT" ? 1 : 2)
            .ThenBy(e => e.WaitListDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);

    public Task<AppointmentWaitListIndexEntry?> FindBestMatchForSlotAsync(string clinicId, DateTime slotDateTime)
    {
        AppointmentWaitListIndexEntry? match = _state.State.Entries.Values
            .Where(e => e.ClinicId == clinicId
                && e.Status == "WAITING"
                && (e.DesiredDateRangeStart == null || slotDateTime.Date >= e.DesiredDateRangeStart.Value.Date)
                && (e.DesiredDateRangeEnd == null || slotDateTime.Date <= e.DesiredDateRangeEnd.Value.Date))
            .OrderBy(e => e.Priority == "STAT" ? 0 : e.Priority == "URGENT" ? 1 : 2)
            .ThenBy(e => e.WaitListDate)
            .FirstOrDefault();

        return Task.FromResult(match);
    }
}
