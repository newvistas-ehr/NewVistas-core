// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Spinal Cord Injury / Dysfunction Registry — VistA File #154.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class SpinalCordInjuryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Enrollment ────────────────────────────────────────────────────────────

    [Test]
    public async Task EnrollInSCIRegistry_CreatesPatientRecord()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            enrollmentDate: new DateTime(2024, 1, 15),
            sciCenter: "Tampa SCI Center",
            dateOfInjuryOnset: new DateTime(2020, 6, 10),
            injuryType: SCIInjuryType.Traumatic,
            etiology: SCIEtiology.MotorVehicleAccident,
            neurologicalLevelOfInjury: "C5",
            aisGrade: SCIAisGrade.B,
            primaryDiagnosisCode: "S14.105A",
            primaryDiagnosisDescription: "Complete lesion of cervical spinal cord, C5",
            enrollingProviderId: "PROV-001",
            enrollingProviderName: "Dr. SCI Specialist",
            primaryProviderId: "PROV-002",
            primaryProviderName: "Dr. Primary SCI",
            bladderManagement: SCIBladderManagement.IntermittentCatheterization,
            bowelProgram: SCIBowelProgram.DigitalStimulation,
            locomotionMethod: SCILocomotionMethod.PowerWheelchair,
            livingSituation: SCILivingSituation.PrivateHome,
            associatedConditions: new List<string> { "Neurogenic bladder", "Spasticity" },
            notes: "Enrolled from inpatient rehabilitation");

        SCIPatientState state = await wf.GetSCIPatientAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.SCICenter, Is.EqualTo("Tampa SCI Center"));
        Assert.That(state.InjuryType, Is.EqualTo(SCIInjuryType.Traumatic));
        Assert.That(state.Etiology, Is.EqualTo(SCIEtiology.MotorVehicleAccident));
        Assert.That(state.NeurologicalLevelOfInjury, Is.EqualTo("C5"));
        Assert.That(state.AisGrade, Is.EqualTo(SCIAisGrade.B));
        Assert.That(state.BladderManagement, Is.EqualTo(SCIBladderManagement.IntermittentCatheterization));
        Assert.That(state.BowelProgram, Is.EqualTo(SCIBowelProgram.DigitalStimulation));
        Assert.That(state.LocomotionMethod, Is.EqualTo(SCILocomotionMethod.PowerWheelchair));
        Assert.That(state.LivingSituation, Is.EqualTo(SCILivingSituation.PrivateHome));
        Assert.That(state.AssociatedConditions, Contains.Item("Neurogenic bladder"));
        Assert.That(state.AssociatedConditions, Contains.Item("Spasticity"));
        Assert.That(state.Status, Is.EqualTo(SCIRegistryStatus.Active));
    }

    [Test]
    public async Task EnrollInSCIRegistry_NonTraumaticInjury()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            enrollmentDate: DateTime.UtcNow,
            sciCenter: "Richmond SCI Center",
            dateOfInjuryOnset: new DateTime(2022, 3, 1),
            injuryType: SCIInjuryType.NonTraumatic,
            etiology: SCIEtiology.Tumor,
            neurologicalLevelOfInjury: "T8",
            aisGrade: SCIAisGrade.C,
            primaryDiagnosisCode: "G95.89",
            primaryDiagnosisDescription: "Other specified diseases of spinal cord",
            enrollingProviderId: null,
            enrollingProviderName: null,
            primaryProviderId: null,
            primaryProviderName: null,
            bladderManagement: null,
            bowelProgram: null,
            locomotionMethod: SCILocomotionMethod.ManualWheelchair,
            livingSituation: null,
            associatedConditions: null,
            notes: null);

        SCIPatientState state = await wf.GetSCIPatientAsync();
        Assert.That(state.InjuryType, Is.EqualTo(SCIInjuryType.NonTraumatic));
        Assert.That(state.Etiology, Is.EqualTo(SCIEtiology.Tumor));
        Assert.That(state.NeurologicalLevelOfInjury, Is.EqualTo("T8"));
        Assert.That(state.AisGrade, Is.EqualTo(SCIAisGrade.C));
    }

    // ── Update clinical data ──────────────────────────────────────────────────

    [Test]
    public async Task UpdateSCIPatient_ChangesClinicalData()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, "SCI Center", new DateTime(2021, 1, 1),
            SCIInjuryType.Traumatic, SCIEtiology.Fall,
            "T10", SCIAisGrade.A,
            null, null, null, null, null, null,
            SCIBladderManagement.IndwellingUrethralCatheter,
            SCIBowelProgram.Suppository,
            SCILocomotionMethod.ManualWheelchair,
            SCILivingSituation.PrivateHome,
            null, null);

        await wf.UpdateSCIPatientAsync(
            neurologicalLevelOfInjury: "T10",
            aisGrade: SCIAisGrade.B,
            primaryDiagnosisCode: "S24.104A",
            primaryDiagnosisDescription: "Complete lesion of thoracic spinal cord",
            bladderManagement: SCIBladderManagement.IntermittentCatheterization,
            bowelProgram: SCIBowelProgram.DigitalStimulation,
            locomotionMethod: SCILocomotionMethod.ManualWheelchair,
            livingSituation: SCILivingSituation.AssistedLiving,
            associatedConditions: new List<string> { "Autonomic dysreflexia", "Pressure injury" },
            primaryProviderId: "PROV-010",
            primaryProviderName: "Dr. Updated",
            notes: "Reclassified after recent exam");

        SCIPatientState state = await wf.GetSCIPatientAsync();
        Assert.That(state.AisGrade, Is.EqualTo(SCIAisGrade.B));
        Assert.That(state.BladderManagement, Is.EqualTo(SCIBladderManagement.IntermittentCatheterization));
        Assert.That(state.LivingSituation, Is.EqualTo(SCILivingSituation.AssistedLiving));
        Assert.That(state.AssociatedConditions, Contains.Item("Autonomic dysreflexia"));
        Assert.That(state.PrimaryProviderName, Is.EqualTo("Dr. Updated"));
    }

    // ── Status changes ────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateSCIStatus_TransitionsToInactive()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Violence,
            "C7", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await wf.UpdateSCIStatusAsync(SCIRegistryStatus.Inactive, "Patient relocated out of catchment area");

        SCIPatientState state = await wf.GetSCIPatientAsync();
        Assert.That(state.Status, Is.EqualTo(SCIRegistryStatus.Inactive));
    }

    [Test]
    public async Task UpdateSCIStatus_TransitionsToTransferred()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, "Palo Alto SCI", null,
            SCIInjuryType.NonTraumatic, SCIEtiology.Degenerative,
            "L2", SCIAisGrade.D,
            null, null, null, null, null, null,
            null, null,
            SCILocomotionMethod.AmbulatoryWithDevice,
            null, null, null);

        await wf.UpdateSCIStatusAsync(SCIRegistryStatus.Transferred, "Transferred to Long Beach SCI");

        SCIPatientState state = await wf.GetSCIPatientAsync();
        Assert.That(state.Status, Is.EqualTo(SCIRegistryStatus.Transferred));
    }

    // ── Annual encounters ─────────────────────────────────────────────────────

    [Test]
    public async Task AddAnnualEncounter_ReturnsId_AndAppearsInList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, "Memphis SCI", new DateTime(2019, 5, 1),
            SCIInjuryType.Traumatic, SCIEtiology.Sports,
            "C6", SCIAisGrade.B,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        string encounterId = await wf.AddSCIAnnualEncounterAsync(
            fiscalYear: 2025,
            encounterDate: new DateTime(2024, 10, 15),
            encounterType: SCIEncounterType.Annual,
            aisGrade: SCIAisGrade.C,
            neurologicalLevel: "C6",
            hospitalAdmissions: 1,
            urinaryTractInfections: 2,
            pressureInjuryCount: 0,
            highestPressureInjuryStage: 0,
            bladderManagement: SCIBladderManagement.IntermittentCatheterization,
            bowelProgram: SCIBowelProgram.DigitalStimulation,
            livingSituation: SCILivingSituation.PrivateHome,
            equipmentNeeds: new List<string> { "Power wheelchair replacement", "Shower chair" },
            providerId: "PROV-020",
            providerName: "Dr. Annual Reviewer",
            notes: "Improvement noted — motor incomplete");

        Assert.That(encounterId, Is.Not.Null.And.Not.Empty);

        List<SCIAnnualEncounterRecord> encounters = await wf.GetSCIAnnualEncountersAsync();
        Assert.That(encounters, Has.Count.EqualTo(1));
        Assert.That(encounters[0].FiscalYear, Is.EqualTo(2025));
        Assert.That(encounters[0].EncounterType, Is.EqualTo(SCIEncounterType.Annual));
        Assert.That(encounters[0].AisGrade, Is.EqualTo(SCIAisGrade.C));
        Assert.That(encounters[0].HospitalAdmissions, Is.EqualTo(1));
        Assert.That(encounters[0].UrinaryTractInfections, Is.EqualTo(2));
        Assert.That(encounters[0].EquipmentNeeds, Contains.Item("Power wheelchair replacement"));
    }

    [Test]
    public async Task AddMultipleAnnualEncounters_AllAppear()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Fall,
            "T12", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await wf.AddSCIAnnualEncounterAsync(
            2024, new DateTime(2023, 11, 1), SCIEncounterType.Annual,
            SCIAisGrade.A, "T12", 0, 1, 1, 2,
            null, null, null, null, null, null, null);

        await wf.AddSCIAnnualEncounterAsync(
            2025, new DateTime(2024, 10, 20), SCIEncounterType.Annual,
            SCIAisGrade.A, "T12", 0, 0, 0, 0,
            null, null, null,
            new List<string> { "Wheelchair cushion" },
            null, null, "Stable");

        List<SCIAnnualEncounterRecord> encounters = await wf.GetSCIAnnualEncountersAsync();
        Assert.That(encounters, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AddProblemFocusedEncounter_DifferentEncounterType()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.NonTraumatic, SCIEtiology.Disease,
            "L1", SCIAisGrade.D,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await wf.AddSCIAnnualEncounterAsync(
            2025, DateTime.UtcNow, SCIEncounterType.ProblemFocused,
            SCIAisGrade.D, "L1", 0, 0, 1, 3,
            null, null, null, null,
            "PROV-030", "Dr. Wound Care",
            "Stage 3 pressure injury on sacrum — wound consult placed");

        List<SCIAnnualEncounterRecord> encounters = await wf.GetSCIAnnualEncountersAsync();
        Assert.That(encounters[0].EncounterType, Is.EqualTo(SCIEncounterType.ProblemFocused));
        Assert.That(encounters[0].PressureInjuryCount, Is.EqualTo(1));
        Assert.That(encounters[0].HighestPressureInjuryStage, Is.EqualTo(3));
    }

    // ── AIS grade update through encounter ────────────────────────────────────

    [Test]
    public async Task AnnualEncounter_UpdatesPatientAisGrade()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.MotorVehicleAccident,
            "C4", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        // Annual encounter showing improvement
        await wf.AddSCIAnnualEncounterAsync(
            2025, DateTime.UtcNow, SCIEncounterType.Annual,
            SCIAisGrade.B, "C4", 0, 0, 0, 0,
            null, null, null, null, null, null,
            "Motor recovery noted below level of injury");

        // The encounter records the new AIS grade
        List<SCIAnnualEncounterRecord> encounters = await wf.GetSCIAnnualEncountersAsync();
        Assert.That(encounters[0].AisGrade, Is.EqualTo(SCIAisGrade.B));
    }

    // ── Independent patients ──────────────────────────────────────────────────

    [Test]
    public async Task DifferentPatients_HaveIndependentSCIRecords()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.Traumatic, SCIEtiology.Fall,
            "T6", SCIAisGrade.A,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        await wf2.EnrollInSCIRegistryAsync(
            DateTime.UtcNow, null, null,
            SCIInjuryType.NonTraumatic, SCIEtiology.Vascular,
            "L3", SCIAisGrade.D,
            null, null, null, null, null, null,
            null, null, null, null, null, null);

        SCIPatientState s1 = await wf1.GetSCIPatientAsync();
        SCIPatientState s2 = await wf2.GetSCIPatientAsync();

        Assert.That(s1.NeurologicalLevelOfInjury, Is.EqualTo("T6"));
        Assert.That(s2.NeurologicalLevelOfInjury, Is.EqualTo("L3"));
        Assert.That(s1.InjuryType, Is.EqualTo(SCIInjuryType.Traumatic));
        Assert.That(s2.InjuryType, Is.EqualTo(SCIInjuryType.NonTraumatic));
    }
}
