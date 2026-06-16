// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// SCI Patient Grain — manages a single patient's SCI/D registry record.
///
/// VistA SCI PATIENT file (#154).
/// MUMPS routines: SCIRPAU.m (registry patient add/update), SCIRPIV.m (patient inquiry view).
///
/// Grain key: "SCI-PATIENT:{patientId}"
/// </summary>
public interface ISCIPatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the full SCI registry record for this patient.</summary>
    Task<SCIPatientState> GetAsync();

    /// <summary>
    /// Enrolls the patient in the SCI/D registry.
    /// Sets status to Active and initializes the clinical profile.
    /// </summary>
    Task EnrollAsync(
        string patientId,
        DateTime enrollmentDate,
        string? sciCenter,
        DateTime? dateOfInjuryOnset,
        SCIInjuryType injuryType,
        SCIEtiology etiology,
        string neurologicalLevelOfInjury,
        SCIAisGrade aisGrade,
        string? primaryDiagnosisCode,
        string? primaryDiagnosisDescription,
        string? enrollingProviderId,
        string? enrollingProviderName,
        string? primaryProviderId,
        string? primaryProviderName,
        SCIBladderManagement? bladderManagement,
        SCIBowelProgram? bowelProgram,
        SCILocomotionMethod? locomotionMethod,
        SCILivingSituation? livingSituation,
        List<string>? associatedConditions,
        string? notes);

    /// <summary>
    /// Updates the clinical profile of the SCI registry record (injury data, management, providers).
    /// </summary>
    Task UpdateClinicalDataAsync(
        string neurologicalLevelOfInjury,
        SCIAisGrade aisGrade,
        string? primaryDiagnosisCode,
        string? primaryDiagnosisDescription,
        SCIBladderManagement? bladderManagement,
        SCIBowelProgram? bowelProgram,
        SCILocomotionMethod? locomotionMethod,
        SCILivingSituation? livingSituation,
        List<string>? associatedConditions,
        string? primaryProviderId,
        string? primaryProviderName,
        string? notes);

    /// <summary>Updates the registry enrollment status (e.g., Active → Inactive or Deceased).</summary>
    Task UpdateStatusAsync(SCIRegistryStatus status, string? notes);

    /// <summary>
    /// Adds an annual review or follow-up encounter record.
    /// Returns the new encounter ID.
    /// </summary>
    Task<string> AddAnnualEncounterAsync(
        int fiscalYear,
        DateTime encounterDate,
        SCIEncounterType encounterType,
        SCIAisGrade aisGrade,
        string neurologicalLevel,
        int hospitalAdmissions,
        int urinaryTractInfections,
        int pressureInjuryCount,
        int highestPressureInjuryStage,
        SCIBladderManagement? bladderManagement,
        SCIBowelProgram? bowelProgram,
        SCILivingSituation? livingSituation,
        List<string>? equipmentNeeds,
        string? providerId,
        string? providerName,
        string? notes);

    /// <summary>Returns all annual encounter records for this patient.</summary>
    Task<List<SCIAnnualEncounterRecord>> GetAnnualEncountersAsync();
}
