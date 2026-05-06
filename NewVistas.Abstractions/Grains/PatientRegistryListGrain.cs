// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class PatientRegistryListState
{
    [Id(0)] public List<PatientRegistryEnrollmentEntry> Enrollments { get; set; } = new();
}

public class PatientRegistryListGrain : Grain, IPatientRegistryListGrain
{
    private readonly IPersistentState<PatientRegistryListState> _state;

    public PatientRegistryListGrain(
        [PersistentState("ccrPatientState", "ccrPatientStore")] IPersistentState<PatientRegistryListState> state)
    {
        _state = state;
    }

    public Task<List<PatientRegistryEnrollmentEntry>> GetAllEnrollmentsAsync() =>
        Task.FromResult(_state.State.Enrollments.ToList());

    public Task<List<PatientRegistryEnrollmentEntry>> GetActiveEnrollmentsAsync() =>
        Task.FromResult(_state.State.Enrollments
            .Where(e => e.Status == CCREnrollmentStatus.Active)
            .ToList());

    public async Task UpsertEnrollmentAsync(PatientRegistryEnrollmentEntry entry)
    {
        int idx = _state.State.Enrollments.FindIndex(e => e.RegistryType == entry.RegistryType);
        if (idx >= 0)
            _state.State.Enrollments[idx] = entry;
        else
            _state.State.Enrollments.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveEnrollmentAsync(RegistryType registryType)
    {
        int idx = _state.State.Enrollments.FindIndex(e => e.RegistryType == registryType);
        if (idx >= 0)
        {
            _state.State.Enrollments.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
