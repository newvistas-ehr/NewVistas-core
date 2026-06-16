// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient-level index grain for Anatomic Pathology cases.
/// Grain key pattern: "AP-CASE-IDX:{patientId}"
/// </summary>
public class AnatomicPathologyCaseIndexGrain : Grain, IAnatomicPathologyCaseIndexGrain
{
    private readonly IPersistentState<AnatomicPathologyCaseIndexState> _state;

    public AnatomicPathologyCaseIndexGrain(
        [PersistentState("apCaseIndexState", "apCaseIndexStore")] IPersistentState<AnatomicPathologyCaseIndexState> state)
    {
        _state = state;
    }

    public Task<List<APCaseIndexEntry>> GetAllCasesAsync() =>
        Task.FromResult(_state.State.Cases.OrderByDescending(c => c.DateReceived).ToList());

    public Task<List<APCaseIndexEntry>> GetCasesByTypeAsync(APCaseType caseType) =>
        Task.FromResult(_state.State.Cases
            .Where(c => c.CaseType == caseType)
            .OrderByDescending(c => c.DateReceived)
            .ToList());

    public async Task UpsertCaseAsync(APCaseIndexEntry entry)
    {
        int idx = _state.State.Cases.FindIndex(c => c.CaseId == entry.CaseId);
        if (idx >= 0)
            _state.State.Cases[idx] = entry;
        else
            _state.State.Cases.Add(entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveCaseAsync(string caseId)
    {
        _state.State.Cases.RemoveAll(c => c.CaseId == caseId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
