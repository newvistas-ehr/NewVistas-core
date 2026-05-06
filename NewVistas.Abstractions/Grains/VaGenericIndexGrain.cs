// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// VA Generic Index Grain — in-memory catalog of all VA Generic drug names.
/// Singleton grain keyed "NDF-GENERIC-INDEX".
/// </summary>
public class VaGenericIndexGrain : Grain, IVaGenericIndexGrain
{
    private readonly IPersistentState<VaGenericIndexState> _state;

    public VaGenericIndexGrain(
        [PersistentState("ndfGenericIndexState", "ndfGenericIndexStore")] IPersistentState<VaGenericIndexState> state)
    {
        _state = state;
    }

    public async Task LoadGenericsAsync(List<VaGenericEntry> entries)
    {
        _state.State.GenericsByIen.Clear();

        foreach (VaGenericEntry entry in entries)
            _state.State.GenericsByIen[entry.Ien] = entry;

        _state.State.TotalGenerics = entries.Count;
        _state.State.ActiveGenerics = entries.Count(e => e.IsActive);
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<VaGenericEntry?> GetGenericAsync(string ien)
    {
        _state.State.GenericsByIen.TryGetValue(ien, out VaGenericEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<VaGenericEntry>> SearchAsync(string searchText, bool activeOnly, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<VaGenericEntry>());

        IEnumerable<VaGenericEntry> query = _state.State.GenericsByIen.Values
            .Where(e => e.Name.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase));

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        List<VaGenericEntry> results = query
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<NdfGenericIndexStatus> GetStatusAsync() =>
        Task.FromResult(new NdfGenericIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalGenerics = _state.State.TotalGenerics,
            ActiveGenerics = _state.State.ActiveGenerics
        });

    public async Task ClearAsync()
    {
        _state.State.GenericsByIen.Clear();
        _state.State.TotalGenerics = 0;
        _state.State.ActiveGenerics = 0;
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        await _state.WriteStateAsync();
    }
}
