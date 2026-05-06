// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class MedProcedureIndexState
{
    [Id(0)] public List<MedProcedureIndexEntry> Procedures { get; set; } = new();
}

public class MedProcedureIndexGrain : Grain, IMedProcedureIndexGrain
{
    private readonly IPersistentState<MedProcedureIndexState> _state;

    public MedProcedureIndexGrain(
        [PersistentState("medProcedureIndexState", "medProcedureIndexStore")] IPersistentState<MedProcedureIndexState> state)
    {
        _state = state;
    }

    public Task<List<MedProcedureIndexEntry>> GetAllProceduresAsync() =>
        Task.FromResult(_state.State.Procedures
            .OrderByDescending(p => p.OrderedDate)
            .ToList());

    public Task<List<MedProcedureIndexEntry>> GetProceduresByCategoryAsync(MedProcedureCategory category) =>
        Task.FromResult(_state.State.Procedures
            .Where(p => p.Category == category)
            .OrderByDescending(p => p.OrderedDate)
            .ToList());

    public Task<List<MedProcedureIndexEntry>> GetCompletedProceduresAsync() =>
        Task.FromResult(_state.State.Procedures
            .Where(p => p.Status == MedProcedureStatus.Completed)
            .OrderByDescending(p => p.PerformedDate ?? p.OrderedDate)
            .ToList());

    public async Task UpsertProcedureAsync(MedProcedureIndexEntry entry)
    {
        int idx = _state.State.Procedures.FindIndex(p => p.ProcedureId == entry.ProcedureId);
        if (idx >= 0)
            _state.State.Procedures[idx] = entry;
        else
            _state.State.Procedures.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveProcedureAsync(string procedureId)
    {
        int idx = _state.State.Procedures.FindIndex(p => p.ProcedureId == procedureId);
        if (idx >= 0)
        {
            _state.State.Procedures.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
