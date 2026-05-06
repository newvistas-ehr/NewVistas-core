// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// CPT Code Index Grain — singleton holding the searchable catalog.
///
/// Grain Key: "CPT-INDEX"
/// </summary>
public class CptCodeIndexGrain : Grain, ICptCodeIndexGrain
{
    private readonly IPersistentState<CptCodeIndexState> _state;

    public CptCodeIndexGrain(
        [PersistentState("cptCodeIndexState", "cptCodeIndexStore")]
        IPersistentState<CptCodeIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateEntryAsync(CptCodeIndexEntry entry)
    {
        _state.State.Codes[entry.Code] = entry;
        _state.State.TotalCodes = _state.State.Codes.Count;
        _state.State.ActiveCodes = _state.State.Codes.Values.Count(c => c.Status == "ACTIVE");
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task LoadCodesAsync(List<CptCodeIndexEntry> entries)
    {
        _state.State.Codes.Clear();

        foreach (CptCodeIndexEntry entry in entries)
        {
            _state.State.Codes[entry.Code] = entry;
        }

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalCodes = entries.Count;
        _state.State.ActiveCodes = entries.Count(e => e.Status == "ACTIVE");

        await _state.WriteStateAsync();
    }

    public Task<CptCodeIndexEntry?> GetCodeAsync(string code)
    {
        _state.State.Codes.TryGetValue(code, out CptCodeIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<CptCodeIndexEntry>> SearchAsync(string searchText, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Return all codes up to maxResults when search text is empty
            List<CptCodeIndexEntry> all = _state.State.Codes.Values
                .OrderBy(e => e.Code)
                .Take(maxResults > 0 ? maxResults : _state.State.Codes.Count)
                .ToList();
            return Task.FromResult(all);
        }

        string query = searchText.Trim();

        // If it looks like a code (all digits), search by code prefix
        bool isCodeSearch = query.All(char.IsDigit);

        IEnumerable<CptCodeIndexEntry> results;

        if (isCodeSearch)
        {
            results = _state.State.Codes.Values
                .Where(e => e.Code.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            results = _state.State.Codes.Values
                .Where(e => e.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            e.LongDescription.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(results
            .OrderBy(e => e.Code)
            .Take(maxResults)
            .ToList());
    }

    public Task<List<CptCodeIndexEntry>> GetByCategoryAsync(string category, int maxResults)
    {
        List<CptCodeIndexEntry> results = _state.State.Codes.Values
            .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Code)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<CptCodeIndexEntry>> GetActiveCodesAsync(int maxResults)
    {
        IEnumerable<CptCodeIndexEntry> query = _state.State.Codes.Values
            .Where(e => e.Status == "ACTIVE")
            .OrderBy(e => e.Code);

        if (maxResults > 0)
            query = query.Take(maxResults);

        return Task.FromResult(query.ToList());
    }

    public Task<CptCodeIndexStatus> GetStatusAsync()
    {
        return Task.FromResult(new CptCodeIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalCodes = _state.State.TotalCodes,
            ActiveCodes = _state.State.ActiveCodes
        });
    }

    public async Task ClearAsync()
    {
        _state.State.Codes.Clear();
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        _state.State.TotalCodes = 0;
        _state.State.ActiveCodes = 0;

        await _state.WriteStateAsync();
    }
}
