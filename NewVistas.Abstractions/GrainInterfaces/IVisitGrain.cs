// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Visit Grain Interface based on VistA PCE VISIT file (#9000010).
///
/// PCE (Patient Care Encounter) captures all patient-provider interactions.
/// Each visit record is the anchor for all clinical data documented during
/// an encounter: diagnoses (V POV #9000010.07), procedures (V CPT #9000010.18),
/// and encounter providers.
///
/// MUMPS references: PXCE*.m, PXK*.m, PXSAVE.m
/// </summary>
public interface IVisitGrain : IGrainWithStringKey
{
    Task<GrainStates.VisitState> GetVisitAsync();

    Task CreateVisitAsync(
        string patientId,
        DateTime visitDateTime,
        string serviceCategory,
        string? locationId,
        string? locationName,
        string? visitType,
        string? stopCode,
        string? secondaryStopCode,
        string? primaryProviderId,
        string? primaryProviderName,
        string? linkedAppointmentId,
        string? comments);

    Task CheckOutAsync(DateTime checkOutDateTime);

    Task AddEncounterDiagnosisAsync(
        string icd10Code,
        string description,
        bool isPrimary,
        string? providerId,
        string? providerName);

    Task AddProcedureAsync(
        string cptCode,
        string description,
        int quantity,
        string? modifiers,
        string? providerId,
        string? providerName);

    Task AddProviderAsync(
        string providerId,
        string providerName,
        string role,
        bool isPrimary);

    Task LinkNoteAsync(string noteId);

    Task CancelVisitAsync(string? reason);

    // ── GAP 7: Billing Treatment Factors ─────────────────────────────────────

    /// <summary>
    /// Sets CIDC billing treatment factors for the encounter.
    /// Mirrors VistA uPCE.pas / UBACore.pas treatment factor flags (SC/AO/IR/SWAC/MST/HNC/CV/SHAD/CL).
    /// </summary>
    Task SetTreatmentFactorsAsync(
        bool serviceConnected,
        bool agentOrange,
        bool ionizingRadiation,
        bool southwestAsia,
        bool mst,
        bool headNeckCancer,
        bool combatVeteran,
        bool shad,
        bool campLejeune);

    // ── GAP 9: Additional PCE Items ───────────────────────────────────────────

    /// <summary>
    /// Adds a health factor to the encounter — V HEALTH FACTORS, File #9000010.23.
    /// Mirrors VistA uPCE.pas TPCEItem hierarchy.
    /// </summary>
    Task AddHealthFactorAsync(
        string healthFactorName,
        string? healthFactorId,
        string? level,
        string? providerId,
        string? providerName);

    /// <summary>
    /// Adds an immunization to the encounter — V IMMUNIZATION, File #9000010.11.
    /// </summary>
    Task AddImmunizationAsync(
        string immunizationName,
        string? immunizationId,
        string? series,
        string? lotNumber,
        string? route,
        string? site,
        bool isContraindicated,
        string? providerId,
        string? providerName);

    /// <summary>
    /// Adds an exam to the encounter — V EXAM, File #9000010.13.
    /// </summary>
    Task AddExamAsync(
        string examName,
        string? examId,
        string? result,
        string? providerId,
        string? providerName);

    /// <summary>
    /// Adds a patient education topic to the encounter — V PATIENT ED, File #9000010.16.
    /// </summary>
    Task AddPatientEducationAsync(
        string topicName,
        string? topicId,
        string? levelOfUnderstanding,
        string? providerId,
        string? providerName);
}
