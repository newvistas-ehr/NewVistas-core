// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Pharmacy Directory Grain — singleton directory of dispensing pharmacies. Grain key:
/// "PHARMACY-DIRECTORY". Lets a provider (or a patient, in the portal) choose which
/// outpatient pharmacy a prescription is sent to. Auto-seeds a demo set on first read.
///
/// Outpatient (RETAIL / MAIL / SPECIALTY) pharmacies are the patient-choice list; the
/// INPATIENT (hospital) pharmacy is the only destination for inpatient orders and is
/// excluded from the outpatient search.
/// </summary>
public interface IPharmacyDirectoryGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates a pharmacy entry, matched by PharmacyId.</summary>
    Task AddOrUpdateAsync(PharmacyDirectoryEntry entry);

    /// <summary>Flips the active flag for a pharmacy (inactive ones drop out of search).</summary>
    Task SetActiveAsync(string pharmacyId, bool isActive);

    /// <summary>Returns the entry for a pharmacy by exact PharmacyId, or null.</summary>
    Task<PharmacyDirectoryEntry?> GetAsync(string pharmacyId);

    /// <summary>
    /// Searches active pharmacies by name (case-insensitive substring) or exact PharmacyId /
    /// NCPDP id. When <paramref name="outpatientOnly"/> is true (default) the hospital
    /// (INPATIENT) pharmacy is excluded.
    /// </summary>
    Task<List<PharmacyDirectoryEntry>> SearchAsync(string searchTerm, bool outpatientOnly = true, int maxResults = 25);

    /// <summary>All active pharmacies, ordered by name; excludes INPATIENT when outpatientOnly.</summary>
    Task<List<PharmacyDirectoryEntry>> GetAllAsync(bool outpatientOnly = true);

    /// <summary>Total number of pharmacies in the directory.</summary>
    Task<int> GetCountAsync();

    /// <summary>Idempotently seeds the demo pharmacy set (hospital, mail, several retail).</summary>
    Task SeedDemoPharmaciesAsync();
}
