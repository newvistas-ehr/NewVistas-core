// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages all PRF patient record flag assignments for a single patient
/// (VistA Files #26.11 PRF LOCAL FLAG, #26.13 PRF ASSIGNMENT).
/// Key: <c>"PRF-ASSIGN:{patientId}"</c>
/// MUMPS references: DGPFAA.m, DGPFAPD.m, DGPFM.m
/// </summary>
public interface IPrfAssignmentGrain : IGrainWithStringKey
{
    /// <summary>Returns all flag assignments (active and historical).</summary>
    Task<PrfAssignmentState> GetAsync();

    /// <summary>Returns only currently active flag assignments.</summary>
    Task<List<PrfFlagAssignment>> GetActiveFlagsAsync();

    /// <summary>Assigns a new PRF flag to the patient.</summary>
    Task AssignFlagAsync(
        string flagId,
        string flagName,
        string flagType,
        bool isNational,
        string assignedByUserId,
        string assignedByUserName,
        string? narrative);

    /// <summary>Deactivates an active flag assignment.</summary>
    Task DeactivateFlagAsync(string flagId, string deactivatedReason, string deactivatedByUserId);

    /// <summary>Records a periodic review of a flag assignment.</summary>
    Task RecordReviewAsync(
        string flagId,
        string reviewedByUserId,
        string reviewedByUserName,
        DateTime reviewDate,
        string? narrative);
}
