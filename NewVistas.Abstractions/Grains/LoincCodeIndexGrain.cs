// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// LOINC Code Index Grain — singleton holding the searchable catalog.
///
/// Grain Key: "LOINC-INDEX"
/// </summary>
public class LoincCodeIndexGrain : Grain, ILoincCodeIndexGrain
{
    private readonly IPersistentState<LoincCodeIndexState> _state;

    public LoincCodeIndexGrain(
        [PersistentState("loincCodeIndexState", "loincCodeIndexStore")]
        IPersistentState<LoincCodeIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateEntryAsync(LoincCodeIndexEntry entry)
    {
        _state.State.Codes[entry.Code] = entry;
        _state.State.TotalCodes = _state.State.Codes.Count;
        _state.State.ActiveCodes = _state.State.Codes.Values.Count(c => c.Status == "ACTIVE");
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task LoadCodesAsync(List<LoincCodeIndexEntry> entries)
    {
        _state.State.Codes.Clear();

        foreach (LoincCodeIndexEntry entry in entries)
        {
            _state.State.Codes[entry.Code] = entry;
        }

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalCodes = entries.Count;
        _state.State.ActiveCodes = entries.Count(e => e.Status == "ACTIVE");

        await _state.WriteStateAsync();
    }

    public Task<LoincCodeIndexEntry?> GetCodeAsync(string code)
    {
        _state.State.Codes.TryGetValue(code, out LoincCodeIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<LoincCodeIndexEntry>> SearchAsync(string searchText, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Return all codes up to maxResults when search text is empty
            List<LoincCodeIndexEntry> all = _state.State.Codes.Values
                .OrderBy(e => e.Code)
                .Take(maxResults > 0 ? maxResults : _state.State.Codes.Count)
                .ToList();
            return Task.FromResult(all);
        }

        string query = searchText.Trim();

        // If it contains a hyphen and digits, treat as LOINC code search
        bool isCodeSearch = query.Any(char.IsDigit) && query.Contains('-');

        IEnumerable<LoincCodeIndexEntry> results;

        if (isCodeSearch)
        {
            results = _state.State.Codes.Values
                .Where(e => e.Code.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            results = _state.State.Codes.Values
                .Where(e => e.Component.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            e.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            e.LongCommonName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(results
            .OrderBy(e => e.Code)
            .Take(maxResults)
            .ToList());
    }

    public Task<List<LoincCodeIndexEntry>> GetBySystemAsync(string system, int maxResults)
    {
        List<LoincCodeIndexEntry> results = _state.State.Codes.Values
            .Where(e => e.System.Contains(system, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Code)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<LoincCodeIndexEntry>> GetActiveCodesAsync(int maxResults)
    {
        IEnumerable<LoincCodeIndexEntry> query = _state.State.Codes.Values
            .Where(e => e.Status == "ACTIVE")
            .OrderBy(e => e.Code);

        if (maxResults > 0)
            query = query.Take(maxResults);

        return Task.FromResult(query.ToList());
    }

    public Task<LoincCodeIndexStatus> GetStatusAsync()
    {
        return Task.FromResult(new LoincCodeIndexStatus
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
