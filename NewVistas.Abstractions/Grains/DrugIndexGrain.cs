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
/// Singleton index grain for the facility drug catalog (VistA File #50).
/// Grain key: "DRUG-INDEX"
/// </summary>
public class DrugIndexGrain : Grain, IDrugIndexGrain
{
    private readonly IPersistentState<DrugIndexState> _state;

    public DrugIndexGrain(
        [PersistentState("drugIndexState", "drugIndexStore")]
        IPersistentState<DrugIndexState> state)
    {
        _state = state;
    }

    public async Task LoadDrugsAsync(List<DrugIndexEntry> entries)
    {
        _state.State.ByIen.Clear();

        foreach (DrugIndexEntry entry in entries)
            _state.State.ByIen[entry.Ien] = entry;

        _state.State.TotalDrugs = entries.Count;
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<DrugIndexEntry?> GetDrugAsync(string ien)
    {
        _state.State.ByIen.TryGetValue(ien, out DrugIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<DrugIndexEntry>> GetByVaProductAsync(string vaProductIen)
    {
        List<DrugIndexEntry> results = _state.State.ByIen.Values
            .Where(e => e.VaProductIen == vaProductIen)
            .OrderBy(e => e.LocalName)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<DrugIndexEntry>> GetByVaGenericAsync(string vaGenericIen)
    {
        List<DrugIndexEntry> results = _state.State.ByIen.Values
            .Where(e => e.VaGenericIen == vaGenericIen)
            .OrderBy(e => e.LocalName)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<DrugIndexEntry>> SearchAsync(
        string searchText,
        PharmacyType? pharmacyType = null,
        bool activeOnly = true,
        int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<DrugIndexEntry>());

        string normalized = searchText.Trim().ToUpperInvariant();

        IEnumerable<DrugIndexEntry> query = _state.State.ByIen.Values
            .Where(e => e.LocalName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                     || e.VaGenericName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                     || e.Synonyms.Any(s => s.Contains(normalized, StringComparison.OrdinalIgnoreCase)));

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        if (pharmacyType.HasValue)
            query = query.Where(e => e.PharmacyType == pharmacyType.Value || e.PharmacyType == PharmacyType.All);

        List<DrugIndexEntry> results = query
            .OrderBy(e => e.LocalName)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<DrugIndexStatus> GetStatusAsync() =>
        Task.FromResult(new DrugIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalDrugs = _state.State.TotalDrugs
        });

    public async Task ClearAsync()
    {
        _state.State.ByIen.Clear();
        _state.State.TotalDrugs = 0;
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        await _state.WriteStateAsync();
    }
}
