// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Orchestrates PT measurement workflows for a patient.
/// Each body group evaluation is a workflow: record ROM + strength, then compare with prior sessions.
/// Key: patient ID string.
/// </summary>
public interface IPTWorkflowGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records a complete body group session. Creates a PTSessionGrain and updates the index.
    /// Returns the session grain key.
    /// </summary>
    Task<string> RecordBodyGroupSessionAsync(
        BodyGroup bodyGroup,
        DateTime sessionDate,
        string? therapistId,
        string? therapistName,
        string? locationId,
        string? locationName,
        Laterality side,
        List<RomMeasurement> romMeasurements,
        List<StrengthMeasurement> strengthMeasurements,
        string? notes);

    /// <summary>
    /// Gets the last N sessions for a body group. Default 2 for side-by-side comparison.
    /// Returns full session state for each, ordered by date descending.
    /// </summary>
    Task<List<PTSessionState>> GetLatestSessionsAsync(BodyGroup bodyGroup, int count = 2);

    /// <summary>
    /// Gets session history for a body group with optional date range filtering.
    /// </summary>
    Task<List<PTSessionState>> GetSessionHistoryAsync(
        BodyGroup bodyGroup, DateTime? from, DateTime? to, int maxCount = 50);

    /// <summary>
    /// Returns which body groups have recorded PT data for this patient.
    /// </summary>
    Task<List<BodyGroup>> GetBodyGroupsWithDataAsync();

    /// <summary>
    /// Returns the standard movements for a body group (convenience wrapper over BodyGroupDefinitions).
    /// </summary>
    Task<List<Movement>> GetStandardMovementsAsync(BodyGroup bodyGroup);

    // ── PT Goals ──────────────────────────────────────────────────────

    /// <summary>Adds a therapeutic goal for a body group. Returns the goal ID.</summary>
    Task<string> AddGoalAsync(BodyGroup bodyGroup, PTGoal goal);

    /// <summary>Updates an existing goal's status, current value, and/or notes.</summary>
    Task UpdateGoalAsync(BodyGroup bodyGroup, string goalId, GoalStatus? status, decimal? currentValue, string? notes);

    /// <summary>Records a progress measurement for a goal.</summary>
    Task AddGoalProgressAsync(BodyGroup bodyGroup, string goalId, decimal value, string? notes);

    /// <summary>Returns all goals for a body group.</summary>
    Task<List<PTGoal>> GetGoalsForBodyGroupAsync(BodyGroup bodyGroup);

    /// <summary>Returns all active goals across all body groups for this patient.</summary>
    Task<List<PTGoal>> GetAllActiveGoalsAsync();

    // ── Clinic Exercises ──────────────────────────────────────────────

    /// <summary>Adds an exercise log entry to an existing PT session.</summary>
    Task AddClinicExerciseAsync(string sessionKey, ClinicExerciseLog exercise);

    // ── Home Exercise Program ─────────────────────────────────────────

    /// <summary>Adds a home exercise prescription. Returns the prescription ID.</summary>
    Task<string> AddHepPrescriptionAsync(HepPrescription prescription);

    /// <summary>Updates the status of a home exercise prescription.</summary>
    Task UpdateHepPrescriptionStatusAsync(string prescriptionId, HepStatus status);

    /// <summary>Logs completion of a home exercise. Returns the log ID.</summary>
    Task<string> LogHepCompletionAsync(HepCompletionLog log);

    /// <summary>Returns all active home exercise prescriptions for this patient.</summary>
    Task<List<HepPrescription>> GetActiveHepPrescriptionsAsync();

    /// <summary>Returns home exercise completion logs, optionally filtered.</summary>
    Task<List<HepCompletionLog>> GetHepCompletionLogsAsync(string? prescriptionId, DateTime? from, DateTime? to);

    // ── PT Referrals ─────────────────────────────────────────────────

    /// <summary>Creates a new PT referral for this patient. Returns the referral grain key.</summary>
    Task<string> CreateReferralAsync(
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

    /// <summary>Returns all referrals for this patient.</summary>
    Task<List<PTReferralState>> GetAllReferralsAsync();

    /// <summary>Returns only active referrals for this patient.</summary>
    Task<List<PTReferralState>> GetActiveReferralsAsync();

    /// <summary>Returns a single referral by its grain key.</summary>
    Task<PTReferralState> GetReferralAsync(string referralGrainKey);

    /// <summary>Updates referral status.</summary>
    Task UpdateReferralStatusAsync(string referralGrainKey, PTReferralStatus status, string? notes);

    /// <summary>Updates referral authorization parameters.</summary>
    Task UpdateReferralAuthorizationAsync(string referralGrainKey, int authorizedVisits, DateTime? expirationDate);

    /// <summary>
    /// Records a complete body group session, optionally linked to a PT referral.
    /// If referralGrainKey is provided, increments the referral's visit count.
    /// </summary>
    Task<string> RecordBodyGroupSessionAsync(
        BodyGroup bodyGroup,
        DateTime sessionDate,
        string? therapistId,
        string? therapistName,
        string? locationId,
        string? locationName,
        Laterality side,
        List<RomMeasurement> romMeasurements,
        List<StrengthMeasurement> strengthMeasurements,
        string? notes,
        string? referralGrainKey);
}
