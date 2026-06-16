// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient dental record grain storing eligibility, clinical status, and
/// aggregate dental health information.
/// Maps to VistA File #228 DENTAL PATIENT, managed by DENPAT.m routines.
/// Grain key: "DENTAL-PATIENT:{patientId}".
/// </summary>
public interface IDentalPatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the current dental patient state.</summary>
    Task<DentalPatientState> GetAsync();

    /// <summary>
    /// Initialises the record for a new patient (idempotent — safe to call more than once).
    /// </summary>
    Task EnsureInitializedAsync(string patientId);

    /// <summary>
    /// Updates dental eligibility status and basis for the patient.
    /// </summary>
    Task UpdateEligibilityAsync(
        DentalEligibilityStatus eligibilityStatus,
        string? eligibilityBasisCode,
        string? eligibilityBasisDescription);

    /// <summary>
    /// Sets or updates the patient's primary VA dentist.
    /// </summary>
    Task SetPrimaryDentistAsync(string dentistId, string dentistName);

    /// <summary>
    /// Updates key clinical status fields: periodontal classification, prosthetic
    /// status, remaining teeth count, fluoride flag, and clinical notes.
    /// </summary>
    Task UpdateClinicalStatusAsync(
        DentalPeriodontalStatus periodontalStatus,
        string? prostheticStatus,
        int? remainingTeethCount,
        bool onFluoride,
        string? clinicalNotes);

    /// <summary>
    /// Records the date of the most recent exam, x-rays, or cleaning.
    /// Null values leave the corresponding field unchanged.
    /// </summary>
    Task RecordVisitDatesAsync(
        DateTime? lastExamDate,
        DateTime? lastXRayDate,
        DateTime? lastCleaningDate);
}
