// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class TransplantWaitlistIndexState
{
    [Id(0)] public List<TransplantWaitlistEntry> Patients { get; set; } = new();
}

public class TransplantWaitlistIndexGrain : Grain, ITransplantWaitlistIndexGrain
{
    private readonly IPersistentState<TransplantWaitlistIndexState> _state;

    public TransplantWaitlistIndexGrain(
        [PersistentState("txWaitlistState", "txWaitlistStore")] IPersistentState<TransplantWaitlistIndexState> state)
    {
        _state = state;
    }

    public Task<List<TransplantWaitlistEntry>> GetAllPatientsAsync() =>
        Task.FromResult(_state.State.Patients
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.ListedDate)
            .ToList());

    public Task<List<TransplantWaitlistEntry>> GetPatientsByOrganAsync(TransplantOrganType organType) =>
        Task.FromResult(_state.State.Patients
            .Where(p => p.OrganType == organType)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.ListedDate)
            .ToList());

    public Task<List<TransplantWaitlistEntry>> GetPatientsByStatusAsync(TransplantStatus status) =>
        Task.FromResult(_state.State.Patients
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.ListedDate)
            .ToList());

    public Task<List<TransplantWaitlistEntry>> GetActiveWaitlistAsync() =>
        Task.FromResult(_state.State.Patients
            .Where(p => p.Status == TransplantStatus.Listed)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.ListedDate)
            .ToList());

    public async Task UpsertPatientAsync(TransplantWaitlistEntry entry)
    {
        int idx = _state.State.Patients.FindIndex(p => p.PatientId == entry.PatientId);
        if (idx >= 0)
            _state.State.Patients[idx] = entry;
        else
            _state.State.Patients.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemovePatientAsync(string patientId)
    {
        int idx = _state.State.Patients.FindIndex(p => p.PatientId == patientId);
        if (idx >= 0)
        {
            _state.State.Patients.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
