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
/// Drug Class Index Grain — in-memory catalog of all VA drug therapeutic classes.
/// Singleton grain keyed "NDF-CLASS-INDEX".
/// </summary>
public class DrugClassIndexGrain : Grain, IDrugClassIndexGrain
{
    private readonly IPersistentState<DrugClassIndexState> _state;

    public DrugClassIndexGrain(
        [PersistentState("ndfClassIndexState", "ndfClassIndexStore")] IPersistentState<DrugClassIndexState> state)
    {
        _state = state;
    }

    public async Task LoadClassesAsync(List<DrugClassEntry> entries)
    {
        _state.State.ClassesByCode.Clear();

        foreach (DrugClassEntry entry in entries)
        {
            // Normalize codes to uppercase for consistent case-insensitive lookup
            entry.Code = entry.Code.ToUpperInvariant();
            if (entry.ParentCode != null)
                entry.ParentCode = entry.ParentCode.ToUpperInvariant();
            _state.State.ClassesByCode[entry.Code] = entry;
        }

        _state.State.TotalClasses = entries.Count;
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<DrugClassEntry?> GetClassAsync(string code)
    {
        _state.State.ClassesByCode.TryGetValue(code.ToUpperInvariant(), out DrugClassEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<DrugClassEntry>> GetAllClassesAsync() =>
        Task.FromResult(_state.State.ClassesByCode.Values
            .OrderBy(c => c.Code)
            .ToList());

    public Task<List<DrugClassEntry>> SearchAsync(string searchText, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<DrugClassEntry>());

        string normalized = searchText.Trim().ToUpperInvariant();

        List<DrugClassEntry> results = _state.State.ClassesByCode.Values
            .Where(c =>
                c.Code.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Code)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<DrugClassEntry>> GetChildClassesAsync(string parentCode)
    {
        string normalizedParent = parentCode.ToUpperInvariant();

        List<DrugClassEntry> results = _state.State.ClassesByCode.Values
            .Where(c => string.Equals(c.ParentCode, normalizedParent, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Code)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> IsLoadedAsync() =>
        Task.FromResult(_state.State.IsLoaded);

    public Task<NdfClassIndexStatus> GetStatusAsync() =>
        Task.FromResult(new NdfClassIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalClasses = _state.State.TotalClasses
        });

    public async Task ClearAsync()
    {
        _state.State.ClassesByCode.Clear();
        _state.State.TotalClasses = 0;
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        await _state.WriteStateAsync();
    }
}
