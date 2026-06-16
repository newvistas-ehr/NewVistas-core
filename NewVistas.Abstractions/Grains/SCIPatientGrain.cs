// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// SCI Patient Grain — manages a single patient's SCI/D registry record.
/// VistA SCI PATIENT file (#154).
/// </summary>
public class SCIPatientGrain : Grain, ISCIPatientGrain
{
    private readonly IPersistentState<SCIPatientState> _state;

    public SCIPatientGrain(
        [PersistentState("sciPatientState", "sciPatientStore")] IPersistentState<SCIPatientState> state)
    {
        _state = state;
    }

    public Task<SCIPatientState> GetAsync() => Task.FromResult(_state.State);

    public async Task EnrollAsync(
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
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.EnrollmentDate = enrollmentDate;
        _state.State.Status = SCIRegistryStatus.Active;
        _state.State.SCICenter = sciCenter;
        _state.State.DateOfInjuryOnset = dateOfInjuryOnset;
        _state.State.InjuryType = injuryType;
        _state.State.Etiology = etiology;
        _state.State.NeurologicalLevelOfInjury = neurologicalLevelOfInjury;
        _state.State.AisGrade = aisGrade;
        _state.State.PrimaryDiagnosisCode = primaryDiagnosisCode;
        _state.State.PrimaryDiagnosisDescription = primaryDiagnosisDescription;
        _state.State.EnrollingProviderId = enrollingProviderId;
        _state.State.EnrollingProviderName = enrollingProviderName;
        _state.State.PrimaryProviderId = primaryProviderId;
        _state.State.PrimaryProviderName = primaryProviderName;
        _state.State.BladderManagement = bladderManagement;
        _state.State.BowelProgram = bowelProgram;
        _state.State.LocomotionMethod = locomotionMethod;
        _state.State.LivingSituation = livingSituation;
        _state.State.AssociatedConditions = associatedConditions ?? new();
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateClinicalDataAsync(
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
        string? notes)
    {
        _state.State.NeurologicalLevelOfInjury = neurologicalLevelOfInjury;
        _state.State.AisGrade = aisGrade;
        _state.State.PrimaryDiagnosisCode = primaryDiagnosisCode;
        _state.State.PrimaryDiagnosisDescription = primaryDiagnosisDescription;
        _state.State.BladderManagement = bladderManagement;
        _state.State.BowelProgram = bowelProgram;
        _state.State.LocomotionMethod = locomotionMethod;
        _state.State.LivingSituation = livingSituation;
        if (associatedConditions is not null)
            _state.State.AssociatedConditions = associatedConditions;
        _state.State.PrimaryProviderId = primaryProviderId;
        _state.State.PrimaryProviderName = primaryProviderName;
        if (notes is not null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(SCIRegistryStatus status, string? notes)
    {
        _state.State.Status = status;
        if (notes is not null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<string> AddAnnualEncounterAsync(
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
        string? notes)
    {
        string encounterId = $"SCI-ENC:{Guid.NewGuid()}";

        var encounter = new SCIAnnualEncounterRecord
        {
            EncounterId = encounterId,
            FiscalYear = fiscalYear,
            EncounterDate = encounterDate,
            EncounterType = encounterType,
            AisGrade = aisGrade,
            NeurologicalLevel = neurologicalLevel,
            HospitalAdmissions = hospitalAdmissions,
            UrinaryTractInfections = urinaryTractInfections,
            PressureInjuryCount = pressureInjuryCount,
            HighestPressureInjuryStage = highestPressureInjuryStage,
            BladderManagement = bladderManagement,
            BowelProgram = bowelProgram,
            LivingSituation = livingSituation,
            EquipmentNeeds = equipmentNeeds ?? new(),
            ProviderId = providerId,
            ProviderName = providerName,
            Notes = notes,
            CreatedDate = DateTime.UtcNow
        };

        _state.State.AnnualEncounters.Add(encounter);

        // Update the top-level NLI and AIS grade to reflect the most recent encounter
        _state.State.NeurologicalLevelOfInjury = neurologicalLevel;
        _state.State.AisGrade = aisGrade;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return encounterId;
    }

    public Task<List<SCIAnnualEncounterRecord>> GetAnnualEncountersAsync()
        => Task.FromResult(_state.State.AnnualEncounters);
}
