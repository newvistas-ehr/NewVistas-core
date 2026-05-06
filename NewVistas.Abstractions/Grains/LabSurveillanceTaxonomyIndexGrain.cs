// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class LabSurveillanceTaxonomyIndexGrain : Grain, ILabSurveillanceTaxonomyIndexGrain
{
    private readonly IPersistentState<LabSurveillanceTaxonomyIndexState> _state;

    public LabSurveillanceTaxonomyIndexGrain(
        [PersistentState("labSurvTaxIndexState", "labSurveillanceTaxonomyIndexStore")]
        IPersistentState<LabSurveillanceTaxonomyIndexState> state)
    {
        _state = state;
    }

    public Task<List<LabSurveillanceTaxonomyIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<LabSurveillanceTaxonomyIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries.Where(e => e.IsActive).ToList());

    public async Task UpsertAsync(LabSurveillanceTaxonomyIndexEntry entry)
    {
        LabSurveillanceTaxonomyIndexEntry? existing = _state.State.Entries
            .FirstOrDefault(e => e.TaxonomyId == entry.TaxonomyId);
        if (existing != null)
        {
            existing.TaxonomyName = entry.TaxonomyName;
            existing.ConditionName = entry.ConditionName;
            existing.Category = entry.Category;
            existing.CodeCount = entry.CodeCount;
            existing.IsActive = entry.IsActive;
        }
        else
        {
            _state.State.Entries.Add(entry);
        }
        await _state.WriteStateAsync();
    }
}
