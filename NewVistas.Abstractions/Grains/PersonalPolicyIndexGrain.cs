// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PersonalPolicyIndexGrain : Grain, IPersonalPolicyIndexGrain
{
    private readonly IPersistentState<PersonalPolicyIndexState> _state;

    public PersonalPolicyIndexGrain(
        [PersistentState("personalPolicyIndexState", "personalPolicyIndexStore")]
        IPersistentState<PersonalPolicyIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PersonalPolicyIndexEntry entry)
    {
        List<PersonalPolicyIndexEntry> entries = _state.State.Entries;
        int idx = entries.FindIndex(e => e.PolicyId == entry.PolicyId);
        if (idx >= 0)
            entries[idx] = entry;
        else
            entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<PersonalPolicyIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PersonalPolicyIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.IsPrimary)
            .ToList());

    public async Task RemoveAsync(string policyId)
    {
        int idx = _state.State.Entries.FindIndex(e => e.PolicyId == policyId);
        if (idx >= 0)
        {
            _state.State.Entries.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
