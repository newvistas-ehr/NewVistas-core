// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PersonIndexGrain : Grain, IPersonIndexGrain
{
    private readonly IPersistentState<PersonIndexState> _state;

    public PersonIndexGrain(
        [PersistentState("personIndexState", "personIndexStore")] IPersistentState<PersonIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertAsync(PersonIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.PersonId == entry.PersonId);
        _state.State.Entries.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string personId)
    {
        int removed = _state.State.Entries.RemoveAll(e => e.PersonId == personId);
        if (removed == 0) return;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<PersonIndexEntry>> GetAllAsync() => Task.FromResult(_state.State.Entries.ToList());

    public Task<List<PersonIndexEntry>> SearchByNameAsync(string namePrefix)
    {
        if (string.IsNullOrWhiteSpace(namePrefix))
            return Task.FromResult(new List<PersonIndexEntry>());
        string p = namePrefix.Trim();
        return Task.FromResult(_state.State.Entries
            .Where(e => e.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public Task<List<PersonIndexEntry>> GetEmployeePatientsAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.IsEmployeePatient).ToList());
}
