// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ClinicalRegistryIndexState
{
    [Id(0)] public List<CCREntrySummary> Entries { get; set; } = new();
}

public class ClinicalRegistryIndexGrain : Grain, IClinicalRegistryIndexGrain
{
    private readonly IPersistentState<ClinicalRegistryIndexState> _state;

    public ClinicalRegistryIndexGrain(
        [PersistentState("ccrIndexState", "ccrIndexStore")] IPersistentState<ClinicalRegistryIndexState> state)
    {
        _state = state;
    }

    public Task<List<CCREntrySummary>> GetAllEntriesAsync() =>
        Task.FromResult(_state.State.Entries
            .OrderByDescending(e => e.EnrollmentDate)
            .ToList());

    public Task<List<CCREntrySummary>> GetActiveEntriesAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == CCREnrollmentStatus.Active)
            .OrderByDescending(e => e.EnrollmentDate)
            .ToList());

    public Task<List<CCREntrySummary>> GetByStatusAsync(CCREnrollmentStatus status) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.EnrollmentDate)
            .ToList());

    public async Task UpsertEntryAsync(CCREntrySummary entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.PatientId == entry.PatientId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string patientId)
    {
        int idx = _state.State.Entries.FindIndex(e => e.PatientId == patientId);
        if (idx >= 0)
        {
            _state.State.Entries.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
