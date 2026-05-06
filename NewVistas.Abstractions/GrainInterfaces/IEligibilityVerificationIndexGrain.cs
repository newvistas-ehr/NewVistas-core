// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of eligibility verification inquiries (EDI 270/271).
/// Grain key: "ELIG-IDX:{patientId}"
/// </summary>
public interface IEligibilityVerificationIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns the full index state.</summary>
    Task<EligibilityVerificationIndexState> GetAsync();

    /// <summary>Returns all eligibility verification entries for this patient.</summary>
    Task<List<EligibilityVerificationIndexEntry>> GetAllAsync();

    /// <summary>Adds or updates an eligibility verification entry in the index.</summary>
    Task AddOrUpdateAsync(EligibilityVerificationIndexEntry entry);

    /// <summary>Returns only entries that are eligible (coverage confirmed).</summary>
    Task<List<EligibilityVerificationIndexEntry>> GetEligibleAsync();

    /// <summary>Returns the most recent verification for a given payer.</summary>
    Task<EligibilityVerificationIndexEntry?> GetLatestForPayerAsync(string payerName);
}
