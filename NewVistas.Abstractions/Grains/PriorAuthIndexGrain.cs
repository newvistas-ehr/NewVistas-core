// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient index of prior-authorization requests.
/// Grain key: "PA-INDEX:{patientId}"
/// </summary>
public class PriorAuthIndexGrain : Grain, IPriorAuthIndexGrain
{
    private readonly IPersistentState<PriorAuthIndexState> _state;

    public PriorAuthIndexGrain(
        [PersistentState("priorAuthIndexState", "priorAuthIndexStore")]
        IPersistentState<PriorAuthIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PriorAuthIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.PaId == entry.PaId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string paId)
    {
        _state.State.Entries.RemoveAll(e => e.PaId == paId);
        await _state.WriteStateAsync();
    }

    public Task<List<PriorAuthIndexEntry>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries
            .OrderByDescending(e => e.RequestDate)
            .ToList());

    public Task<List<PriorAuthIndexEntry>> GetPendingAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == "PENDING")
            .OrderByDescending(e => e.RequestDate)
            .ToList());

    public Task<List<PriorAuthIndexEntry>> GetApprovedAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == "APPROVED")
            .OrderByDescending(e => e.RequestDate)
            .ToList());

    public async Task ClearAsync()
    {
        _state.State.Entries.Clear();
        await _state.WriteStateAsync();
    }
}
