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
/// Per-institution capacity board + unit directory. Written only by unit-grain
/// pushes; every read is a projection of those rollups. No self-seeding —
/// entries appear automatically on a unit's first capacity push.
/// </summary>
public class BedCapacityGrain : Grain, IBedCapacityGrain
{
    private readonly IPersistentState<BedCapacityState> _state;

    public BedCapacityGrain(
        [PersistentState("bedCapacity", "bedCapacityStore")]
        IPersistentState<BedCapacityState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstitutionId))
        {
            string rawKey = this.GetPrimaryKeyString();
            _state.State.InstitutionId = rawKey.StartsWith("BED-CAPACITY:")
                ? rawKey["BED-CAPACITY:".Length..]
                : rawKey;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task UpsertUnitAsync(UnitCapacitySummary summary)
    {
        if (string.IsNullOrWhiteSpace(summary.UnitId))
            return;

        _state.State.Units.RemoveAll(u => u.UnitId == summary.UnitId);
        _state.State.Units.Add(summary);
        _state.State.Units.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveUnitAsync(string unitId)
    {
        int removed = _state.State.Units.RemoveAll(u => u.UnitId == unitId);
        if (removed == 0)
            return;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<UnitCapacitySummary>> GetUnitsAsync(bool activeOnly = true)
        => Task.FromResult(_state.State.Units
            .Where(u => !activeOnly || u.IsActive)
            .ToList());

    public Task<UnitCapacitySummary?> GetUnitAsync(string unitId)
        => Task.FromResult(_state.State.Units.FirstOrDefault(u => u.UnitId == unitId));

    public Task<(int Total, int Available, int Occupied, int Dirty, int Blocked, int OutOfService)> GetInstitutionTotalsAsync()
    {
        List<UnitCapacitySummary> active = _state.State.Units.Where(u => u.IsActive).ToList();
        return Task.FromResult((
            active.Sum(u => u.TotalBeds),
            active.Sum(u => u.Available),
            active.Sum(u => u.Occupied),
            active.Sum(u => u.Dirty),
            active.Sum(u => u.Blocked),
            active.Sum(u => u.OutOfService)));
    }

    public Task<List<(string UnitId, DirtyBedEntry Bed)>> GetDirtyBedQueueAsync()
        => Task.FromResult(_state.State.Units
            .Where(u => u.IsActive)
            .SelectMany(u => u.DirtyBeds.Select(b => (u.UnitId, b)))
            .OrderBy(x => x.b.DirtySince ?? DateTime.MaxValue)
            .ToList());
}
