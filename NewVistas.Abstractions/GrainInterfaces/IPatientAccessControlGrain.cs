// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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

    // ── ADR-002 Phase 4 — employee-patient / share-preference / access decision ──

    /// <summary>
    /// Marks (or clears) the record as an employee-patient sensitivity (adds/removes the "EMPLOYEE"
    /// category; keeps the record sensitive while any category remains). Called automatically by the
    /// Person anchor when a human gains/loses both a patient- and a staff-role.
    /// </summary>
    Task SetEmployeePatientAsync(bool isEmployeePatient);

    /// <summary>Sets the patient's own sharing preference (maximal-openness is a first-class choice).</summary>
    Task SetSharePreferenceAsync(GrainStates.PatientSharePreference preference);

    /// <summary>
    /// Decides — and audits — a viewer's access to this record. Treatment relationship (authorized
    /// list) is never gated; a patient's open-sharing choice is honored; otherwise, for a sensitive
    /// record, access without a relationship requires break-the-glass (attest-and-proceed, never a hard
    /// block). Records the access (including a pending-BTG attempt) to the audit log.
    /// </summary>
    Task<GrainStates.PatientAccessDecision> DecideAccessAsync(
        string viewerUserId, string viewerName, bool breakTheGlassAttested, string? justificationText);
}
