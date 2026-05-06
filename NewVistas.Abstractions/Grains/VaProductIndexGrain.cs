// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// VA Product Index Grain — in-memory searchable catalog of all VA Products.
/// Singleton grain keyed "NDF-PRODUCT-INDEX".
/// </summary>
public class VaProductIndexGrain : Grain, IVaProductIndexGrain
{
    private readonly IPersistentState<VaProductIndexState> _state;

    public VaProductIndexGrain(
        [PersistentState("ndfProductIndexState", "ndfProductIndexStore")] IPersistentState<VaProductIndexState> state)
    {
        _state = state;
    }

    public async Task LoadProductsAsync(List<VaProductIndexEntry> entries)
    {
        _state.State.ProductsByIen.Clear();
        _state.State.IenByNdc.Clear();
        _state.State.IensByGenericIen.Clear();

        foreach (VaProductIndexEntry entry in entries)
        {
            _state.State.ProductsByIen[entry.Ien] = entry;

            foreach (string ndc in entry.NdcCodes)
            {
                if (!string.IsNullOrEmpty(ndc))
                    _state.State.IenByNdc[ndc] = entry.Ien;
            }

            if (!string.IsNullOrEmpty(entry.VaGenericIen))
            {
                if (!_state.State.IensByGenericIen.TryGetValue(entry.VaGenericIen, out List<string>? iens))
                {
                    iens = new List<string>();
                    _state.State.IensByGenericIen[entry.VaGenericIen] = iens;
                }
                if (!iens.Contains(entry.Ien))
                    iens.Add(entry.Ien);
            }
        }

        _state.State.TotalProducts = entries.Count;
        _state.State.FormularyProducts = entries.Count(e => e.FormularyIndicator);
        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<VaProductIndexEntry?> GetProductAsync(string ien)
    {
        _state.State.ProductsByIen.TryGetValue(ien, out VaProductIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<VaProductIndexEntry?> GetByNdcAsync(string ndcCode)
    {
        if (_state.State.IenByNdc.TryGetValue(ndcCode, out string? ien))
        {
            _state.State.ProductsByIen.TryGetValue(ien, out VaProductIndexEntry? entry);
            return Task.FromResult(entry);
        }
        return Task.FromResult<VaProductIndexEntry?>(null);
    }

    public Task<List<VaProductIndexEntry>> GetByGenericAsync(string vaGenericIen, int maxResults = 100)
    {
        if (!_state.State.IensByGenericIen.TryGetValue(vaGenericIen, out List<string>? iens))
            return Task.FromResult(new List<VaProductIndexEntry>());

        List<VaProductIndexEntry> results = iens
            .Take(maxResults)
            .Select(ien => _state.State.ProductsByIen.TryGetValue(ien, out VaProductIndexEntry? e) ? e : null)
            .Where(e => e != null)
            .Cast<VaProductIndexEntry>()
            .OrderBy(e => e.Name)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<VaProductIndexEntry>> GetByDrugClassAsync(string drugClassCode, bool activeOnly = true, int maxResults = 200)
    {
        List<VaProductIndexEntry> results = _state.State.ProductsByIen.Values
            .Where(e => e.PrimaryDrugClassCode == drugClassCode)
            .Where(e => !activeOnly || e.IsActive)
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<VaProductIndexEntry>> SearchAsync(
        string searchText,
        bool formularyOnly,
        string? drugClassCode,
        bool activeOnly,
        int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<VaProductIndexEntry>());

        string normalizedSearch = searchText.Trim().ToUpperInvariant();

        // If it looks like an NDC (digits and hyphens), try NDC lookup first
        bool isNdcLike = normalizedSearch.Length >= 5 &&
            normalizedSearch.All(c => char.IsDigit(c) || c == '-');

        IEnumerable<VaProductIndexEntry> query = _state.State.ProductsByIen.Values;

        if (isNdcLike)
        {
            if (_state.State.IenByNdc.TryGetValue(normalizedSearch, out string? ienByNdc))
            {
                if (_state.State.ProductsByIen.TryGetValue(ienByNdc, out VaProductIndexEntry? ndcMatch))
                    return Task.FromResult(new List<VaProductIndexEntry> { ndcMatch });
            }
        }

        // Name-based search: match product name or generic name
        query = query.Where(e =>
            e.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
            e.VaGenericName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
            e.NdcCodes.Any(ndc => ndc.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)));

        if (formularyOnly)
            query = query.Where(e => e.FormularyIndicator);

        if (!string.IsNullOrEmpty(drugClassCode))
            query = query.Where(e => e.PrimaryDrugClassCode == drugClassCode);

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        List<VaProductIndexEntry> results = query
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<NdfProductIndexStatus> GetStatusAsync() =>
        Task.FromResult(new NdfProductIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalProducts = _state.State.TotalProducts,
            FormularyProducts = _state.State.FormularyProducts,
            ActiveProducts = _state.State.ProductsByIen.Values.Count(e => e.IsActive)
        });

    public async Task ClearAsync()
    {
        _state.State.ProductsByIen.Clear();
        _state.State.IenByNdc.Clear();
        _state.State.IensByGenericIen.Clear();
        _state.State.TotalProducts = 0;
        _state.State.FormularyProducts = 0;
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        await _state.WriteStateAsync();
    }
}
