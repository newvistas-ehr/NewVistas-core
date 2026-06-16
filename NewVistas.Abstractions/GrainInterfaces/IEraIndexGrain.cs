// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all ERAs (Electronic Remittance Advices).
/// Grain key: "ERA-IDX"
/// </summary>
public interface IEraIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new entry or updates an existing one (matched by EraId).</summary>
    Task AddOrUpdateAsync(EraIndexEntry entry);

    /// <summary>Returns all ERA entries.</summary>
    Task<List<EraIndexEntry>> GetAllAsync();
}
