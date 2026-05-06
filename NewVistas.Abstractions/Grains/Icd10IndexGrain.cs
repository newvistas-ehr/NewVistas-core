// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// ICD-10-CM Index Grain — singleton holding the searchable catalog.
///
/// Grain Key: "ICD10-INDEX"
///
/// With ~97K codes at ~150 bytes per index entry, the full index is ~15MB.
/// This is acceptable for a singleton grain that stays hot and serves
/// all ICD-10 lookups without fan-out to individual code grains.
///
/// Search strategy mirrors VistA:
///   - Code lookup: exact match or prefix match on the code field
///     (like ^ICD9 "B" cross-reference)
///   - Text search: case-insensitive substring match on descriptions
///     (like the LEX search in DGICD.m)
/// </summary>
public class Icd10IndexGrain : Grain, IIcd10IndexGrain
{
    private readonly IPersistentState<Icd10IndexState> _state;

    public Icd10IndexGrain(
        [PersistentState("icd10IndexState", "icd10Store")]
        IPersistentState<Icd10IndexState> state)
    {
        _state = state;
    }

    public async Task LoadCodesAsync(List<Icd10IndexEntry> entries)
    {
        _state.State.Codes.Clear();

        foreach (var entry in entries)
        {
            _state.State.Codes[entry.Code] = entry;
        }

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalCodes = entries.Count;
        _state.State.BillableCodes = entries.Count(e => e.IsBillable);

        await _state.WriteStateAsync();
    }

    public Task<Icd10IndexEntry?> GetCodeAsync(string code)
    {
        _state.State.Codes.TryGetValue(code.ToUpperInvariant(), out var entry);
        return Task.FromResult(entry);
    }

    public Task<List<Icd10IndexEntry>> SearchAsync(
        string searchText, bool billableOnly, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<Icd10IndexEntry>());

        var query = searchText.Trim();
        var upper = query.ToUpperInvariant();

        // If it looks like a code (starts with letter then digit), search by code prefix first
        var isCodeSearch = query.Length >= 1 && char.IsLetter(query[0]) &&
                           (query.Length < 2 || char.IsDigit(query[1]));

        IEnumerable<Icd10IndexEntry> results;

        if (isCodeSearch)
        {
            // Code prefix search — like ^ICD9 "B" cross-reference
            results = _state.State.Codes.Values
                .Where(e => e.Code.StartsWith(upper, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // Text search — like LEX search in DGICD.m
            results = _state.State.Codes.Values
                .Where(e => e.ShortDescription.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            e.LongDescription.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (billableOnly)
            results = results.Where(e => e.IsBillable);

        return Task.FromResult(results
            .OrderBy(e => e.OrderNumber)
            .Take(maxResults)
            .ToList());
    }

    public Task<List<Icd10IndexEntry>> GetByPrefixAsync(
        string codePrefix, bool billableOnly, int maxResults)
    {
        var upper = codePrefix.ToUpperInvariant();

        var results = _state.State.Codes.Values
            .Where(e => e.Code.StartsWith(upper, StringComparison.OrdinalIgnoreCase));

        if (billableOnly)
            results = results.Where(e => e.IsBillable);

        return Task.FromResult(results
            .OrderBy(e => e.OrderNumber)
            .Take(maxResults)
            .ToList());
    }

    public Task<Icd10IndexStatus> GetStatusAsync()
    {
        return Task.FromResult(new Icd10IndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalCodes = _state.State.TotalCodes,
            BillableCodes = _state.State.BillableCodes
        });
    }

    public async Task ClearAsync()
    {
        _state.State.Codes.Clear();
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        _state.State.TotalCodes = 0;
        _state.State.BillableCodes = 0;

        await _state.WriteStateAsync();
    }
}
