// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Provider Directory Grain — singleton staff/provider lookup index (VistA NEW PERSON,
/// File #200 "B" name cross-reference). Grain key: "PROVIDER-DIRECTORY".
///
/// Lets a clinician find a colleague by name — e.g. a nurse entering an order on behalf
/// of a physician, or any field that references a provider other than the signed-in user.
/// Maintained by <see cref="INewPersonGrain"/> on every profile / active-status change,
/// so it stays in sync with the authoritative staff records.
/// </summary>
public interface IProviderDirectoryGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates a provider entry, matched by UserId.</summary>
    Task AddOrUpdateAsync(ProviderDirectoryEntry entry);

    /// <summary>Flips the active flag for a provider (terminated staff drop out of search).</summary>
    Task SetActiveAsync(string userId, bool isActive);

    /// <summary>Returns the entry for a provider by exact UserId, or null.</summary>
    Task<ProviderDirectoryEntry?> GetAsync(string userId);

    /// <summary>
    /// Searches active providers by name (case-insensitive substring on "LAST,FIRST MI",
    /// so both last- and first-name fragments match) or exact UserId. Ordered by name.
    /// </summary>
    Task<List<ProviderDirectoryEntry>> SearchAsync(string searchTerm, int maxResults = 25);

    /// <summary>Total number of providers in the directory (active and inactive).</summary>
    Task<int> GetCountAsync();
}
