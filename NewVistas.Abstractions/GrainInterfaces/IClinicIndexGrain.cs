// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Clinic Index Grain — singleton index of all clinics.
/// Keyed as "SD-CLINIC-INDEX". Self-seeding with demo clinics.
/// VistA equivalent: "B" cross-reference on HOSPITAL LOCATION file (#44).
/// </summary>
public interface IClinicIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all clinics. Seeds demo data on first call if empty.
    /// </summary>
    Task<List<GrainStates.ClinicEntry>> GetAllClinicsAsync();

    /// <summary>
    /// Adds or updates a clinic entry in the index.
    /// </summary>
    Task AddOrUpdateClinicAsync(GrainStates.ClinicEntry entry);

    /// <summary>
    /// Searches clinics by name or stop code (case-insensitive substring).
    /// </summary>
    Task<List<GrainStates.ClinicEntry>> SearchClinicsAsync(string term);

    /// <summary>
    /// Seeds demonstration clinic data. Idempotent.
    /// </summary>
    Task SeedDemoClinicsAsync();
}
