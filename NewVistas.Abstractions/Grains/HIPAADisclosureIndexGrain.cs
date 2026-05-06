// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class HIPAADisclosureIndexState
{
    [Id(0)] public List<HIPAADisclosureIndexEntry> Disclosures { get; set; } = new();
}

public class HIPAADisclosureIndexGrain : Grain, IHIPAADisclosureIndexGrain
{
    private readonly IPersistentState<HIPAADisclosureIndexState> _state;

    public HIPAADisclosureIndexGrain(
        [PersistentState("roiDisclosureIndexState", "roiDisclosureIndexStore")] IPersistentState<HIPAADisclosureIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertDisclosureAsync(HIPAADisclosureIndexEntry entry)
    {
        HIPAADisclosureIndexEntry? existing = _state.State.Disclosures.Find(d => d.DisclosureId == entry.DisclosureId);
        if (existing is not null)
            _state.State.Disclosures.Remove(existing);
        _state.State.Disclosures.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<HIPAADisclosureIndexEntry>> GetAllDisclosuresAsync()
    {
        List<HIPAADisclosureIndexEntry> result = _state.State.Disclosures
            .OrderByDescending(d => d.DisclosureDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HIPAADisclosureIndexEntry>> GetDisclosuresSubjectToAccountingAsync()
    {
        List<HIPAADisclosureIndexEntry> result = _state.State.Disclosures
            .Where(d => d.IsSubjectToAccounting)
            .OrderByDescending(d => d.DisclosureDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<HIPAADisclosureIndexEntry>> GetDisclosuresByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        List<HIPAADisclosureIndexEntry> result = _state.State.Disclosures
            .Where(d => d.DisclosureDate >= startDate && d.DisclosureDate <= endDate)
            .OrderByDescending(d => d.DisclosureDate)
            .ToList();
        return Task.FromResult(result);
    }
}
