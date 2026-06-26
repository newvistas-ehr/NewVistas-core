// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Provider Directory Grain — singleton staff/provider name index. Key "PROVIDER-DIRECTORY".
/// Single writer (NewPersonGrain syncs it); reads are by-name search and by-id lookup.
/// Mirrors VistA's ^VA(200,"B") name cross-reference for provider selection.
/// </summary>
public class ProviderDirectoryGrain : Grain, IProviderDirectoryGrain
{
    private readonly IPersistentState<ProviderDirectoryState> _state;

    public ProviderDirectoryGrain(
        [PersistentState("providerDirectory", "providerDirectoryStore")]
        IPersistentState<ProviderDirectoryState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ProviderDirectoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.UserId))
            return;

        _state.State.Providers[entry.UserId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync(string userId, bool isActive)
    {
        if (_state.State.Providers.TryGetValue(userId, out ProviderDirectoryEntry? entry))
        {
            _state.State.Providers[userId] = entry with { IsActive = isActive };
            await _state.WriteStateAsync();
        }
    }

    public Task<ProviderDirectoryEntry?> GetAsync(string userId)
        => Task.FromResult(_state.State.Providers.GetValueOrDefault(userId));

    public Task<List<ProviderDirectoryEntry>> SearchAsync(string searchTerm, int maxResults = 25)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Task.FromResult(new List<ProviderDirectoryEntry>());

        string term = searchTerm.Trim();

        List<ProviderDirectoryEntry> results = _state.State.Providers.Values
            .Where(p => p.IsActive)
            .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(p.UserId, term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Providers.Count);
}
