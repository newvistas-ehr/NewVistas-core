// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class HAICaseIndexState
{
    [Id(0)] public List<HAICaseSummary> Cases { get; set; } = new();
}

public class HaiCaseIndexGrain : Grain, IHAICaseIndexGrain
{
    private readonly IPersistentState<HAICaseIndexState> _state;

    public HaiCaseIndexGrain(
        [PersistentState("haiCaseIndexState", "haiCaseIndexStore")] IPersistentState<HAICaseIndexState> state)
    {
        _state = state;
    }

    public Task<List<HAICaseSummary>> GetAllCasesAsync() =>
        Task.FromResult(_state.State.Cases
            .OrderByDescending(c => c.InfectionDate)
            .ToList());

    public Task<List<HAICaseSummary>> GetActiveAsync() =>
        Task.FromResult(_state.State.Cases
            .Where(c => c.Status == HAICaseStatus.Suspected || c.Status == HAICaseStatus.Confirmed)
            .OrderByDescending(c => c.InfectionDate)
            .ToList());

    public Task<List<HAICaseSummary>> GetByTypeAsync(HAIType haiType) =>
        Task.FromResult(_state.State.Cases
            .Where(c => c.HAIType == haiType)
            .OrderByDescending(c => c.InfectionDate)
            .ToList());

    public Task<List<HAICaseSummary>> GetByLocationAsync(string locationId) =>
        Task.FromResult(_state.State.Cases
            .Where(c => c.LocationId == locationId)
            .OrderByDescending(c => c.InfectionDate)
            .ToList());

    public Task<List<HAICaseSummary>> GetByOutbreakAsync(string outbreakId) =>
        Task.FromResult(_state.State.Cases
            .Where(c => c.OutbreakId == outbreakId)
            .OrderByDescending(c => c.InfectionDate)
            .ToList());

    public async Task UpsertCaseAsync(HAICaseSummary summary)
    {
        int idx = _state.State.Cases.FindIndex(c => c.CaseId == summary.CaseId);
        if (idx >= 0)
            _state.State.Cases[idx] = summary;
        else
            _state.State.Cases.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveCaseAsync(string caseId)
    {
        int idx = _state.State.Cases.FindIndex(c => c.CaseId == caseId);
        if (idx >= 0)
        {
            _state.State.Cases.RemoveAt(idx);
            await _state.WriteStateAsync();
        }
    }
}
