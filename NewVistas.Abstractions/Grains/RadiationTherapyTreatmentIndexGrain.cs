// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class RtTreatmentIndexState
{
    [Id(0)] public List<RtTreatmentIndexEntry> Treatments { get; set; } = new();
}

public class RadiationTherapyTreatmentIndexGrain : Grain, IRadiationTherapyTreatmentIndexGrain
{
    private readonly IPersistentState<RtTreatmentIndexState> _state;

    public RadiationTherapyTreatmentIndexGrain(
        [PersistentState("rtTreatmentIndexState", "rtTreatmentIndexStore")] IPersistentState<RtTreatmentIndexState> state)
    {
        _state = state;
    }

    public Task<List<RtTreatmentIndexEntry>> GetAllTreatmentsAsync() =>
        Task.FromResult(_state.State.Treatments
            .OrderBy(t => t.FractionNumber)
            .ToList());

    public Task<List<RtTreatmentIndexEntry>> GetDeliveredTreatmentsAsync() =>
        Task.FromResult(_state.State.Treatments
            .Where(t => t.Status == RtFractionStatus.Delivered)
            .OrderBy(t => t.FractionNumber)
            .ToList());

    public Task<int> GetTotalDeliveredDoseCgyAsync() =>
        Task.FromResult(_state.State.Treatments
            .Where(t => t.Status == RtFractionStatus.Delivered)
            .Sum(t => t.DoseDeliveredCgy));

    public Task<int> GetDeliveredFractionCountAsync() =>
        Task.FromResult(_state.State.Treatments
            .Count(t => t.Status == RtFractionStatus.Delivered));

    public async Task UpsertTreatmentAsync(RtTreatmentIndexEntry entry)
    {
        int idx = _state.State.Treatments.FindIndex(t => t.TreatmentId == entry.TreatmentId);
        if (idx >= 0)
            _state.State.Treatments[idx] = entry;
        else
            _state.State.Treatments.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveTreatmentAsync(string treatmentId)
    {
        int idx = _state.State.Treatments.FindIndex(t => t.TreatmentId == treatmentId);
        if (idx >= 0)
        {
            _state.State.Treatments.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
