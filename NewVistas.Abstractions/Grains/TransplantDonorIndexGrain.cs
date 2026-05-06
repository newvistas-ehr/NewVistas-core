// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class TransplantDonorIndexState
{
    [Id(0)] public List<TransplantDonorSummaryEntry> Donors { get; set; } = new();
}

public class TransplantDonorIndexGrain : Grain, ITransplantDonorIndexGrain
{
    private readonly IPersistentState<TransplantDonorIndexState> _state;

    public TransplantDonorIndexGrain(
        [PersistentState("txDonorIndexState", "txDonorIndexStore")] IPersistentState<TransplantDonorIndexState> state)
    {
        _state = state;
    }

    public Task<List<TransplantDonorSummaryEntry>> GetAllDonorsAsync() =>
        Task.FromResult(_state.State.Donors
            .OrderByDescending(d => d.RecoveryDateTime)
            .ToList());

    public Task<List<TransplantDonorSummaryEntry>> GetDonorsByOrganAsync(TransplantOrganType organType) =>
        Task.FromResult(_state.State.Donors
            .Where(d => d.OrganType == organType)
            .OrderByDescending(d => d.RecoveryDateTime)
            .ToList());

    public Task<List<TransplantDonorSummaryEntry>> GetDonorsByStatusAsync(DonorStatus status) =>
        Task.FromResult(_state.State.Donors
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.RecoveryDateTime)
            .ToList());

    public Task<List<TransplantDonorSummaryEntry>> GetAvailableDonorsAsync() =>
        Task.FromResult(_state.State.Donors
            .Where(d => d.Status == DonorStatus.Available)
            .OrderByDescending(d => d.RecoveryDateTime)
            .ToList());

    public async Task UpsertDonorAsync(TransplantDonorSummaryEntry entry)
    {
        int idx = _state.State.Donors.FindIndex(d => d.DonorId == entry.DonorId);
        if (idx >= 0)
            _state.State.Donors[idx] = entry;
        else
            _state.State.Donors.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveDonorAsync(string donorId)
    {
        int idx = _state.State.Donors.FindIndex(d => d.DonorId == donorId);
        if (idx >= 0)
        {
            _state.State.Donors.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
