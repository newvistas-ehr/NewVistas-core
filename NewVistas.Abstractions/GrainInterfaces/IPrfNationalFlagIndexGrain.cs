// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of the four VistA PRF national patient record flag definitions (File #26.15).
/// Key: <c>"PRF-NATIONAL-IDX"</c>
/// </summary>
public interface IPrfNationalFlagIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all national PRF flag definitions.</summary>
    Task<List<PrfNationalFlagEntry>> GetAllAsync();

    /// <summary>
    /// Seeds the index with the four VistA national flags:
    /// BEHAVIORAL, HIGH RISK FOR SUICIDE, URGENT ADDRESS AS FEMALE, MISSING PATIENT.
    /// Idempotent — no-op if entries already exist.
    /// </summary>
    Task SeedDefaultsAsync();
}
