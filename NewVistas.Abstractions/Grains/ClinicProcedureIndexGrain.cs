// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ClinicProcedureIndexState
{
    [Id(0)] public List<ClinicProcedureIndexEntry> Procedures { get; set; } = new();
}

public class ClinicProcedureIndexGrain : Grain, IClinicProcedureIndexGrain
{
    private readonly IPersistentState<ClinicProcedureIndexState> _state;

    public ClinicProcedureIndexGrain(
        [PersistentState("cpProcedureIndexState", "cpProcedureIndexStore")] IPersistentState<ClinicProcedureIndexState> state)
    {
        _state = state;
    }

    public Task<List<ClinicProcedureIndexEntry>> GetAllProceduresAsync() =>
        Task.FromResult(_state.State.Procedures
            .OrderByDescending(p => p.OrderedDate)
            .ToList());

    public Task<List<ClinicProcedureIndexEntry>> GetProceduresByCategoryAsync(ClinicProcedureCategory category) =>
        Task.FromResult(_state.State.Procedures
            .Where(p => p.Category == category)
            .OrderByDescending(p => p.OrderedDate)
            .ToList());

    public Task<List<ClinicProcedureIndexEntry>> GetCompletedProceduresAsync() =>
        Task.FromResult(_state.State.Procedures
            .Where(p => p.Status == ClinicProcedureStatus.Completed)
            .OrderByDescending(p => p.PerformedDate ?? p.OrderedDate)
            .ToList());

    public async Task UpsertProcedureAsync(ClinicProcedureIndexEntry entry)
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
