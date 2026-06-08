// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain mapping drug dose forms to their valid routes of
/// administration, derived from RxNorm dose-form / dose-form-group metadata.
/// Grain key: "DOSE-FORM-ROUTE-INDEX".
///
/// Self-seeding: on first activation the grain loads embedded tables
/// (DFG→VistA routes, DF→DFG, VistA-form→DF). No admin load or internet access
/// is required — the data is small and stable and ships with the build.
///
/// Lookup chain: VistA DosageFormName → RxNorm dose form → dose form group(s)
/// → valid VistA route names (File #51.23).
/// </summary>
public interface IDoseFormRouteIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns the set of valid VistA route names for the given VistA dose form
    /// (or dispense unit) string, e.g. "TABLET" → ["ORAL","ENTERAL",...].
    /// Returns an empty list when the dose form is unknown/unmapped.
    /// </summary>
    Task<List<string>> GetValidRoutesForDoseFormAsync(string vistaDosageFormName);

    /// <summary>
    /// Returns true when <paramref name="route"/> is valid for the given dose
    /// form. Fails open: returns true when the dose form is unknown/unmapped or
    /// when either argument is blank, so the check never blocks an order it
    /// cannot evaluate. Case-insensitive.
    /// </summary>
    Task<bool> IsRouteValidForDoseFormAsync(string vistaDosageFormName, string route);

    /// <summary>Returns all curated dose form groups and their valid routes.</summary>
    Task<List<DoseFormGroup>> GetAllGroupsAsync();

    /// <summary>Returns the dose form group matching the given name, or null.</summary>
    Task<DoseFormGroup?> GetGroupByNameAsync(string name);

    /// <summary>Returns true once the grain has been seeded.</summary>
    Task<bool> IsLoadedAsync();

    /// <summary>
    /// Refreshes the DF→DFG and VistA-form→DF bridges from RxNav when a live
    /// client is configured. The curated DFG→route mapping is never overwritten.
    /// Returns the number of dose-form rows updated, or 0 when the feature is
    /// disabled (the offline default).
    /// </summary>
    Task<int> RefreshFromRxNavAsync();
}
