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
/// DSS Unit Grain — manages a single Decision Support System unit definition.
/// Persists to "dssUnitStore".
/// </summary>
public class DssUnitGrain : Grain, IDssUnitGrain
{
    private readonly IPersistentState<DssUnitState> _state;

    public DssUnitGrain(
        [PersistentState("dssUnitState", "dssUnitStore")]
        IPersistentState<DssUnitState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.DssUnitId))
        {
            _state.State.DssUnitId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<DssUnitState> GetUnitAsync() => Task.FromResult(_state.State);

    public async Task UpsertAsync(
        string unitName,
        string unitCode,
        string? divisionId,
        string? divisionName,
        string? primaryStopCode,
        string? creditStopCode,
        string? treatmentCode,
        string? description,
        bool isActive)
    {
        _state.State.UnitName = unitName;
        _state.State.UnitCode = unitCode;
        _state.State.DivisionId = divisionId;
        _state.State.DivisionName = divisionName;
        _state.State.PrimaryStopCode = primaryStopCode;
        _state.State.CreditStopCode = creditStopCode;
        _state.State.TreatmentCode = treatmentCode;
        _state.State.Description = description;
        _state.State.IsActive = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
