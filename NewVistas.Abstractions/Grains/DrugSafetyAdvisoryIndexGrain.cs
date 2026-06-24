// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index of all drug safety advisories. Grain key: "DSA-INDEX".
/// </summary>
public class DrugSafetyAdvisoryIndexGrain : Grain, IDrugSafetyAdvisoryIndexGrain
{
    private readonly IPersistentState<DrugSafetyAdvisoryIndexState> _state;

    public DrugSafetyAdvisoryIndexGrain(
        [PersistentState("drugSafetyAdvisoryIndexState", "drugSafetyAdvisoryIndexStore")]
        IPersistentState<DrugSafetyAdvisoryIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertAsync(DrugSafetyAdvisorySummary summary)
    {
        _state.State.ById[summary.AdvisoryId] = summary;
        await _state.WriteStateAsync();
    }

    public Task<List<DrugSafetyAdvisorySummary>> GetActiveAsync() =>
        Task.FromResult(_state.State.ById.Values
            .Where(s => s.Status == AdvisoryStatus.Active)
            .OrderByDescending(s => s.SourcePublishedDate)
            .ToList());

    public Task<List<DrugSafetyAdvisorySummary>> GetAllAsync() =>
        Task.FromResult(_state.State.ById.Values
            .OrderByDescending(s => s.SourcePublishedDate)
            .ToList());

    public Task<List<DrugSafetyAdvisorySummary>> GetActiveByDrugClassAsync(string drugClassCode) =>
        Task.FromResult(_state.State.ById.Values
            .Where(s => s.Status == AdvisoryStatus.Active
                     && s.TargetDrugClassCodes.Contains(drugClassCode, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(s => s.SourcePublishedDate)
            .ToList());
}
