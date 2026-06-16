// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single Clinical Procedures record for a patient.
/// Maps to VistA File #702 (CLINICAL PROCEDURES) covering non-radiology procedures
/// such as EEG, EMG, nerve conduction studies, sleep studies, and audiometry.
/// MUMPS routines: CPRS.m, CPRSPAT.m
/// Grain key pattern: "CP-PROC:{guid}"
/// </summary>
public interface IClinicProcedureGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this clinical procedure record.</summary>
    Task<ClinicProcedureState> GetProcedureAsync();

    /// <summary>
    /// Orders/registers a new clinical procedure.
    /// CPRS.m ORDER.
    /// </summary>
    Task OrderProcedureAsync(
        string patientId,
        ClinicProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication);

    /// <summary>Schedules the procedure for a specific date/time.</summary>
    Task ScheduleProcedureAsync(DateTime scheduledDate);

    /// <summary>Marks the procedure as in-progress (begun).</summary>
    Task BeginProcedureAsync(DateTime performedDate);

    /// <summary>
    /// Records the narrative findings and clinical impression, completing the procedure.
    /// CPRS.m COMPLETE.
    /// </summary>
    Task CompleteProcedureAsync(
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes);

    /// <summary>Cancels the procedure with an optional reason.</summary>
    Task CancelProcedureAsync(string? reason);

    // ── EEG-specific ──────────────────────────────────────────────────────────

    /// <summary>
    /// Records EEG study results including background activity and seizure findings.
    /// CPRSEEG.m RESULTS.
    /// </summary>
    Task RecordEegResultsAsync(
        int? durationMinutes,
        string? background,
        EegAlertType? alertType,
        bool? seizureActivity,
        string? focalRegion,
        List<string>? activations);

    // ── EMG-specific ──────────────────────────────────────────────────────────

    /// <summary>
    /// Records EMG study results including muscles studied and primary finding.
    /// CPRSEMG.m RESULTS.
    /// </summary>
    Task RecordEmgResultsAsync(
        List<string>? musclesStudied,
        EmgFindingType? findingType,
        string? spontaneousActivity,
        string? mupDescription);

    // ── Nerve Conduction Study-specific ───────────────────────────────────────

    /// <summary>
    /// Records nerve conduction study (NCS) results.
    /// CPRSNCS.m RESULTS.
    /// </summary>
    Task RecordNcsResultsAsync(
        List<string>? nervesStudied,
        decimal? meanMotorVelocity,
        decimal? meanSensoryVelocity,
        bool? fWavesObtained,
        EmgFindingType? findingType);

    // ── Sleep Study-specific ──────────────────────────────────────────────────

    /// <summary>
    /// Records polysomnography / sleep study results.
    /// CPRSSLEEP.m RESULTS.
    /// </summary>
    Task RecordSleepStudyResultsAsync(
        SleepStudyType studyType,
        SleepApneaType? apneaType,
        decimal? apneaHypopneaIndex,
        decimal? cpapPressureCmH2O,
        decimal? sleepEfficiencyPct,
        int? totalSleepTimeMin,
        decimal? sleepLatencyMin,
        decimal? remLatencyMin);

    // ── Audiometry-specific ───────────────────────────────────────────────────

    /// <summary>
    /// Records audiometry results including pure tone averages and speech discrimination.
    /// CPRSAUD.m RESULTS.
    /// </summary>
    Task RecordAudiometryResultsAsync(
        HearingLossType? hearingLossType,
        decimal? rightEarPta,
        decimal? leftEarPta,
        decimal? speechDiscriminationRight,
        decimal? speechDiscriminationLeft,
        string? tympanometryRight,
        string? tympanometryLeft);
}
