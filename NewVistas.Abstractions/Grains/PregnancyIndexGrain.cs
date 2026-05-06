// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PregnancyIndexGrain : Grain, IPregnancyIndexGrain
{
    private readonly IPersistentState<PregnancyIndexState> _state;

    public PregnancyIndexGrain(
        [PersistentState("pregnancyIndexState", "pregnancyIndexStore")]
        IPersistentState<PregnancyIndexState> state)
    {
        _state = state;
    }

    public Task<List<PregnancyIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PregnancyIndexEntry>> GetByStatusAsync(PregnancyStatus status)
        => Task.FromResult(_state.State.Entries.Where(e => e.Status == status).ToList());

    public Task<PregnancyIndexEntry?> GetActiveAsync()
        => Task.FromResult(_state.State.Entries.FirstOrDefault(e => e.Status == PregnancyStatus.Active));

    public async Task AddEntryAsync(PregnancyIndexEntry entry)
    {
        _state.State.Entries.Insert(0, entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryAsync(string pregnancyId, PregnancyStatus status,
        PregnancyOutcome outcome, PregnancyRiskLevel riskLevel)
    {
        PregnancyIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.PregnancyId == pregnancyId);
        if (entry != null)
        {
            entry.Status = status;
            entry.Outcome = outcome;
            entry.RiskLevel = riskLevel;
        }
        await _state.WriteStateAsync();
    }
}
