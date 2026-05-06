// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Master Patient Index Correlation Grain — represents a single patient's
/// cross-facility identity in the MPI, keyed by Integration Control Number (ICN).
///
/// Grain Key: "MPI:{icn}" where ICN is the national patient identifier
/// (e.g., "MPI:1000000001V123456").
///
/// Maps to VistA PATIENT CORRELATION file (#985) and the MPI/PD package.
/// The ICN is a 10-digit number + "V" + 6-digit checksum assigned by the
/// Master Patient Index at Austin.
///
/// Related VistA APIs / MUMPS routines:
///   GETICN^MPIF001  — retrieve ICN for a local patient
///   SETCOR^MPIF001  — establish/update a correlation
///   GETCOR^MPIF001  — retrieve all correlations for an ICN
///   RGADTP.m        — ADT messaging to/from MPI
///   VAFCTFU.m       — treating facility utility
/// </summary>
public interface IMpiCorrelationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the full correlation state for this patient.
    /// Mirrors GETCOR^MPIF001.
    /// </summary>
    Task<MpiCorrelationState> GetCorrelationAsync();

    /// <summary>
    /// Sets or updates the core identity fields for this patient in the MPI.
    /// Mirrors SETCOR^MPIF001.
    /// </summary>
    Task SetCorrelationAsync(string icn, string patientName, string ssn, DateTime? dateOfBirth, string? sex);

    /// <summary>
    /// Adds a local facility correlation — maps this national ICN to a local
    /// DFN (Data File Number) at a specific VistA facility.
    /// Mirrors the PATIENT CORRELATION file (#985) entry creation.
    /// </summary>
    Task AddLocalCorrelationAsync(string facilityId, string facilityName, string localDfn, DateTime correlationDate);

    /// <summary>
    /// Removes a local facility correlation.
    /// </summary>
    Task RemoveLocalCorrelationAsync(string facilityId);

    /// <summary>
    /// Gets the list of treating facilities for this patient.
    /// Mirrors VAFCTFU treating facility list.
    /// </summary>
    Task<List<MpiLocalCorrelation>> GetTreatingFacilitiesAsync();

    /// <summary>
    /// Updates the last-seen date for a specific facility correlation.
    /// Called when a patient is seen at a facility.
    /// </summary>
    Task UpdateLastSeenAsync(string facilityId, DateTime lastSeenDate);

    /// <summary>
    /// Marks this patient as deceased in the MPI.
    /// Propagated from File #2 date of death field (.351).
    /// </summary>
    Task MarkDeceasedAsync(DateTime dateOfDeath);

    /// <summary>
    /// Marks this ICN as merged into another (the surviving ICN). Sets
    /// <see cref="MpiCorrelationState.MergedIntoIcn"/> permanently;
    /// idempotent if the same target is supplied. Called from the
    /// <c>PatientMergeGrain</c> after the clinical-data move completes
    /// so cross-cluster lookups by this ICN can redirect to the survivor.
    /// </summary>
    /// <param name="survivingIcn">The target ICN that this one was merged into.</param>
    Task MarkAsMergedAsync(string survivingIcn);
}
