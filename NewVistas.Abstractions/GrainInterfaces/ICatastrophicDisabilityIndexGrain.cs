// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton reference index of catastrophic disability categories (VistA File #27.17).
/// Key: <c>"CAT-DISABILITY-IDX"</c>
/// </summary>
public interface ICatastrophicDisabilityIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all catastrophic disability entries.</summary>
    Task<List<CatastrophicDisabilityEntry>> GetAllAsync();

    /// <summary>
    /// Searches catastrophic disability entries by description text (case-insensitive).
    /// </summary>
    Task<List<CatastrophicDisabilityEntry>> SearchAsync(string text);

    /// <summary>
    /// Seeds the index with representative VistA catastrophic disability codes.
    /// Idempotent — no-op if entries already exist.
    /// </summary>
    Task SeedDefaultsAsync();
}
