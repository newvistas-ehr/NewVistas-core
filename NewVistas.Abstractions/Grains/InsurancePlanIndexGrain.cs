// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class InsurancePlanIndexGrain : Grain, IInsurancePlanIndexGrain
{
    private readonly IPersistentState<InsurancePlanIndexState> _state;

    public InsurancePlanIndexGrain(
        [PersistentState("insurancePlanIndexState", "insurancePlanIndexStore")]
        IPersistentState<InsurancePlanIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(InsurancePlanIndexEntry entry)
    {
        List<InsurancePlanIndexEntry> entries = _state.State.Entries;
        int idx = entries.FindIndex(e => e.PlanId == entry.PlanId);
        if (idx >= 0)
            entries[idx] = entry;
        else
            entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public Task<List<InsurancePlanIndexEntry>> SearchAsync(
        string searchText,
        string? planType,
        bool activeOnly,
        int maxResults = 50)
    {
        IEnumerable<InsurancePlanIndexEntry> query = _state.State.Entries;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string lower = searchText.ToLowerInvariant();
            query = query.Where(e =>
                e.GroupPlanName.ToLowerInvariant().Contains(lower) ||
                e.InsuranceCompanyName.ToLowerInvariant().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(planType))
            query = query.Where(e => e.PlanType == planType);

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        return Task.FromResult(query
            .OrderBy(e => e.GroupPlanName)
            .Take(maxResults)
            .ToList());
    }

    public Task<List<InsurancePlanIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries
            .OrderBy(e => e.GroupPlanName)
            .ToList());
}
