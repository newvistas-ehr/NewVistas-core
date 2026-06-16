// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents one Anatomic Pathology case — Surgical Pathology, Cytology, or Autopsy.
/// Maps to VistA LAB DATA file (#63) subfiles #63.08, #63.09, #63.19.
/// MUMPS routines: LRAP.m, LRAPSC.m, LRAPACC.m, LRAPAU.m
/// Grain key pattern: "AP-CASE:{caseId}"
/// </summary>
public interface IAnatomicPathologyCaseGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this AP case.</summary>
    Task<AnatomicPathologyState> GetCaseAsync();

    /// <summary>
    /// Accessions a new AP case, creating the record with specimen and requester details.
    /// LRAPACC.m ACCESSION
    /// </summary>
    Task AccessionCaseAsync(
        string patientId,
        APCaseType caseType,
        string accessionNumber,
        string? specimenSource,
        string? specimenDescription,
        string? specimenType,
        string? clinicalHistory,
        string? clinicalDiagnosis,
        string? referringProviderId,
        string? referringProviderName,
        string? collectionLocation,
        DateTime? dateCollected,
        DateTime dateReceived);

    /// <summary>
    /// Records the gross (macroscopic) examination of the specimen.
    /// LRAPSC.m GROSS
    /// </summary>
    Task RecordGrossDescriptionAsync(
        string grossDescription,
        string? pathologistId,
        string? pathologistName,
        int? specimenPartCount,
        decimal? specimenWeightGrams,
        string? frozenSectionDiagnosis);

    /// <summary>
    /// Records the microscopic (histologic) description after slide review.
    /// LRAPSC.m MICRO
    /// </summary>
    Task RecordMicroscopicDescriptionAsync(string microscopicDescription);

    /// <summary>
    /// Issues the final signed-out diagnosis, transitioning status to Final.
    /// LRAP.m SIGNOUT
    /// </summary>
    Task SignOutDiagnosisAsync(
        string diagnosis,
        List<string> diagnosisCodes,
        string pathologistId,
        string pathologistName,
        DateTime signOutDateTime);

    /// <summary>
    /// Issues a preliminary diagnosis before full workup is complete.
    /// Status transitions to Preliminary.
    /// </summary>
    Task IssuePreliminaryDiagnosisAsync(
        string preliminaryDiagnosis,
        string pathologistId,
        string pathologistName);

    /// <summary>
    /// Appends an addendum to a final case (additional findings, ancillary results).
    /// Status transitions to Addendum.
    /// </summary>
    Task AddAddendumAsync(
        string addendumText,
        string pathologistId,
        string pathologistName);

    /// <summary>
    /// Amends (corrects) a previously signed-out diagnosis.
    /// Status transitions to Amended.
    /// </summary>
    Task AmendDiagnosisAsync(
        string correctedDiagnosis,
        List<string> correctedCodes,
        string amendmentReason,
        string pathologistId,
        string pathologistName);

    /// <summary>
    /// Adds a special stain or supplemental study to the case.
    /// </summary>
    Task AddSpecialStainAsync(string stainName);

    /// <summary>
    /// Records immunohistochemistry (IHC) panel results.
    /// </summary>
    Task AddImmunohistochemistryResultAsync(string ihcResult);

    /// <summary>
    /// Records cytology-specific fields: Bethesda category and specimen adequacy.
    /// </summary>
    Task RecordCytologyDetailsAsync(string? bethesdaCategory, string? specimenAdequacy);

    /// <summary>
    /// Records autopsy-specific findings.
    /// LRAPAU.m AUTOPSY
    /// </summary>
    Task RecordAutopsyFindingsAsync(
        string? causeOfDeath,
        string? underlyingCauseOfDeath,
        MannerOfDeath? mannerOfDeath,
        string? toxicologyFindings,
        decimal? bodyWeightKg,
        string? neuropathologyFindings);

    /// <summary>
    /// Adds free-text comments to the case.
    /// </summary>
    Task AddCommentsAsync(string comments);

    /// <summary>
    /// Cancels the case (unacceptable specimen, duplicate accession, etc.).
    /// </summary>
    Task CancelCaseAsync(string reason);
}
