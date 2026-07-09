// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton directory of institutions (File #4 list). Grain key: "INSTITUTION-INDEX".
/// Store: institutionIndexStore. Maintained by InstitutionGrain — do not write directly.
/// </summary>
public interface IInstitutionIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(InstitutionIndexEntry entry, IEnumerable<string>? legacyAliases = null);

    Task<List<InstitutionIndexEntry>> GetAllAsync(bool activeOnly = true);

    Task<List<InstitutionIndexEntry>> GetByHealthSystemAsync(string healthSystemId);

    Task<List<InstitutionIndexEntry>> SearchAsync(string? nameContains, InstitutionType? type, string? capability);

    /// <summary>
    /// Resolve a legacy facility spelling ("MAIN", "INST-500") — or a canonical id —
    /// to the canonical institution id. Returns null when unknown.
    /// </summary>
    Task<string?> ResolveLegacyFacilityIdAsync(string legacyId);

    /// <summary>Drives the Transfer Center's self-hide on single-institution sites.</summary>
    Task<int> GetActiveCountAsync();
}
