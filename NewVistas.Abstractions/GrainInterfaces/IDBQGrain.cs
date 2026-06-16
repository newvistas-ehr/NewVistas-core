// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single Disability Benefits Questionnaire (DBQ) document.
/// Grain key: "CP-DBQ:{guid}"
/// DBQ forms are modern VA instruments (post-2010) linked to C&amp;P exams.
/// </summary>
public interface IDBQGrain : IGrainWithStringKey
{
    /// <summary>
    /// Creates the DBQ record with identification and claim linkage.
    /// Status → Draft.
    /// </summary>
    Task CreateDBQAsync(
        string examId,
        string patientId,
        string patientName,
        GrainStates.DBQType dbqType,
        string dbqFormNumber,
        string dbqTitle,
        string claimNumber,
        string conditionClaimed,
        string diagnosisCode,
        string diagnosisDescription);

    /// <summary>Updates the clinical narrative sections of the DBQ.</summary>
    Task UpdateSectionsAsync(
        string historySection,
        string symptomsSection,
        string functionalImpactSection,
        string rangeOfMotionSection,
        string mentalStatusSection,
        string diagnosticTestsSection);

    /// <summary>Records the examiner's nexus and service-connection opinion.</summary>
    Task RecordOpinionAsync(
        bool nexusOpinion,
        string nexusStatement,
        string opinionsSection,
        GrainStates.ServiceConnectionType serviceConnectionType,
        bool residualsPermanent,
        bool expectedImprovement);

    /// <summary>Sets the proposed disability rating percentage (0–100).</summary>
    Task SetProposedRatingAsync(int proposedRating);

    /// <summary>Marks the DBQ as clinically complete. Status → Completed.</summary>
    Task CompleteDBQAsync();

    /// <summary>Electronically signs the DBQ. Status → Signed.</summary>
    Task SignDBQAsync(string signedBy, DateTime signedDate);

    /// <summary>Returns the full state of this DBQ document.</summary>
    Task<GrainStates.DBQState> GetDBQAsync();
}
