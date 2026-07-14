// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Home-Health Agency Directory Grain — singleton directory of home-health agencies. Grain key:
/// "HHA-DIRECTORY". The agency an externally-delivered home-care episode points at, and the picker a
/// coordinator chooses from when referring a patient out. Mirrors the PharmacyDirectory singleton
/// pattern; auto-seeds a demo set on first read so the picker is never empty. The IN_HOUSE entry is
/// the health system's own licensed agency (the hospital-provided delivering org); EXTERNAL entries
/// are independent agencies.
/// </summary>
public interface IHomeHealthAgencyDirectoryGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates an agency entry, matched by AgencyId.</summary>
    Task AddOrUpdateAsync(HomeHealthAgencyEntry entry);

    /// <summary>Flips the active flag for an agency (inactive ones drop out of search).</summary>
    Task SetActiveAsync(string agencyId, bool isActive);

    /// <summary>Returns the entry for an agency by exact AgencyId, or null.</summary>
    Task<HomeHealthAgencyEntry?> GetAsync(string agencyId);

    /// <summary>
    /// Searches active agencies by name (case-insensitive substring) or exact AgencyId. When
    /// <paramref name="externalOnly"/> is true, the in-house agency is excluded (referral-out picker).
    /// </summary>
    Task<List<HomeHealthAgencyEntry>> SearchAsync(string searchTerm, bool externalOnly = false, int maxResults = 25);

    /// <summary>All active agencies, ordered by name; excludes IN_HOUSE when externalOnly.</summary>
    Task<List<HomeHealthAgencyEntry>> GetAllAsync(bool externalOnly = false);

    /// <summary>Total number of agencies in the directory.</summary>
    Task<int> GetCountAsync();

    /// <summary>Idempotently seeds the demo agency set (one in-house + a couple of external agencies).</summary>
    Task SeedDemoAgenciesAsync();
}
