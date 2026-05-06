// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.GrainStates;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Per-patient index of PT referral grain keys.
/// Key format: "PTREF-IDX:{patientId}"
/// </summary>
public interface IPTReferralIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates an entry in the index (upsert by ReferralGrainKey).</summary>
    Task AddOrUpdateAsync(PTReferralIndexEntry entry);

    /// <summary>Returns all referral entries for this patient, sorted by ReferralDate descending.</summary>
    Task<List<PTReferralIndexEntry>> GetAllReferralsAsync();

    /// <summary>Returns only active referral entries for this patient.</summary>
    Task<List<PTReferralIndexEntry>> GetActiveReferralsAsync();

    /// <summary>Returns the total referral count for this patient.</summary>
    Task<int> GetCountAsync();
}
