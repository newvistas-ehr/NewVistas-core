// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PharmacyPosClaimIndexGrain : Grain, IPharmacyPosClaimIndexGrain
{
    private readonly IPersistentState<PosClaimIndexState> _state;

    public PharmacyPosClaimIndexGrain(
        [PersistentState("posClaimIndexState", "posClaimIndexStore")]
        IPersistentState<PosClaimIndexState> state)
    {
        _state = state;
    }

    public Task<List<PosClaimIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PosClaimIndexEntry>> GetByStatusAsync(PosClaimStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public async Task AddEntryAsync(PosClaimIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryStatusAsync(string claimId, PosClaimStatus status,
        decimal? insurancePaidAmount, decimal? patientResponsibility)
    {
        PosClaimIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.ClaimId == claimId);
        if (entry != null)
        {
            entry.Status = status;
            entry.InsurancePaidAmount = insurancePaidAmount;
            entry.PatientResponsibility = patientResponsibility;
        }
        await _state.WriteStateAsync();
    }
}
