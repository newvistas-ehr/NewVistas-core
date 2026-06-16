// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Admission Grain — a patient's inpatient admission to a BR center.
///
/// Derived from VistA Blind Rehabilitation module:
///   File #782.2 — BLIND REHABILITATION ADMISSION
///   Routines: ANRVAD.m, ANRVADB.m
///
/// Grain key: "BR-ADMIT:{admitId}"
/// </summary>
public interface IBRAdmissionGrain : IGrainWithStringKey
{
    /// <summary>Returns the full admission record.</summary>
    Task<BRAdmissionState> GetAsync();

    /// <summary>
    /// Creates an inpatient blind rehabilitation admission.
    /// Corresponds to VistA ANRVAD CREATE.
    /// </summary>
    Task CreateAsync(
        string admitId,
        string patientId,
        string centerId,
        string centerName,
        DateTime admitDate,
        DateTime? plannedDischargeDate,
        List<BRTrainingArea> programAreas,
        BRAdmissionPriority priority,
        string referringProviderId,
        string referringProviderName,
        string? goals,
        string? notes);

    /// <summary>
    /// Records progress notes and updates training status.
    /// Corresponds to VistA ANRVADB NOTE.
    /// </summary>
    Task AddProgressNoteAsync(string note, string authorId, string authorName);

    /// <summary>
    /// Records discharge from the BR center.
    /// Corresponds to VistA ANRVAD DISCHARGE.
    /// </summary>
    Task DischargeAsync(
        DateTime dischargeDate,
        BRDischargeDisposition disposition,
        string dischargeSummary,
        List<BRTrainingArea> areasCompleted,
        string? followUpPlan);

    /// <summary>Cancels the admission before it begins.</summary>
    Task CancelAsync(string reason);
}
