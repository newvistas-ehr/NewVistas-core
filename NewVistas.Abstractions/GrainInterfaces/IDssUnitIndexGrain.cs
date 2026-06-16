// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// DSS Unit Index Grain — application-wide searchable index of DSS units.
/// Enables lookup of DSS units by name, code, or active status.
/// MUMPS routines: ECPEDSS.m (DSS unit definition maintenance).
///
/// Grain key: "EC-DSS-IDX" (singleton)
/// </summary>
public interface IDssUnitIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates a DSS unit entry in the index.</summary>
    Task AddOrUpdateAsync(GrainStates.DssUnitIndexEntry entry);

    /// <summary>
    /// Searches DSS units by name or code fragment.
    /// Returns active units by default.
    /// </summary>
    Task<List<GrainStates.DssUnitIndexEntry>> SearchAsync(
        string? searchText,
        bool activeOnly,
        int maxResults);

    /// <summary>Returns all DSS units (active and inactive).</summary>
    Task<List<GrainStates.DssUnitIndexEntry>> GetAllAsync();
}
