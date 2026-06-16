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
/// Volunteer Index Grain — singleton registry index for all VA Voluntary Service volunteers.
/// VistA VOLUNTARY SERVICE file (#8810).
/// </summary>
public class VolunteerIndexGrain : Grain, IVolunteerIndexGrain
{
    private readonly IPersistentState<VolunteerIndexState> _state;

    public VolunteerIndexGrain(
        [PersistentState("volunteerIndexState", "vsIndexStore")] IPersistentState<VolunteerIndexState> state)
    {
        _state = state;
    }

    public Task<List<VolunteerIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<VolunteerIndexEntry>> GetByStatusAsync(VolunteerStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public Task<List<VolunteerIndexEntry>> GetByServiceTypeAsync(VolunteerServiceType serviceType)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.PrimaryServiceType == serviceType)
            .ToList());

    public Task<List<VolunteerIndexEntry>> SearchAsync(string nameFragment)
    {
        string fragment = nameFragment.ToLowerInvariant();
        List<VolunteerIndexEntry> results = _state.State.Entries
            .Where(e => e.FirstName.ToLowerInvariant().Contains(fragment)
                     || e.LastName.ToLowerInvariant().Contains(fragment))
            .ToList();
        return Task.FromResult(results);
    }

    public async Task UpsertEntryAsync(VolunteerIndexEntry entry)
    {
        int existing = _state.State.Entries.FindIndex(e => e.VolunteerId == entry.VolunteerId);
        if (existing >= 0)
            _state.State.Entries[existing] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string volunteerId)
    {
        _state.State.Entries.RemoveAll(e => e.VolunteerId == volunteerId);
        await _state.WriteStateAsync();
    }
}
