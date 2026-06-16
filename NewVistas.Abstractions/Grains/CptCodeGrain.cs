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
/// CPT Code Grain — stores a single CPT procedure code.
///
/// Grain Key: "CPT:{code}" (e.g., "CPT:99213").
/// </summary>
public class CptCodeGrain : Grain, ICptCodeGrain
{
    private readonly IPersistentState<CptCodeState> _state;

    public CptCodeGrain(
        [PersistentState("cptCodeState", "cptCodeStore")]
        IPersistentState<CptCodeState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Code))
        {
            // Extract code from grain key "CPT:{code}"
            string key = this.GetPrimaryKeyString();
            string code = key.StartsWith("CPT:") ? key[4..] : key;
            _state.State.Code = code;
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<CptCodeState> GetCodeAsync() =>
        Task.FromResult(_state.State);

    public async Task LoadCodeAsync(
        string code,
        string shortName,
        string longDescription,
        string category,
        decimal workRvu,
        decimal totalRvu,
        string status,
        DateTime? effectiveDate)
    {
        _state.State.Code = code;
        _state.State.ShortName = shortName;
        _state.State.LongDescription = longDescription;
        _state.State.Category = category;
        _state.State.WorkRvu = workRvu;
        _state.State.TotalRvu = totalRvu;
        _state.State.Status = status;
        _state.State.EffectiveDate = effectiveDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsActiveAsync() =>
        Task.FromResult(_state.State.Status == "ACTIVE");

    public async Task ActivateAsync()
    {
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.Status = "INACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
