// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index grain for the Pharmacy Orderable Item catalog (VistA File #50.7).
/// Grain key: "OI-INDEX"
/// </summary>
public class OrderableItemIndexGrain : Grain, IOrderableItemIndexGrain
{
    private readonly IPersistentState<OrderableItemIndexState> _state;

    public OrderableItemIndexGrain(
        [PersistentState("oiIndexState", "orderableItemIndexStore")]
        IPersistentState<OrderableItemIndexState> state)
    {
        _state = state;
    }

    public async Task LoadItemsAsync(List<OrderableItemIndexEntry> entries)
    {
        _state.State.ByIen.Clear();

        foreach (OrderableItemIndexEntry entry in entries)
            _state.State.ByIen[entry.Ien] = entry;

        _state.State.TotalItems = entries.Count;
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<OrderableItemIndexEntry?> GetItemAsync(string ien)
    {
        _state.State.ByIen.TryGetValue(ien, out OrderableItemIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<OrderableItemIndexEntry>> SearchAsync(
        string searchText,
        PharmacyType? type = null,
        bool activeOnly = true,
        int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<OrderableItemIndexEntry>());

        string normalized = searchText.Trim().ToUpperInvariant();

        IEnumerable<OrderableItemIndexEntry> query = _state.State.ByIen.Values
            .Where(e => e.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                     || e.VaGenericName.Contains(normalized, StringComparison.OrdinalIgnoreCase));

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        if (type.HasValue)
            query = query.Where(e => e.PharmacyType == type.Value || e.PharmacyType == PharmacyType.All);

        List<OrderableItemIndexEntry> results = query
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<OrderableItemIndexEntry>> GetByVaGenericAsync(string vaGenericIen)
    {
        List<OrderableItemIndexEntry> results = _state.State.ByIen.Values
            .Where(e => e.VaGenericIen == vaGenericIen)
            .OrderBy(e => e.Name)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<OrderableItemIndexStatus> GetStatusAsync() =>
        Task.FromResult(new OrderableItemIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalItems = _state.State.TotalItems
        });

    public async Task ClearAsync()
    {
        _state.State.ByIen.Clear();
        _state.State.TotalItems = 0;
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        await _state.WriteStateAsync();
    }
}
