// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-institution capacity board AND unit directory — replaces the old
/// IBedBoardGrain, IWardLocationIndexGrain, and INursingUnitIndexGrain.
///
/// Grain key: "BED-CAPACITY:{institutionId}". Store: bedCapacityStore.
///
/// Read-mostly: written ONLY by unit-grain pushes after each mutation (and on
/// unit activation, so a missed push self-heals). Never a second source of
/// truth — every number here is a rollup of exactly one unit grain's state.
/// </summary>
public interface IBedCapacityGrain : IGrainWithStringKey
{
    /// <summary>Called by unit grains only.</summary>
    Task UpsertUnitAsync(UnitCapacitySummary summary);

    /// <summary>Called on unit deactivation.</summary>
    Task RemoveUnitAsync(string unitId);

    /// <summary>The institution's unit directory with live counts.</summary>
    Task<List<UnitCapacitySummary>> GetUnitsAsync(bool activeOnly = true);

    Task<UnitCapacitySummary?> GetUnitAsync(string unitId);

    /// <summary>Institution-level totals (active units only).</summary>
    Task<(int Total, int Available, int Occupied, int Dirty, int Blocked, int OutOfService)> GetInstitutionTotalsAsync();

    /// <summary>Institution-wide EVS worklist in one read, oldest-dirty first, with isolation precautions.</summary>
    Task<List<(string UnitId, DirtyBedEntry Bed)>> GetDirtyBedQueueAsync();
}
