// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MailGroupIndexGrain : Grain, IMailGroupIndexGrain
{
    private readonly IPersistentState<MailGroupIndexState> _state;

    public MailGroupIndexGrain(
        [PersistentState("mailGroupIndex", "mailGroupIndexStore")]
        IPersistentState<MailGroupIndexState> state)
    {
        _state = state;
    }

    public async Task<List<MailGroupIndexEntry>> GetAllGroupsAsync()
    {
        if (_state.State.Groups.Count == 0)
            await SeedDemoGroupsAsync();

        return _state.State.Groups;
    }

    public async Task AddOrUpdateGroupAsync(MailGroupIndexEntry entry)
    {
        int idx = _state.State.Groups.FindIndex(g => g.Name == entry.Name);
        if (idx >= 0)
            _state.State.Groups[idx] = entry;
        else
            _state.State.Groups.Add(entry);

        await _state.WriteStateAsync();
    }

    public async Task RemoveGroupAsync(string groupName)
    {
        _state.State.Groups.RemoveAll(g => g.Name == groupName);
        await _state.WriteStateAsync();
    }

    public Task<List<MailGroupIndexEntry>> SearchGroupsAsync(string searchTerm)
    {
        string lower = searchTerm.ToLowerInvariant();
        List<MailGroupIndexEntry> results = _state.State.Groups
            .Where(g => g.Name.ToLowerInvariant().Contains(lower)
                     || g.Description.ToLowerInvariant().Contains(lower))
            .ToList();
        return Task.FromResult(results);
    }

    private async Task SeedDemoGroupsAsync()
    {
        if (_state.State.Groups.Count > 0) return;

        _state.State.Groups.AddRange(new[]
        {
            new MailGroupIndexEntry { Name = "MEDICINE-SERVICE",   Description = "Medicine Service clinicians",  MemberCount = 0, IsActive = true },
            new MailGroupIndexEntry { Name = "SURGERY-SERVICE",    Description = "Surgery Service staff",        MemberCount = 0, IsActive = true },
            new MailGroupIndexEntry { Name = "PHARMACY-SERVICE",   Description = "Pharmacy team",                MemberCount = 0, IsActive = true },
            new MailGroupIndexEntry { Name = "LAB-SERVICE",        Description = "Laboratory personnel",         MemberCount = 0, IsActive = true },
            new MailGroupIndexEntry { Name = "RADIOLOGY-SERVICE",  Description = "Radiology department",         MemberCount = 0, IsActive = true },
            new MailGroupIndexEntry { Name = "ADMIN-STAFF",        Description = "Administrative staff",         MemberCount = 0, IsActive = true },
        });

        await _state.WriteStateAsync();
    }
}
