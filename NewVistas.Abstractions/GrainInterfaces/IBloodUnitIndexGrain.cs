// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blood Unit Index Grain — singleton inventory index for all blood units.
///
/// Grain key: "BB-UNIT-IDX" (singleton)
/// </summary>
public interface IBloodUnitIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(BloodUnitIndexEntry entry);

    /// <summary>Returns all units matching the given filters.</summary>
    Task<List<BloodUnitIndexEntry>> SearchAsync(
        BloodProductType? productType,
        AboBloodType? aboType,
        RhBloodType? rhType,
        BloodUnitStatus? status,
        bool availableOnly);

    Task<List<BloodUnitIndexEntry>> GetAllAsync();
}
