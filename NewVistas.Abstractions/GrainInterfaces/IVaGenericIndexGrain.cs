// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// VA Generic Drug Index Grain — singleton holding the in-memory catalog
/// of all VA Generic drug names from NDF File #50.6.
///
/// Grain Key: "NDF-GENERIC-INDEX" (singleton)
///
/// VA Generics (~5,417 entries) are the root of the NDF hierarchy.
/// Each generic groups one or more VA Product formulations.
///
/// Mirrors VistA cross-reference:
///   ^PSNDF(50.6,"B",name,ien) — lookup by generic name
/// </summary>
public interface IVaGenericIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads VA Generic entries into the index.
    /// </summary>
    Task LoadGenericsAsync(List<VaGenericEntry> entries);

    /// <summary>
    /// Looks up a single VA Generic by its IEN. Returns null if not found.
    /// </summary>
    Task<VaGenericEntry?> GetGenericAsync(string ien);

    /// <summary>
    /// Searches VA Generic names by text, case-insensitively.
    /// Mirrors $$VAGN^PSNAPIS and PSNLOOK.m generic name browsing.
    /// </summary>
    Task<List<VaGenericEntry>> SearchAsync(string searchText, bool activeOnly, int maxResults);

    /// <summary>
    /// Returns index load status.
    /// </summary>
    Task<NdfGenericIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the index.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Status information for the VA Generic index.
/// </summary>
[GenerateSerializer]
public class NdfGenericIndexStatus
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    [Id(2)]
    public int TotalGenerics { get; set; }

    [Id(3)]
    public int ActiveGenerics { get; set; }
}
