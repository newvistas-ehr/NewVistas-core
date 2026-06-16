// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single radiation therapy treatment fraction.
/// Maps to VistA File #135 treatment sub-records.
/// MUMPS routines: RORTX.m
/// Grain key pattern: "RT-TX:{guid}"
/// </summary>
public interface IRadiationTherapyTreatmentGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this fraction record.</summary>
    Task<RtTreatmentState> GetTreatmentAsync();

    /// <summary>
    /// Records a delivered radiation therapy fraction.
    /// RORTX.m DELIVER.
    /// </summary>
    Task RecordDeliveryAsync(
        string courseId,
        string patientId,
        int fractionNumber,
        DateTime treatmentDate,
        int doseDeliveredCgy,
        int? treatmentDurationMin,
        string? machineId,
        string? machineName,
        string? technicianId,
        string? technicianName,
        bool setupVerified,
        string? setupMethod,
        decimal? setupDeviationMm,
        bool interrupted,
        string? interruptionReason,
        string? notes);

    /// <summary>
    /// Records a skipped or cancelled fraction.
    /// RORTX.m SKIP.
    /// </summary>
    Task RecordSkipAsync(
        string courseId,
        string patientId,
        int fractionNumber,
        DateTime scheduledDate,
        RtFractionStatus status,
        string? skipReason);

    /// <summary>Updates the status of this fraction record.</summary>
    Task UpdateStatusAsync(RtFractionStatus status, string? reason);
}
