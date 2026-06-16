// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single Medicine (Procedures) record for a patient.
/// Maps to VistA Medicine Package files #691-699
/// (Cardiology, Pulmonary Function, GI/Endoscopy, Electrocardiograms).
/// MUMPS routines: MDAPI.m, MDEV.m, MDECHO.m, MDPFT.m, MDEC.m, MDGI.m
/// Grain key pattern: "MED-PROC:{guid}"
/// </summary>
public interface IMedProcedureGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this procedure record.</summary>
    Task<MedProcedureState> GetProcedureAsync();

    /// <summary>
    /// Orders/registers a new Medicine procedure.
    /// MDAPI.m ORDER.
    /// </summary>
    Task OrderProcedureAsync(
        string patientId,
        MedProcedureCategory category,
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
    /// MDEV.m COMPLETE / MDAPI.m SIGNOUT.
    /// </summary>
    Task CompleteProcedureAsync(
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes);

    /// <summary>Cancels the procedure with an optional reason.</summary>
    Task CancelProcedureAsync(string? reason);

    // ── ECG-specific ──────────────────────────────────────────────────────────

    /// <summary>
    /// Records ECG measurements and interpretation.
    /// MDEC.m RESULTS.
    /// </summary>
    Task RecordEcgResultsAsync(
        int? rate,
        CardiacRhythm? rhythm,
        int? prIntervalMs,
        int? qrsDurationMs,
        int? qtcMs,
        int? axisDegrees,
        string? interpretation,
        bool? isNormal);

    // ── Cardiology-specific ───────────────────────────────────────────────────

    /// <summary>
    /// Records echocardiogram results.
    /// MDECHO.m RESULTS.
    /// </summary>
    Task RecordEchoResultsAsync(
        decimal? lvEjectionFraction,
        string? lvDiastolicFunction,
        string? valvularFindings);

    /// <summary>
    /// Records cardiac stress test results.
    /// MDSTRESS.m RESULTS.
    /// </summary>
    Task RecordStressTestResultsAsync(
        decimal? peakMets,
        decimal? targetHeartRatePct,
        bool? inducibleIschemia);

    /// <summary>
    /// Records Holter monitor summary results.
    /// MDHOLT.m RESULTS.
    /// </summary>
    Task RecordHolterResultsAsync(
        int? durationHours,
        int? arrhythmiaEvents,
        CardiacRhythm? dominantRhythm);

    /// <summary>
    /// Records cardiac catheterization findings.
    /// MDCATH.m RESULTS.
    /// </summary>
    Task RecordCathResultsAsync(
        string? accessSite,
        string? coronaryFindings,
        string? intervention);

    // ── Pulmonary Function-specific ───────────────────────────────────────────

    /// <summary>
    /// Records spirometry and lung volumes from pulmonary function testing.
    /// MDPFT.m SPIROMETRY.
    /// </summary>
    Task RecordPftResultsAsync(
        decimal? fev1,
        decimal? fev1PctPredicted,
        decimal? fvc,
        decimal? fvcPctPredicted,
        decimal? fev1FvcRatio,
        decimal? dlco,
        decimal? dlcoPctPredicted,
        decimal? tlc,
        decimal? rv,
        bool? obstructive,
        bool? restrictive,
        bool? bronchodilatorResponse);

    /// <summary>
    /// Records arterial blood gas values.
    /// MDABG.m RESULTS.
    /// </summary>
    Task RecordAbgResultsAsync(
        decimal? ph,
        decimal? pao2,
        decimal? paco2,
        decimal? hco3,
        decimal? sao2);

    // ── GI/Endoscopy-specific ─────────────────────────────────────────────────

    /// <summary>
    /// Records GI/Endoscopy procedure details and findings.
    /// MDGI.m RESULTS.
    /// </summary>
    Task RecordEndoscopyResultsAsync(
        EndoscopyType endoscopyType,
        BowelPrepQuality? bowelPrepQuality,
        bool? cecumReached,
        int? scopeAdvancedCm,
        bool? biopsyTaken,
        List<string>? biopsySites,
        int? polypCount,
        List<string>? polypDescriptions,
        List<string>? endoscopicInterventions);
}
