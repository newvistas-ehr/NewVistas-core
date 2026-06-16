// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for patient record merging.
/// Enabled per site via ISiteParametersGrain.Features containing "PATIENT_MERGE".
/// Follows the Site Flavor Architecture (Option 4 — Composition): this grain
/// only activates when a site explicitly enables the PATIENT_MERGE feature.
///
/// Maps to VistA DG MERGE utility (File #15.1).
/// Keyed by merge job ID (e.g., "MERGE:{guid}").
/// </summary>
public interface IPatientMergeGrain : IGrainWithStringKey
{
    /// <summary>
    /// Execute a patient merge: move all clinical data from the source (duplicate)
    /// patient into the target (surviving) patient. The source patient is deactivated
    /// and marked as merged.
    /// </summary>
    /// <param name="targetPatientId">Surviving patient ID (keeps demographics)</param>
    /// <param name="sourcePatientId">Duplicate patient ID (will be deactivated)</param>
    /// <param name="reason">Reason for the merge (audit trail)</param>
    /// <param name="mergedByUserId">User authorizing the merge</param>
    /// <param name="mergedByUserName">Display name of user</param>
    /// <returns>Merge result with details of what was moved</returns>
    Task<PatientMergeResult> ExecuteMergeAsync(
        string targetPatientId,
        string sourcePatientId,
        string reason,
        string mergedByUserId,
        string mergedByUserName);

    /// <summary>
    /// Get the result/status of this merge job.
    /// </summary>
    Task<PatientMergeState> GetMergeStateAsync();
}
