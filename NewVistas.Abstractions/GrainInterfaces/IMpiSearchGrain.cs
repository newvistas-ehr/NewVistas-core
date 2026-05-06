// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// MPI Search/Index Grain — singleton that holds the searchable patient index
/// for cross-facility patient lookup and identification.
///
/// Grain Key: "MPI-INDEX" (singleton)
///
/// Provides fast in-memory search by patient name prefix, SSN (full or last-4),
/// and exact ICN lookup. Mirrors VistA's MPI lookup functionality used by
/// MPIF001.m and the Patient Lookup (DG) package.
///
/// The index is maintained by AddOrUpdatePatientAsync calls from the
/// MpiCorrelationGrain and demo data seeding.
///
/// Related VistA APIs:
///   MPIF001.m  — MPI functions
///   DG53541.m  — patient lookup
///   DGSEC.m    — sensitive patient check
/// </summary>
public interface IMpiSearchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Searches the MPI index by name prefix, SSN last-4, or exact ICN.
    /// - If searchText starts with a digit and is 10+ chars, tries exact ICN match first.
    /// - If searchText is exactly 4 digits, searches by SSN last-4.
    /// - Otherwise, searches by patient name prefix (case-insensitive).
    /// </summary>
    Task<List<MpiSearchResult>> SearchAsync(string searchText, int maxResults);

    /// <summary>
    /// Looks up a single patient by exact ICN.
    /// Returns null if no patient found with that ICN.
    /// </summary>
    Task<MpiSearchResult?> LookupByIcnAsync(string icn);

    /// <summary>
    /// Looks up patients by full SSN.
    /// Returns all patients matching the given SSN.
    /// </summary>
    Task<List<MpiSearchResult>> LookupBySsnAsync(string ssn);

    /// <summary>
    /// Adds or updates a patient entry in the MPI index.
    /// Called when a correlation is established or patient data changes.
    /// </summary>
    Task AddOrUpdatePatientAsync(MpiSearchEntry entry);

    /// <summary>
    /// Removes a patient from the MPI index by ICN.
    /// </summary>
    Task RemovePatientAsync(string icn);

    /// <summary>
    /// Returns index status and statistics.
    /// </summary>
    Task<MpiIndexStatus> GetStatusAsync();

    /// <summary>
    /// Seeds the index with 10 demo patients having ICNs and
    /// multi-facility correlations for development/testing.
    /// </summary>
    Task SeedDemoDataAsync();
}

/// <summary>
/// Status information for the MPI index.
/// </summary>
[GenerateSerializer]
public class MpiIndexStatus
{
    /// <summary>
    /// Total number of patients in the index.
    /// </summary>
    [Id(0)]
    public int TotalPatients { get; set; }

    /// <summary>
    /// Total number of facility correlations across all patients.
    /// </summary>
    [Id(1)]
    public int TotalCorrelations { get; set; }

    /// <summary>
    /// When the index was last updated.
    /// </summary>
    [Id(2)]
    public DateTime LastUpdated { get; set; }
}
