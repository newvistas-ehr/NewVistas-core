// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// SCI Registry Index Grain — singleton listing all patients enrolled in the SCI/D registry.
/// Grain key: "SCI-INDEX"
/// </summary>
public class SCIIndexGrain : Grain, ISCIIndexGrain
{
    private readonly IPersistentState<SCIIndexState> _state;

    public SCIIndexGrain(
        [PersistentState("sciIndexState", "sciIndexStore")] IPersistentState<SCIIndexState> state)
    {
        _state = state;
    }

    public Task<List<SCIIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<SCIIndexEntry>> GetByStatusAsync(SCIRegistryStatus status)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.Status == status)
            .ToList());

    public Task<List<SCIIndexEntry>> GetByNeurologicalLevelAsync(string levelPrefix)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.NeurologicalLevel.StartsWith(levelPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public async Task AddEntryAsync(SCIIndexEntry entry)
    {
        // Idempotent: skip if already enrolled
        if (_state.State.Entries.Any(e => e.PatientId == entry.PatientId))
            return;

        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateEntryAsync(
        string patientId,
        SCIRegistryStatus status,
        string neurologicalLevel,
        SCIAisGrade aisGrade)
    {
        SCIIndexEntry? entry = _state.State.Entries.FirstOrDefault(e => e.PatientId == patientId);
        if (entry is null) return;

        entry.Status = status;
        entry.NeurologicalLevel = neurologicalLevel;
        entry.AisGrade = aisGrade;
        await _state.WriteStateAsync();
    }
}
