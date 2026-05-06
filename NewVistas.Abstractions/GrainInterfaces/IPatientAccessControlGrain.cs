// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient Access Control Grain — manages sensitive record flags, authorized provider lists,
/// and break-the-glass audit trail.
///
/// Derived from VistA DG SENSITIVITY routines and DG SECURITY LOG (File #38.1).
/// Key pattern: "PAC:{patientId}"
/// </summary>
public interface IPatientAccessControlGrain : IGrainWithStringKey
{
    /// <summary>
    /// Get the full access control state for this patient.
    /// </summary>
    Task<GrainStates.PatientAccessControlState> GetAccessControlAsync();

    /// <summary>
    /// Set the sensitivity flags for this patient record.
    /// </summary>
    Task SetSensitivityAsync(bool isSensitive, string sensitivityLevel, List<string> categories);

    /// <summary>
    /// Add a provider to the authorized access list (treating team).
    /// </summary>
    Task AddAuthorizedProviderAsync(string providerId);

    /// <summary>
    /// Remove a provider from the authorized access list.
    /// </summary>
    Task RemoveAuthorizedProviderAsync(string providerId);

    /// <summary>
    /// Check whether a user has access to this patient record.
    /// Returns true if patient is not sensitive OR user is in AuthorizedProviderIds.
    /// </summary>
    Task<bool> CheckAccessAsync(string userId);

    /// <summary>
    /// Record an access event in the audit log.
    /// </summary>
    Task RecordAccessAsync(string userId, string userName, string accessReason, bool wasBreakTheGlass, string? justificationText);

    /// <summary>
    /// Get the full access audit log for this patient.
    /// </summary>
    Task<List<GrainStates.PatientAccessLog>> GetAccessLogAsync();

    /// <summary>
    /// Clear the access audit log (admin only).
    /// </summary>
    Task ClearAccessLogAsync();

    /// <summary>
    /// Record whether the patient has consented to Part 2 disclosure (42 CFR Part 2).
    /// </summary>
    Task SetPart2ConsentAsync(bool hasConsent, DateTime? consentDate, string? scope);

    /// <summary>
    /// Check if Part 2 consent is on file for this patient.
    /// </summary>
    Task<bool> HasPart2ConsentAsync();
}
