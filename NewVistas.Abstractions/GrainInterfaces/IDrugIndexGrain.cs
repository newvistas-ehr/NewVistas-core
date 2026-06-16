// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for the facility drug catalog (VistA File #50).
/// Grain key: "DRUG-INDEX"
///
/// Provides fast search and lookup across all facility drugs without
/// activating individual DrugGrain instances. Follows the same two-grain
/// pattern used by NDF (VaProductIndexGrain) and ICD-10 (Icd10IndexGrain).
///
/// The index is populated via bulk load (LoadDrugsAsync) during data import.
/// Individual drug saves should update both the DrugGrain and the index.
/// </summary>
public interface IDrugIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Bulk-loads the drug index from a list of index entries.
    /// Replaces the entire existing index. Sets IsLoaded = true.
    /// </summary>
    Task LoadDrugsAsync(List<DrugIndexEntry> entries);

    /// <summary>
    /// Returns the index entry for the given IEN, or null if not found.
    /// </summary>
    Task<DrugIndexEntry?> GetDrugAsync(string ien);

    /// <summary>
    /// Returns all drug entries linked to a specific VA Product IEN.
    /// A VA Product may have multiple local drug entries (different packaging).
    /// </summary>
    Task<List<DrugIndexEntry>> GetByVaProductAsync(string vaProductIen);

    /// <summary>
    /// Returns all drug entries linked to a specific VA Generic IEN.
    /// Equivalent to querying by generic drug name across all formulations.
    /// </summary>
    Task<List<DrugIndexEntry>> GetByVaGenericAsync(string vaGenericIen);

    /// <summary>
    /// Full-text search across drug local names and synonyms.
    /// </summary>
    /// <param name="searchText">Substring to match (case-insensitive).</param>
    /// <param name="pharmacyType">Filter by pharmacy type; null = all types.</param>
    /// <param name="activeOnly">When true, excludes inactive drugs.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    Task<List<DrugIndexEntry>> SearchAsync(
        string searchText,
        PharmacyType? pharmacyType = null,
        bool activeOnly = true,
        int maxResults = 50);

    /// <summary>
    /// Returns the current status of the drug index.
    /// </summary>
    Task<DrugIndexStatus> GetStatusAsync();

    /// <summary>
    /// Clears the entire index. Used during data refresh or testing.
    /// </summary>
    Task ClearAsync();
}
