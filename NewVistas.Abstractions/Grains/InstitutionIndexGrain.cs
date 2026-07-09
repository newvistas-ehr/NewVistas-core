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
/// Singleton institution directory ("INSTITUTION-INDEX") — written only by
/// InstitutionGrain. Also owns the legacy-alias map so pre-institution facility
/// strings ("MAIN", "INST-500") resolve to canonical ids.
/// </summary>
public class InstitutionIndexGrain : Grain, IInstitutionIndexGrain
{
    private readonly IPersistentState<InstitutionIndexState> _state;

    public InstitutionIndexGrain(
        [PersistentState("institutionIndex", "institutionIndexStore")]
        IPersistentState<InstitutionIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(InstitutionIndexEntry entry, IEnumerable<string>? legacyAliases = null)
    {
        if (string.IsNullOrWhiteSpace(entry.InstitutionId))
            return;

        _state.State.Institutions[entry.InstitutionId] = entry;
        if (legacyAliases is not null)
        {
            foreach (string alias in legacyAliases)
                if (!string.IsNullOrWhiteSpace(alias))
                    _state.State.LegacyAliasMap[alias] = entry.InstitutionId;
        }
        await _state.WriteStateAsync();
    }

    public Task<List<InstitutionIndexEntry>> GetAllAsync(bool activeOnly = true)
        => Task.FromResult(_state.State.Institutions.Values
            .Where(i => !activeOnly || i.IsActive)
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public Task<List<InstitutionIndexEntry>> GetByHealthSystemAsync(string healthSystemId)
        => Task.FromResult(_state.State.Institutions.Values
            .Where(i => i.IsActive && string.Equals(i.HealthSystemId, healthSystemId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public Task<List<InstitutionIndexEntry>> SearchAsync(string? nameContains, InstitutionType? type, string? capability)
        => Task.FromResult(_state.State.Institutions.Values
            .Where(i => i.IsActive)
            .Where(i => nameContains is null || i.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            .Where(i => type is null || i.Type == type)
            .Where(i => capability is null || i.Capabilities.Contains(capability))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public Task<string?> ResolveLegacyFacilityIdAsync(string legacyId)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
            return Task.FromResult<string?>(null);
        if (_state.State.Institutions.ContainsKey(legacyId))
            return Task.FromResult<string?>(legacyId);
        return Task.FromResult(_state.State.LegacyAliasMap.TryGetValue(legacyId, out string? canonical)
            ? canonical
            : null);
    }

    public Task<int> GetActiveCountAsync()
        => Task.FromResult(_state.State.Institutions.Values.Count(i => i.IsActive));
}
