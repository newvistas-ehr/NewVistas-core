// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ClinicalRegistrySiteIndexState
{
    [Id(0)] public List<CCREntrySummary> Entries { get; set; } = new();
}

public class ClinicalRegistrySiteIndexGrain : Grain, IClinicalRegistrySiteIndexGrain
{
    private readonly IPersistentState<ClinicalRegistrySiteIndexState> _state;

    public ClinicalRegistrySiteIndexGrain(
        [PersistentState("ccrSiteIndexState", "ccrSiteIndexStore")] IPersistentState<ClinicalRegistrySiteIndexState> state)
    {
        _state = state;
    }

    public Task<List<CCREntrySummary>> GetAllEntriesAsync() =>
        Task.FromResult(_state.State.Entries
            .OrderByDescending(e => e.EnrollmentDate)
            .ToList());

    public Task<List<CCREntrySummary>> GetRecentEnrollmentsAsync(int count) =>
        Task.FromResult(_state.State.Entries
            .OrderByDescending(e => e.EnrollmentDate)
            .Take(count)
            .ToList());

    public async Task UpsertEntryAsync(CCREntrySummary entry)
    {
        int idx = _state.State.Entries.FindIndex(e =>
            e.PatientId == entry.PatientId && e.RegistryType == entry.RegistryType);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string patientId, RegistryType registryType)
    {
        int idx = _state.State.Entries.FindIndex(e =>
            e.PatientId == patientId && e.RegistryType == registryType);
        if (idx >= 0)
        {
            _state.State.Entries.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
