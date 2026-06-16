// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Patient Grain — maintains a patient's blind rehabilitation record.
///
/// Derived from VistA Blind Rehabilitation module (ANRV.m, ANRUTIL.m):
///   File #782   — BLIND REHABILITATION PATIENT record
///   File #783   — VISUAL ACUITY
///
/// Grain key: "BR-PATIENT:{patientId}"
/// </summary>
public interface IBRPatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the full blind rehabilitation patient record.</summary>
    Task<BRPatientState> GetAsync();

    /// <summary>
    /// Initializes the BR patient record.
    /// Corresponds to VistA ANRUTIL INIT.
    /// </summary>
    Task InitializeAsync(string patientId);

    /// <summary>
    /// Records or updates the patient's visual acuity assessment.
    /// Corresponds to VistA Visual Acuity file (#783).
    /// </summary>
    Task RecordVisualAcuityAsync(
        string rightEyeDistance,
        string leftEyeDistance,
        string bestCorrectedRight,
        string bestCorrectedLeft,
        VisualField visualFieldRight,
        VisualField visualFieldLeft,
        string? contrastSensitivity,
        DateTime examDate,
        string examinerId,
        string examinerName,
        string? notes);

    /// <summary>
    /// Records the patient's primary visual diagnosis.
    /// Corresponds to VistA File #782 field (.06).
    /// </summary>
    Task UpdateDiagnosisAsync(
        string primaryDiagnosis,
        string? secondaryDiagnosis,
        BROnsetType onsetType,
        DateTime? onsetDate,
        bool serviceConnected,
        int? serviceConnectedPercentage,
        string? icd10Code,
        string? notes);

    /// <summary>
    /// Records an assistive device or piece of adaptive equipment issued to the patient.
    /// Corresponds to VistA File #782 equipment sub-file.
    /// </summary>
    Task AddDeviceAsync(BRDeviceEntry device);

    /// <summary>
    /// Records a training goal for the patient.
    /// Corresponds to VistA File #782 goals sub-file.
    /// </summary>
    Task AddTrainingGoalAsync(string goal, BRTrainingArea area);

    /// <summary>Sets the patient's eligibility status for blind rehabilitation services.</summary>
    Task UpdateEligibilityAsync(BREligibilityStatus eligibility, string? reason);
}
