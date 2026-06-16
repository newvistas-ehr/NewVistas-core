// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single radiation therapy treatment course for a patient.
/// Maps to VistA File #135 (RADIATION THERAPY).
/// MUMPS routines: RORTS.m, RORTX.m, RORTP.m
/// Grain key pattern: "RT-COURSE:{guid}"
/// </summary>
public interface IRadiationTherapyCourseGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this RT course.</summary>
    Task<RtCourseState> GetCourseAsync();

    /// <summary>
    /// Creates/initializes a new radiation therapy course with prescription details.
    /// RORTS.m CREATE.
    /// </summary>
    Task CreateCourseAsync(
        string patientId,
        string courseName,
        string diagnosisCode,
        string diagnosisText,
        string treatmentSite,
        RtLaterality laterality,
        RtIntent intent,
        RtModality modality,
        int prescribedDoseCgy,
        int fractionsPlanned,
        int dosePerFractionCgy,
        string? beamEnergy,
        string? oncologistId,
        string? oncologistName,
        string? physicistId,
        string? physicistName,
        string? dosimetristId,
        string? dosimetristName,
        string? treatmentMachineId,
        string? treatmentMachineName,
        string? planningNotes);

    /// <summary>Records CT simulation completion. RORTS.m SIM.</summary>
    Task RecordSimulationAsync(DateTime simulationDate, string? planningNotes);

    /// <summary>
    /// Marks the course as active (first fraction delivered).
    /// RORTS.m START.
    /// </summary>
    Task StartCourseAsync(DateTime treatmentStartDate);

    /// <summary>Marks the course as completed. RORTS.m COMPLETE.</summary>
    Task CompleteCourseAsync(DateTime completionDate, string? notes);

    /// <summary>Discontinues the course. RORTS.m DISC.</summary>
    Task DiscontinueCourseAsync(DateTime discontinuationDate, string reason, string? notes);

    /// <summary>Places the course on hold. RORTS.m HOLD.</summary>
    Task PlaceCourseOnHoldAsync(string? reason);

    /// <summary>Resumes a course from on-hold. RORTS.m RESUME.</summary>
    Task ResumeCourseAsync();

    /// <summary>
    /// Records a delivered fraction and updates cumulative dose totals.
    /// RORTX.m DELIVER.
    /// </summary>
    Task RecordFractionDeliveredAsync(int doseDeliveredCgy);

    /// <summary>Sets boost phase details for this course.</summary>
    Task SetBoostAsync(
        string boostSite,
        int boostDoseCgy,
        int boostFractionsPlanned);

    /// <summary>Sets brachytherapy details for this course.</summary>
    Task SetBrachytherapyAsync(BrachytherapyDoseRate doseRate, string? isotope);
}
