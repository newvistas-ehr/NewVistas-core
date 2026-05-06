// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Grain representing a single PT referral from an external referring provider.
/// Key format: "PTREF:{patientId}:{guid}"
/// </summary>
public interface IPTReferralGrain : IGrainWithStringKey
{
    /// <summary>Returns the full referral state.</summary>
    Task<PTReferralState> GetReferralAsync();

    /// <summary>Creates the referral (write-once). Populates all fields.</summary>
    Task CreateReferralAsync(
        string patientId,
        string patientName,
        string? referringProviderName,
        string? referringProviderId,
        string? referringProviderSpecialty,
        string? referringFacilityName,
        string? diagnosis,
        string? diagnosisCode,
        List<BodyGroup>? bodyGroups,
        string? reasonForReferral,
        string? precautions,
        int authorizedVisits,
        DateTime? authorizationExpirationDate,
        DateTime referralDate,
        DateTime? receivedDate,
        string? notes);

    /// <summary>Updates the referral lifecycle status.</summary>
    Task UpdateStatusAsync(PTReferralStatus status, string? notes);

    /// <summary>
    /// Increments UsedVisits by 1. Called when a session is recorded against this referral.
    /// Does not block if UsedVisits exceeds AuthorizedVisits — the referral is context, not a gate.
    /// Returns the new UsedVisits count.
    /// </summary>
    Task<int> IncrementVisitCountAsync();

    /// <summary>Updates the visit authorization parameters.</summary>
    Task UpdateAuthorizationAsync(int authorizedVisits, DateTime? expirationDate);

    /// <summary>Updates the referral notes.</summary>
    Task UpdateNotesAsync(string notes);
}
