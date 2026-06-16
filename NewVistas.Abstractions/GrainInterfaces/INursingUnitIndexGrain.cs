// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton directory of all nursing units — VistA File #210 list.
/// Grain key: "NURS-UNIT-IDX"
/// </summary>
public interface INursingUnitIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns the full unit directory.</summary>
    Task<NursingUnitIndexState> GetAsync();

    /// <summary>Inserts or replaces a unit summary entry (sorted by name).</summary>
    Task UpsertUnitAsync(NursingUnitEntry entry);

    /// <summary>Removes a unit from the directory.</summary>
    Task RemoveUnitAsync(string unitId);
}
