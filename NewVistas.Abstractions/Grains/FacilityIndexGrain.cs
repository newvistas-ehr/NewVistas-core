// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Index state for the facility singleton index grain.
/// </summary>
[GenerateSerializer]
public class FacilityIndexState
{
    [Id(0)]
    public List<FacilityIndexEntry> Entries { get; set; } = new();
}

/// <summary>
/// Implementation of <see cref="IFacilityIndexGrain"/>.
/// Singleton grain that maintains a searchable index of all facility records.
/// </summary>
public class FacilityIndexGrain : Grain, IFacilityIndexGrain
{
    private readonly IPersistentState<FacilityIndexState> _state;

    public FacilityIndexGrain(
        [PersistentState("engFacilityIndexState", "engFacilityIndexStore")]
        IPersistentState<FacilityIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(FacilityIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.FacilityId == entry.FacilityId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<FacilityIndexEntry>> SearchAsync(
        string? searchText,
        FacilityCategory? category,
        bool activeOnly,
        int maxResults)
    {
        IEnumerable<FacilityIndexEntry> query = _state.State.Entries;

        if (activeOnly)
            query = query.Where(e => e.Status != FacilityStatus.Decommissioned);

        if (category.HasValue)
            query = query.Where(e => e.Category == category.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string lower = searchText.ToLowerInvariant();
            query = query.Where(e =>
                e.FacilityName.ToLowerInvariant().Contains(lower) ||
                (e.Building != null && e.Building.ToLowerInvariant().Contains(lower)) ||
                (e.DepartmentName != null && e.DepartmentName.ToLowerInvariant().Contains(lower)) ||
                (e.Room != null && e.Room.ToLowerInvariant().Contains(lower)));
        }

        List<FacilityIndexEntry> results = query
            .OrderBy(e => e.FacilityName)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<FacilityIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries.OrderBy(e => e.FacilityName).ToList());
}
