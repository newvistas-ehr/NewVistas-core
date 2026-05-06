// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Admission Index Grain — grain key: "BR-ADMIT-IDX:{patientId}"
/// </summary>
public class BRAdmissionIndexGrain : Grain, IBRAdmissionIndexGrain
{
    private readonly IPersistentState<BRAdmissionIndexState> _state;

    public BRAdmissionIndexGrain(
        [PersistentState("brAdmissionIndexState", "brAdmissionIndexStore")]
        IPersistentState<BRAdmissionIndexState> state)
    {
        _state = state;
    }

    public Task<List<BRAdmissionIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Admissions);

    public Task<List<BRAdmissionIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Admissions
            .Where(a => a.Status == BRAdmissionStatus.Active || a.Status == BRAdmissionStatus.Accepted)
            .ToList());

    public async Task AddAsync(BRAdmissionIndexEntry entry)
    {
        _state.State.Admissions.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string admitId, BRAdmissionStatus status)
    {
        BRAdmissionIndexEntry? entry = _state.State.Admissions.FirstOrDefault(a => a.AdmitId == admitId);
        if (entry is not null)
        {
            entry.Status = status;
            await _state.WriteStateAsync();
        }
    }
}
