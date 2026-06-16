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
/// Blood Unit Index Grain — singleton: "BB-UNIT-IDX"
/// </summary>
public class BloodUnitIndexGrain : Grain, IBloodUnitIndexGrain
{
    private readonly IPersistentState<BloodUnitIndexState> _state;

    public BloodUnitIndexGrain(
        [PersistentState("bbUnitIndexState", "bbUnitIndexStore")]
        IPersistentState<BloodUnitIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(BloodUnitIndexEntry entry)
    {
        int idx = _state.State.Units.FindIndex(u => u.UnitId == entry.UnitId);
        if (idx >= 0)
            _state.State.Units[idx] = entry;
        else
            _state.State.Units.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<BloodUnitIndexEntry>> SearchAsync(
        BloodProductType? productType,
        AboBloodType? aboType,
        RhBloodType? rhType,
        BloodUnitStatus? status,
        bool availableOnly)
    {
        IEnumerable<BloodUnitIndexEntry> query = _state.State.Units;
        if (productType.HasValue) query = query.Where(u => u.ProductType == productType.Value);
        if (aboType.HasValue) query = query.Where(u => u.AboType == aboType.Value);
        if (rhType.HasValue) query = query.Where(u => u.RhType == rhType.Value);
        if (status.HasValue) query = query.Where(u => u.Status == status.Value);
        if (availableOnly) query = query.Where(u => u.Status == BloodUnitStatus.Available);
        return Task.FromResult(query.ToList());
    }

    public Task<List<BloodUnitIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Units);
}

/// <summary>Internal state wrapper for the blood unit index.</summary>
[GenerateSerializer]
public class BloodUnitIndexState
{
    [Id(0)]
    public List<BloodUnitIndexEntry> Units { get; set; } = new();
}
