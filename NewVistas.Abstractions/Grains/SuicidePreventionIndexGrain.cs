// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class SuicidePreventionIndexState
{
    [Id(0)] public List<PatientHighRiskSummary> Patients { get; set; } = new();
}

public class SuicidePreventionIndexGrain : Grain, ISuicidePreventionIndexGrain
{
    private readonly IPersistentState<SuicidePreventionIndexState> _state;

    public SuicidePreventionIndexGrain(
        [PersistentState("spIndexState", "spIndexStore")] IPersistentState<SuicidePreventionIndexState> state)
    {
        _state = state;
    }

    public Task<List<PatientHighRiskSummary>> GetAllPatientsAsync() =>
        Task.FromResult(_state.State.Patients
            .OrderByDescending(p => p.LastModifiedDate)
            .ToList());

    public Task<List<PatientHighRiskSummary>> GetHighRiskPatientsAsync() =>
        Task.FromResult(_state.State.Patients
            .Where(p => p.IsHighRiskFlagged)
            .OrderByDescending(p => p.LastModifiedDate)
            .ToList());

    public async Task UpsertPatientAsync(PatientHighRiskSummary summary)
    {
        int idx = _state.State.Patients.FindIndex(p => p.PatientId == summary.PatientId);
        if (idx >= 0)
            _state.State.Patients[idx] = summary;
        else
            _state.State.Patients.Add(summary);
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
