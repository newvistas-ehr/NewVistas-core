// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Spinal Cord Injury / Dysfunction (SCI/D) grain layer.
/// VistA SCI PATIENT file (#154).
/// Tests SCIPatientGrain, SCIIndexGrain, and PatientWorkflowGrain SCI methods.
/// </summary>
[TestFixture]
public class SCIGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── SCIPatientGrain — enrollment ──────────────────────────────────────────

    [Test]
    public async Task SCIPatientGrain_Enroll_PersistsAllFields()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        DateTime enrollDate    = new DateTime(2024, 3, 15);
        DateTime injuryDate    = new DateTime(2015, 7, 4);

        await grain.EnrollAsync(
            patientId:                    "PATIENT-001",
            enrollmentDate:               enrollDate,
            sciCenter:                    "Memphis VAMC SCI Center",
            dateOfInjuryOnset:            injuryDate,
            injuryType:                   SCIInjuryType.Traumatic,
            etiology:                     SCIEtiology.MotorVehicleAccident,
            neurologicalLevelOfInjury:    "C5",
            aisGrade:                     SCIAisGrade.B,
            primaryDiagnosisCode:         "S14.105A",
            primaryDiagnosisDescription:  "Unspecified injury at C5 level of cervical spinal cord",
            enrollingProviderId:          "PROV-001",
            enrollingProviderName:        "Dr. A. Patel",
            primaryProviderId:            "PROV-002",
            primaryProviderName:          "Dr. B. Nguyen",
            bladderManagement:            SCIBladderManagement.IntermittentCatheterization,
            bowelProgram:                 SCIBowelProgram.DigitalStimulation,
            locomotionMethod:             SCILocomotionMethod.ManualWheelchair,
            livingSituation:              SCILivingSituation.PrivateHome,
            associatedConditions:         new List<string> { "Neurogenic bladder", "Spasticity" },
            notes:                        "MVA 2015, complete initial workup.");

        SCIPatientState state = await grain.GetAsync();

        Assert.That(state.PatientId,                    Is.EqualTo("PATIENT-001"));
        Assert.That(state.EnrollmentDate,               Is.EqualTo(enrollDate));
        Assert.That(state.Status,                       Is.EqualTo(SCIRegistryStatus.Active));
        Assert.That(state.SCICenter,                    Is.EqualTo("Memphis VAMC SCI Center"));
        Assert.That(state.DateOfInjuryOnset,            Is.EqualTo(injuryDate));
        Assert.That(state.InjuryType,                   Is.EqualTo(SCIInjuryType.Traumatic));
        Assert.That(state.Etiology,                     Is.EqualTo(SCIEtiology.MotorVehicleAccident));
        Assert.That(state.NeurologicalLevelOfInjury,    Is.EqualTo("C5"));
        Assert.That(state.AisGrade,                     Is.EqualTo(SCIAisGrade.B));
        Assert.That(state.PrimaryDiagnosisCode,         Is.EqualTo("S14.105A"));
        Assert.That(state.BladderManagement,            Is.EqualTo(SCIBladderManagement.IntermittentCatheterization));
        Assert.That(state.BowelProgram,                 Is.EqualTo(SCIBowelProgram.DigitalStimulation));
        Assert.That(state.LocomotionMethod,             Is.EqualTo(SCILocomotionMethod.ManualWheelchair));
        Assert.That(state.LivingSituation,              Is.EqualTo(SCILivingSituation.PrivateHome));
        Assert.That(state.AssociatedConditions,         Has.Count.EqualTo(2));
        Assert.That(state.AssociatedConditions,         Does.Contain("Spasticity"));
        Assert.That(state.EnrollingProviderName,        Is.EqualTo("Dr. A. Patel"));
        Assert.That(state.PrimaryProviderName,          Is.EqualTo("Dr. B. Nguyen"));
        Assert.That(state.Notes,                        Does.Contain("MVA 2015"));
    }

    [Test]
    public async Task SCIPatientGrain_Enroll_NonTraumatic_PersistsEtiology()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            patientId:                    "PATIENT-002",
            enrollmentDate:               DateTime.UtcNow,
            sciCenter:                    null,
            dateOfInjuryOnset:            new DateTime(2020, 1, 1),
            injuryType:                   SCIInjuryType.NonTraumatic,
            etiology:                     SCIEtiology.Tumor,
            neurologicalLevelOfInjury:    "T4",
            aisGrade:                     SCIAisGrade.A,
            primaryDiagnosisCode:         null,
            primaryDiagnosisDescription:  null,
            enrollingProviderId:          null,
            enrollingProviderName:        null,
            primaryProviderId:            null,
            primaryProviderName:          null,
            bladderManagement:            SCIBladderManagement.IndwellingUrethralCatheter,
            bowelProgram:                 null,
            locomotionMethod:             SCILocomotionMethod.PowerWheelchair,
            livingSituation:              null,
            associatedConditions:         null,
            notes:                        null);

        SCIPatientState state = await grain.GetAsync();

        Assert.That(state.InjuryType,                Is.EqualTo(SCIInjuryType.NonTraumatic));
        Assert.That(state.Etiology,                  Is.EqualTo(SCIEtiology.Tumor));
        Assert.That(state.NeurologicalLevelOfInjury, Is.EqualTo("T4"));
        Assert.That(state.AisGrade,                  Is.EqualTo(SCIAisGrade.A));
        Assert.That(state.LocomotionMethod,          Is.EqualTo(SCILocomotionMethod.PowerWheelchair));
        Assert.That(state.AssociatedConditions,      Is.Empty);
    }

    // ── SCIPatientGrain — update clinical data ────────────────────────────────

    [Test]
    public async Task SCIPatientGrain_UpdateClinicalData_ReflectsChanges()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            "PATIENT-003", DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Fall,
            "T10", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await grain.UpdateClinicalDataAsync(
            neurologicalLevelOfInjury:   "T10",
            aisGrade:                    SCIAisGrade.D,
            primaryDiagnosisCode:        "S24.104A",
            primaryDiagnosisDescription: "Incomplete lesion T9-T10",
            bladderManagement:           SCIBladderManagement.SpontaneousVoiding,
            bowelProgram:                SCIBowelProgram.NaturalSpontaneous,
            locomotionMethod:            SCILocomotionMethod.AmbulatoryWithDevice,
            livingSituation:             SCILivingSituation.PrivateHome,
            associatedConditions:        new List<string> { "Recovered motor function" },
            primaryProviderId:           "PROV-010",
            primaryProviderName:         "Dr. C. Kim",
            notes:                       "Significant motor recovery post-rehabilitation.");

        SCIPatientState state = await grain.GetAsync();

        Assert.That(state.AisGrade,               Is.EqualTo(SCIAisGrade.D));
        Assert.That(state.PrimaryDiagnosisCode,   Is.EqualTo("S24.104A"));
        Assert.That(state.BladderManagement,      Is.EqualTo(SCIBladderManagement.SpontaneousVoiding));
        Assert.That(state.LocomotionMethod,       Is.EqualTo(SCILocomotionMethod.AmbulatoryWithDevice));
        Assert.That(state.PrimaryProviderName,    Is.EqualTo("Dr. C. Kim"));
        Assert.That(state.AssociatedConditions,   Does.Contain("Recovered motor function"));
    }

    // ── SCIPatientGrain — status ──────────────────────────────────────────────

    [Test]
    public async Task SCIPatientGrain_UpdateStatus_ChangesRegistryStatus()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            "PATIENT-004", DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Violence,
            "C7", SCIAisGrade.C,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(SCIRegistryStatus.Active));

        await grain.UpdateStatusAsync(SCIRegistryStatus.Transferred, "Patient relocated to another VAMC.");

        SCIPatientState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(SCIRegistryStatus.Transferred));
        Assert.That(state.Notes,  Does.Contain("relocated"));
    }

    [Test]
    public async Task SCIPatientGrain_UpdateStatus_Deceased_SetsDeceasedStatus()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            "PATIENT-005", DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Sports,
            "C6", SCIAisGrade.B,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await grain.UpdateStatusAsync(SCIRegistryStatus.Deceased, null);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(SCIRegistryStatus.Deceased));
    }

    // ── SCIPatientGrain — annual encounters ───────────────────────────────────

    [Test]
    public async Task SCIPatientGrain_AddAnnualEncounter_PersistsAllFields()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            "PATIENT-006", DateTime.UtcNow, "Memphis VAMC", null,
            SCIInjuryType.Traumatic, SCIEtiology.Fall,
            "L2", SCIAisGrade.D,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        string encId = await grain.AddAnnualEncounterAsync(
            fiscalYear:                 2025,
            encounterDate:              new DateTime(2025, 2, 10),
            encounterType:              SCIEncounterType.Annual,
            aisGrade:                   SCIAisGrade.D,
            neurologicalLevel:          "L2",
            hospitalAdmissions:         1,
            urinaryTractInfections:     2,
            pressureInjuryCount:        0,
            highestPressureInjuryStage: 0,
            bladderManagement:          SCIBladderManagement.IntermittentCatheterization,
            bowelProgram:               SCIBowelProgram.Suppository,
            livingSituation:            SCILivingSituation.PrivateHome,
            equipmentNeeds:             new List<string> { "Power wheelchair", "Hand controls for vehicle" },
            providerId:                 "PROV-003",
            providerName:               "Dr. D. Okonkwo",
            notes:                      "Stable. Skin intact. UTIs managed.");

        Assert.That(encId, Does.StartWith("SCI-ENC:"));

        List<SCIAnnualEncounterRecord> encounters = await grain.GetAnnualEncountersAsync();
        Assert.That(encounters, Has.Count.EqualTo(1));

        SCIAnnualEncounterRecord enc = encounters[0];
        Assert.That(enc.EncounterId,                Is.EqualTo(encId));
        Assert.That(enc.FiscalYear,                 Is.EqualTo(2025));
        Assert.That(enc.EncounterType,              Is.EqualTo(SCIEncounterType.Annual));
        Assert.That(enc.AisGrade,                   Is.EqualTo(SCIAisGrade.D));
        Assert.That(enc.NeurologicalLevel,          Is.EqualTo("L2"));
        Assert.That(enc.HospitalAdmissions,         Is.EqualTo(1));
        Assert.That(enc.UrinaryTractInfections,     Is.EqualTo(2));
        Assert.That(enc.PressureInjuryCount,        Is.EqualTo(0));
        Assert.That(enc.EquipmentNeeds,             Has.Count.EqualTo(2));
        Assert.That(enc.EquipmentNeeds,             Does.Contain("Power wheelchair"));
        Assert.That(enc.ProviderName,               Is.EqualTo("Dr. D. Okonkwo"));
        Assert.That(enc.Notes,                      Does.Contain("Stable"));
    }

    [Test]
    public async Task SCIPatientGrain_AddMultipleEncounters_UpdatesNliAndAis()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            "PATIENT-007", DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.MotorVehicleAccident,
            "C6", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        // FY2023 — initial AIS A
        await grain.AddAnnualEncounterAsync(
            2023, new DateTime(2023, 10, 5), SCIEncounterType.Annual,
            SCIAisGrade.A, "C6", 2, 3, 1, 2, null, null, null, null, null, null, null);

        // FY2024 — improved to AIS C
        await grain.AddAnnualEncounterAsync(
            2024, new DateTime(2024, 10, 8), SCIEncounterType.Annual,
            SCIAisGrade.C, "C6", 0, 1, 0, 0, null, null, null, null, null, null,
            "Improved motor function following FES therapy.");

        List<SCIAnnualEncounterRecord> encounters = await grain.GetAnnualEncountersAsync();
        Assert.That(encounters, Has.Count.EqualTo(2));

        // Top-level NLI/AIS should reflect most recent encounter
        SCIPatientState state = await grain.GetAsync();
        Assert.That(state.AisGrade,                  Is.EqualTo(SCIAisGrade.C));
        Assert.That(state.NeurologicalLevelOfInjury, Is.EqualTo("C6"));
    }

    [Test]
    public async Task SCIPatientGrain_AddEncounter_ProblemFocused_PersistsType()
    {
        string key = $"SCI-PATIENT:{Guid.NewGuid()}";
        ISCIPatientGrain grain = _cluster.GrainFactory.GetGrain<ISCIPatientGrain>(key);

        await grain.EnrollAsync(
            "PATIENT-008", DateTime.UtcNow, null, null,
            SCIInjuryType.NonTraumatic, SCIEtiology.Vascular,
            "T8", SCIAisGrade.B,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        string encId = await grain.AddAnnualEncounterAsync(
            2025, DateTime.UtcNow, SCIEncounterType.ProblemFocused,
            SCIAisGrade.B, "T8", 0, 0, 1, 2, null, null, null, null,
            "PROV-004", "Dr. E. Rivera", "Stage 2 pressure injury, right ischium.");

        List<SCIAnnualEncounterRecord> encounters = await grain.GetAnnualEncountersAsync();
        Assert.That(encounters[0].EncounterType,           Is.EqualTo(SCIEncounterType.ProblemFocused));
        Assert.That(encounters[0].PressureInjuryCount,     Is.EqualTo(1));
        Assert.That(encounters[0].HighestPressureInjuryStage, Is.EqualTo(2));
    }

    // ── SCIIndexGrain ─────────────────────────────────────────────────────────

    [Test]
    public async Task SCIIndexGrain_AddEntry_IdempotentForSamePatient()
    {
        ISCIIndexGrain index = _cluster.GrainFactory.GetGrain<ISCIIndexGrain>($"SCI-IDX-TEST:{Guid.NewGuid()}");

        SCIIndexEntry entry = new SCIIndexEntry
        {
            PatientId          = "PAT-IDX-001",
            EnrollmentDate     = DateTime.UtcNow,
            Status             = SCIRegistryStatus.Active,
            NeurologicalLevel  = "C5",
            AisGrade           = SCIAisGrade.B,
            SCICenter          = "Memphis VAMC",
            EnrollingProviderName = "Dr. A. Patel",
            InjuryType         = SCIInjuryType.Traumatic
        };

        await index.AddEntryAsync(entry);
        await index.AddEntryAsync(entry); // duplicate call — should be ignored

        List<SCIIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SCIIndexGrain_GetByStatus_FiltersCorrectly()
    {
        ISCIIndexGrain index = _cluster.GrainFactory.GetGrain<ISCIIndexGrain>($"SCI-IDX-TEST:{Guid.NewGuid()}");

        await index.AddEntryAsync(new SCIIndexEntry
        {
            PatientId = "PAT-IDX-002", Status = SCIRegistryStatus.Active,
            NeurologicalLevel = "C4", AisGrade = SCIAisGrade.A,
            EnrollmentDate = DateTime.UtcNow, InjuryType = SCIInjuryType.Traumatic
        });

        await index.AddEntryAsync(new SCIIndexEntry
        {
            PatientId = "PAT-IDX-003", Status = SCIRegistryStatus.Inactive,
            NeurologicalLevel = "T6", AisGrade = SCIAisGrade.C,
            EnrollmentDate = DateTime.UtcNow, InjuryType = SCIInjuryType.NonTraumatic
        });

        await index.AddEntryAsync(new SCIIndexEntry
        {
            PatientId = "PAT-IDX-004", Status = SCIRegistryStatus.Deceased,
            NeurologicalLevel = "L1", AisGrade = SCIAisGrade.D,
            EnrollmentDate = DateTime.UtcNow, InjuryType = SCIInjuryType.Traumatic
        });

        List<SCIIndexEntry> active = await index.GetByStatusAsync(SCIRegistryStatus.Active);
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].PatientId, Is.EqualTo("PAT-IDX-002"));

        List<SCIIndexEntry> inactive = await index.GetByStatusAsync(SCIRegistryStatus.Inactive);
        Assert.That(inactive, Has.Count.EqualTo(1));
        Assert.That(inactive[0].PatientId, Is.EqualTo("PAT-IDX-003"));
    }

    [Test]
    public async Task SCIIndexGrain_GetByNeurologicalLevel_PrefixMatchesCervical()
    {
        ISCIIndexGrain index = _cluster.GrainFactory.GetGrain<ISCIIndexGrain>($"SCI-IDX-TEST:{Guid.NewGuid()}");

        foreach ((string level, string patId) in new[] {
            ("C3", "P-C3"), ("C6", "P-C6"), ("T4", "P-T4"), ("L2", "P-L2")
        })
        {
            await index.AddEntryAsync(new SCIIndexEntry
            {
                PatientId = patId, Status = SCIRegistryStatus.Active,
                NeurologicalLevel = level, AisGrade = SCIAisGrade.A,
                EnrollmentDate = DateTime.UtcNow, InjuryType = SCIInjuryType.Traumatic
            });
        }

        List<SCIIndexEntry> cervical = await index.GetByNeurologicalLevelAsync("C");
        Assert.That(cervical, Has.Count.EqualTo(2));
        Assert.That(cervical.Select(e => e.PatientId), Does.Contain("P-C3"));
        Assert.That(cervical.Select(e => e.PatientId), Does.Contain("P-C6"));

        List<SCIIndexEntry> thoracic = await index.GetByNeurologicalLevelAsync("T");
        Assert.That(thoracic, Has.Count.EqualTo(1));
        Assert.That(thoracic[0].PatientId, Is.EqualTo("P-T4"));

        List<SCIIndexEntry> lumbar = await index.GetByNeurologicalLevelAsync("L");
        Assert.That(lumbar, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SCIIndexGrain_UpdateEntry_ReflectsNewStatusAndAis()
    {
        ISCIIndexGrain index = _cluster.GrainFactory.GetGrain<ISCIIndexGrain>($"SCI-IDX-TEST:{Guid.NewGuid()}");

        await index.AddEntryAsync(new SCIIndexEntry
        {
            PatientId = "PAT-IDX-010", Status = SCIRegistryStatus.Active,
            NeurologicalLevel = "C5", AisGrade = SCIAisGrade.A,
            EnrollmentDate = DateTime.UtcNow, InjuryType = SCIInjuryType.Traumatic
        });

        await index.UpdateEntryAsync("PAT-IDX-010", SCIRegistryStatus.Transferred, "C5", SCIAisGrade.C);

        List<SCIIndexEntry> all = await index.GetAllAsync();
        SCIIndexEntry updated = all.First(e => e.PatientId == "PAT-IDX-010");
        Assert.That(updated.Status,  Is.EqualTo(SCIRegistryStatus.Transferred));
        Assert.That(updated.AisGrade, Is.EqualTo(SCIAisGrade.C));
    }

    [Test]
    public async Task SCIIndexGrain_UpdateEntry_MissingPatient_DoesNotThrow()
    {
        ISCIIndexGrain index = _cluster.GrainFactory.GetGrain<ISCIIndexGrain>($"SCI-IDX-TEST:{Guid.NewGuid()}");

        // Updating non-existent patient should not throw
        Assert.DoesNotThrowAsync(async () =>
            await index.UpdateEntryAsync("NON-EXISTENT", SCIRegistryStatus.Inactive, "C5", SCIAisGrade.A));
    }

    // ── Workflow grain integration ─────────────────────────────────────────────

    [Test]
    public async Task WorkflowGrain_EnrollAndGetSCIPatient_RoundTripsCorrectly()
    {
        string patientId = $"PATIENT-SCI-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        DateTime enrollDate = new DateTime(2024, 5, 20);

        await workflow.EnrollInSCIRegistryAsync(
            enrollmentDate:               enrollDate,
            sciCenter:                    "Houston SCI Center",
            dateOfInjuryOnset:            new DateTime(2018, 3, 12),
            injuryType:                   SCIInjuryType.Traumatic,
            etiology:                     SCIEtiology.Fall,
            neurologicalLevelOfInjury:    "T6",
            aisGrade:                     SCIAisGrade.A,
            primaryDiagnosisCode:         "S24.101A",
            primaryDiagnosisDescription:  "T5-T7 complete SCI",
            enrollingProviderId:          null,
            enrollingProviderName:        "Dr. F. Johnson",
            primaryProviderId:            null,
            primaryProviderName:          "Dr. G. Lee",
            bladderManagement:            SCIBladderManagement.IntermittentCatheterization,
            bowelProgram:                 SCIBowelProgram.DigitalStimulation,
            locomotionMethod:             SCILocomotionMethod.ManualWheelchair,
            livingSituation:              SCILivingSituation.PrivateHome,
            associatedConditions:         new List<string> { "Neurogenic pain" },
            notes:                        "Fall from scaffolding 2018.");

        SCIPatientState state = await workflow.GetSCIPatientAsync();

        Assert.That(state.PatientId,                    Is.EqualTo(patientId));
        Assert.That(state.Status,                       Is.EqualTo(SCIRegistryStatus.Active));
        Assert.That(state.SCICenter,                    Is.EqualTo("Houston SCI Center"));
        Assert.That(state.NeurologicalLevelOfInjury,    Is.EqualTo("T6"));
        Assert.That(state.AisGrade,                     Is.EqualTo(SCIAisGrade.A));
        Assert.That(state.EnrollingProviderName,        Is.EqualTo("Dr. F. Johnson"));
        Assert.That(state.AssociatedConditions,         Does.Contain("Neurogenic pain"));
    }

    [Test]
    public async Task WorkflowGrain_AddAnnualEncounter_ReturnEncounterIdAndUpdatesState()
    {
        string patientId = $"PATIENT-SCI-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.NonTraumatic, SCIEtiology.Disease,
            "C8", SCIAisGrade.B,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        string encId = await workflow.AddSCIAnnualEncounterAsync(
            fiscalYear:                 2025,
            encounterDate:              new DateTime(2025, 1, 15),
            encounterType:              SCIEncounterType.Annual,
            aisGrade:                   SCIAisGrade.C,
            neurologicalLevel:          "C8",
            hospitalAdmissions:         0,
            urinaryTractInfections:     1,
            pressureInjuryCount:        0,
            highestPressureInjuryStage: 0,
            bladderManagement:          null,
            bowelProgram:               null,
            livingSituation:            null,
            equipmentNeeds:             null,
            providerId:                 null,
            providerName:               "Dr. H. Adams",
            notes:                      "Mild motor improvement.");

        Assert.That(encId, Does.StartWith("SCI-ENC:"));

        List<SCIAnnualEncounterRecord> encounters = await workflow.GetSCIAnnualEncountersAsync();
        Assert.That(encounters, Has.Count.EqualTo(1));
        Assert.That(encounters[0].EncounterId,  Is.EqualTo(encId));
        Assert.That(encounters[0].FiscalYear,   Is.EqualTo(2025));
        Assert.That(encounters[0].AisGrade,     Is.EqualTo(SCIAisGrade.C));
        Assert.That(encounters[0].ProviderName, Is.EqualTo("Dr. H. Adams"));

        // Most-recent NLI/AIS should be updated on the patient record
        SCIPatientState state = await workflow.GetSCIPatientAsync();
        Assert.That(state.AisGrade, Is.EqualTo(SCIAisGrade.C));
    }

    [Test]
    public async Task WorkflowGrain_UpdateSCIStatus_PropagatesStatusChange()
    {
        string patientId = $"PATIENT-SCI-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Violence,
            "C4", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await workflow.UpdateSCIStatusAsync(SCIRegistryStatus.Inactive, "Relocated out of VA system.");

        SCIPatientState state = await workflow.GetSCIPatientAsync();
        Assert.That(state.Status, Is.EqualTo(SCIRegistryStatus.Inactive));
    }

    [Test]
    public async Task WorkflowGrain_UpdateSCIPatient_ChangesBladderAndLocomotion()
    {
        string patientId = $"PATIENT-SCI-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.MotorVehicleAccident,
            "L4", SCIAisGrade.C,
            null, null, null, null, null, null,
            SCIBladderManagement.IndwellingUrethralCatheter,
            SCIBowelProgram.ManualEvacuation,
            SCILocomotionMethod.PowerWheelchair,
            null, null, null);

        await workflow.UpdateSCIPatientAsync(
            neurologicalLevelOfInjury:   "L4",
            aisGrade:                    SCIAisGrade.D,
            primaryDiagnosisCode:        null,
            primaryDiagnosisDescription: null,
            bladderManagement:           SCIBladderManagement.IntermittentCatheterization,
            bowelProgram:                SCIBowelProgram.NaturalSpontaneous,
            locomotionMethod:            SCILocomotionMethod.AmbulatoryWithDevice,
            livingSituation:             SCILivingSituation.PrivateHome,
            associatedConditions:        null,
            primaryProviderId:           null,
            primaryProviderName:         null,
            notes:                       null);

        SCIPatientState state = await workflow.GetSCIPatientAsync();
        Assert.That(state.AisGrade,          Is.EqualTo(SCIAisGrade.D));
        Assert.That(state.BladderManagement, Is.EqualTo(SCIBladderManagement.IntermittentCatheterization));
        Assert.That(state.LocomotionMethod,  Is.EqualTo(SCILocomotionMethod.AmbulatoryWithDevice));
    }
}
