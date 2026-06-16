// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for patient lookup and search.
/// Grain key: "PATIENT-INDEX"
///
/// Mirrors the VistA ORWPT LOOKUP RPC (ORWPT.m), which supports:
///   - Last name prefix search  (^DPT "B" cross-reference)
///   - SSN last-4 exact match   (^DPT "BS5" cross-reference)
///   - DFN exact match          (^DPT direct IEN access)
///
/// The controller calls this grain directly for system-level search,
/// following the same pattern as ICD-10, drug, and DSS unit index grains.
/// </summary>
public interface IPatientIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds a new patient to the index, or updates an existing entry matched by PatientId.
    /// Called by PatientWorkflowGrain after every demographics, DFN, or ICN change.
    /// </summary>
    Task AddOrUpdateAsync(PatientIndexEntry entry);

    /// <summary>
    /// Searches the patient index. Search logic mirrors ORWPT LOOKUP:
    ///   - 4-digit all-numeric term → SSN last-4 exact match
    ///   - 1–8 digit all-numeric term → DFN exact match
    ///   - Otherwise → case-insensitive prefix match on Name (last name or "LAST,FIRST" prefix)
    /// Returns up to maxResults entries ordered by Name ascending.
    /// </summary>
    Task<List<PatientIndexEntry>> SearchAsync(string searchTerm, int maxResults = 25);

    /// <summary>
    /// Returns the index entry for a patient by exact PatientId (Orleans grain key).
    /// Returns null if the patient is not in the index.
    /// </summary>
    Task<PatientIndexEntry?> GetByPatientIdAsync(string patientId);

    /// <summary>
    /// Returns the index entry for a patient by DFN (exact match).
    /// Returns null if no patient with that DFN is in the index.
    /// </summary>
    Task<PatientIndexEntry?> GetByDfnAsync(string dfn);

    /// <summary>
    /// Returns the index entry for a patient by ICN (exact match).
    /// Returns null if no patient with that ICN is in the index.
    /// </summary>
    Task<PatientIndexEntry?> GetByIcnAsync(string icn);

    /// <summary>Returns the total number of patients in the index.</summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Returns the patient ids (Orleans grain keys) of all indexed patients,
    /// up to <paramref name="maxResults"/>. Used by bulk operations that need
    /// to walk the cohort (NDW export, federated GPRA aggregation, etc.).
    /// Pass null for no limit.
    /// </summary>
    Task<List<string>> GetAllPatientIdsAsync(int? maxResults = null);

    // ── Versioned snapshot/delta serving for PatientSearchGrain readers ──

    /// <summary>
    /// Current monotonic index version (bumped on every mutation). Tiny
    /// payload — search readers call this once per search to validate their
    /// silo-local snapshot.
    /// </summary>
    Task<long> GetVersionAsync();

    /// <summary>Full versioned copy of the index for silo-local caching.</summary>
    Task<PatientIndexSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Changes after the given version, or SnapshotRequired when the version
    /// predates the in-memory change buffer (reader then takes a full pull).
    /// </summary>
    Task<PatientIndexDelta> GetChangesSinceAsync(long since);
}
