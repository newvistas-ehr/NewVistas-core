// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class OncologyTreatmentIndexGrain : Grain, IOncologyTreatmentIndexGrain
{
    private readonly IPersistentState<OncologyTreatmentIndexState> _state;

    public OncologyTreatmentIndexGrain(
        [PersistentState("oncTreatmentIndexState", "oncTreatmentIndexStore")] IPersistentState<OncologyTreatmentIndexState> state)
    {
        _state = state;
    }

    public Task<List<OncologyTreatmentIndexEntry>> GetAllTreatmentsAsync() =>
        Task.FromResult(
            _state.State.Treatments
                .OrderByDescending(t => t.StartDate)
                .ToList());

    public Task<List<OncologyTreatmentIndexEntry>> GetTreatmentsByTumorAsync(string tumorId) =>
        Task.FromResult(
            _state.State.Treatments
                .Where(t => t.TumorId == tumorId)
                .OrderByDescending(t => t.StartDate)
                .ToList());

    public async Task UpsertTreatmentAsync(OncologyTreatmentIndexEntry entry)
    {
        int idx = _state.State.Treatments.FindIndex(t => t.TreatmentId == entry.TreatmentId);
        if (idx >= 0)
            _state.State.Treatments[idx] = entry;
        else
            _state.State.Treatments.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTreatmentAsync(string treatmentId)
    {
        int idx = _state.State.Treatments.FindIndex(t => t.TreatmentId == treatmentId);
        if (idx < 0) return;
        _state.State.Treatments.RemoveAt(idx);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
