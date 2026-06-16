// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Blind Rehabilitation — VistA File #782.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class BlindRehabilitationWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Patient initialization ────────────────────────────────────────────────

    [Test]
    public async Task GetBRPatient_InitializesRecordWithPatientId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        BRPatientState state = await wf.GetBRPatientAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.Unknown));
        Assert.That(state.Devices, Has.Count.EqualTo(0));
        Assert.That(state.TrainingGoals, Has.Count.EqualTo(0));
    }

    // ── Visual acuity ─────────────────────────────────────────────────────────

    [Test]
    public async Task RecordVisualAcuity_PersistsAssessment()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.RecordVisualAcuityAsync(
            rightEyeDistance: "20/200",
            leftEyeDistance: "20/400",
            bestCorrectedRight: "20/100",
            bestCorrectedLeft: "20/200",
            visualFieldRight: VisualField.ModerateConstriction,
            visualFieldLeft: VisualField.SevereConstriction,
            contrastSensitivity: "1.2 log units",
            examDate: new DateTime(2024, 5, 15),
            examinerId: "OPT-001",
            examinerName: "Dr. Vision",
            notes: "Bilateral age-related macular degeneration");

        BRPatientState state = await wf.GetBRPatientAsync();

        Assert.That(state.RightEyeDistance, Is.EqualTo("20/200"));
        Assert.That(state.LeftEyeDistance, Is.EqualTo("20/400"));
        Assert.That(state.BestCorrectedRight, Is.EqualTo("20/100"));
        Assert.That(state.BestCorrectedLeft, Is.EqualTo("20/200"));
        Assert.That(state.VisualFieldRight, Is.EqualTo(VisualField.ModerateConstriction));
        Assert.That(state.VisualFieldLeft, Is.EqualTo(VisualField.SevereConstriction));
        Assert.That(state.ContrastSensitivity, Is.EqualTo("1.2 log units"));
        Assert.That(state.LastExamDate, Is.EqualTo(new DateTime(2024, 5, 15)));
        Assert.That(state.ExaminerName, Is.EqualTo("Dr. Vision"));
    }

    // ── Diagnosis ─────────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateDiagnosis_PersistsVisualDiagnosis()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateBRDiagnosisAsync(
            primaryDiagnosis: "Age-Related Macular Degeneration",
            secondaryDiagnosis: "Diabetic Retinopathy",
            onsetType: BROnsetType.Progressive,
            onsetDate: new DateTime(2018, 3, 1),
            serviceConnected: true,
            serviceConnectedPercentage: 30,
            icd10Code: "H35.3211",
            notes: "Bilateral dry AMD with diabetic retinopathy");

        BRPatientState state = await wf.GetBRPatientAsync();

        Assert.That(state.PrimaryDiagnosis, Is.EqualTo("Age-Related Macular Degeneration"));
        Assert.That(state.SecondaryDiagnosis, Is.EqualTo("Diabetic Retinopathy"));
        Assert.That(state.OnsetType, Is.EqualTo(BROnsetType.Progressive));
        Assert.That(state.OnsetDate, Is.EqualTo(new DateTime(2018, 3, 1)));
        Assert.That(state.ServiceConnected, Is.True);
        Assert.That(state.ServiceConnectedPercentage, Is.EqualTo(30));
        Assert.That(state.Icd10Code, Is.EqualTo("H35.3211"));
    }

    // ── Eligibility ───────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateEligibility_SetsEligibilityStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateBREligibilityAsync(
            BREligibilityStatus.LegallyBlind,
            "Best corrected visual acuity 20/200 in better eye — meets legal blindness criteria");

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.LegallyBlind));
        Assert.That(state.EligibilityReason, Does.Contain("legal blindness"));
    }

    [Test]
    public async Task UpdateEligibility_ToNotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateBREligibilityAsync(
            BREligibilityStatus.NotEligible,
            "Visual acuity 20/40 with correction — does not meet eligibility criteria");

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.NotEligible));
    }

    // ── Devices ───────────────────────────────────────────────────────────────

    [Test]
    public async Task AddDevice_AppearsInPatientDeviceList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        BRDeviceEntry device = new()
        {
            DeviceName = "CCTV Desktop Magnifier",
            Category = "Low Vision",
            SerialNumber = "CCTV-2024-001",
            IssuedDate = new DateTime(2024, 6, 1),
            IssuedBy = "Assistive Technology Specialist",
            Returned = false,
            ReturnedDate = null,
            Notes = "10x magnification — for reading mail and labels"
        };

        await wf.AddBRDeviceAsync(device);

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.Devices, Has.Count.EqualTo(1));
        Assert.That(state.Devices[0].DeviceName, Is.EqualTo("CCTV Desktop Magnifier"));
        Assert.That(state.Devices[0].Category, Is.EqualTo("Low Vision"));
        Assert.That(state.Devices[0].SerialNumber, Is.EqualTo("CCTV-2024-001"));
        Assert.That(state.Devices[0].Returned, Is.False);
    }

    [Test]
    public async Task AddMultipleDevices_AllAppear()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddBRDeviceAsync(new BRDeviceEntry
        {
            DeviceName = "White cane (folding)",
            Category = "Mobility",
            IssuedDate = DateTime.UtcNow,
            IssuedBy = "O&M Specialist"
        });

        await wf.AddBRDeviceAsync(new BRDeviceEntry
        {
            DeviceName = "JAWS Screen Reader License",
            Category = "Technology",
            IssuedDate = DateTime.UtcNow,
            IssuedBy = "CAT Specialist"
        });

        await wf.AddBRDeviceAsync(new BRDeviceEntry
        {
            DeviceName = "Handheld magnifier 5x",
            Category = "Low Vision",
            IssuedDate = DateTime.UtcNow,
            IssuedBy = "Low Vision Therapist"
        });

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.Devices, Has.Count.EqualTo(3));
    }

    // ── Training goals ────────────────────────────────────────────────────────

    [Test]
    public async Task AddTrainingGoal_AppearsInGoalList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddBRTrainingGoalAsync(
            "Safely cross 4-lane intersection using long cane",
            BRTrainingArea.OrientationAndMobility);

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.TrainingGoals, Has.Count.EqualTo(1));
        Assert.That(state.TrainingGoals[0].Goal, Does.Contain("intersection"));
        Assert.That(state.TrainingGoals[0].Area, Is.EqualTo(BRTrainingArea.OrientationAndMobility));
    }

    [Test]
    public async Task AddMultipleTrainingGoals_DifferentAreas()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddBRTrainingGoalAsync(
            "Cook a simple meal independently",
            BRTrainingArea.ActivitiesOfDailyLiving);

        await wf.AddBRTrainingGoalAsync(
            "Use JAWS screen reader to access email",
            BRTrainingArea.ComputerAccessTechnology);

        await wf.AddBRTrainingGoalAsync(
            "Eccentric viewing to read newspaper headlines",
            BRTrainingArea.VisualSkillsTraining);

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.TrainingGoals, Has.Count.EqualTo(3));
        Assert.That(state.TrainingGoals[0].Area, Is.EqualTo(BRTrainingArea.ActivitiesOfDailyLiving));
        Assert.That(state.TrainingGoals[1].Area, Is.EqualTo(BRTrainingArea.ComputerAccessTechnology));
        Assert.That(state.TrainingGoals[2].Area, Is.EqualTo(BRTrainingArea.VisualSkillsTraining));
    }

    // ── Admissions ────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateAdmission_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string admitId = await wf.CreateBRAdmissionAsync(
            centerId: "BRCTR-001",
            centerName: "Hines BRC",
            admitDate: new DateTime(2024, 9, 1),
            plannedDischargeDate: new DateTime(2024, 10, 15),
            programAreas: new List<BRTrainingArea>
            {
                BRTrainingArea.OrientationAndMobility,
                BRTrainingArea.ActivitiesOfDailyLiving,
                BRTrainingArea.ComputerAccessTechnology
            },
            priority: BRAdmissionPriority.Routine,
            referringProviderId: "PROV-050",
            referringProviderName: "Dr. Referring",
            goals: "Independent travel and ADL skills",
            notes: "First inpatient BR admission");

        Assert.That(admitId, Does.StartWith("BR-ADMIT-"));

        List<BRAdmissionIndexEntry> admissions = await wf.GetBRAdmissionsAsync();
        Assert.That(admissions, Has.Count.EqualTo(1));
        Assert.That(admissions[0].AdmitId, Is.EqualTo(admitId));
        Assert.That(admissions[0].CenterName, Is.EqualTo("Hines BRC"));
        Assert.That(admissions[0].Status, Is.EqualTo(BRAdmissionStatus.Pending));
    }

    // ── Outpatient visits ─────────────────────────────────────────────────────

    [Test]
    public async Task ScheduleOutpatientVisit_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.ScheduleBROutpatientVisitAsync(
            visitDate: new DateTime(2024, 8, 15, 10, 0, 0),
            trainingArea: BRTrainingArea.LowVision,
            therapistId: "THER-001",
            therapistName: "Jane LV Therapist",
            location: "Low Vision Clinic Room 3",
            durationMinutes: 60,
            sessionNotes: "Initial low vision evaluation and device trial",
            skillsAddressed: new List<string> { "Eccentric viewing", "Magnifier use" });

        Assert.That(visitId, Does.StartWith("BR-VISIT-"));

        List<BROutpatientVisitIndexEntry> visits = await wf.GetBROutpatientVisitsAsync();
        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].VisitId, Is.EqualTo(visitId));
        Assert.That(visits[0].TrainingArea, Is.EqualTo(BRTrainingArea.LowVision));
        Assert.That(visits[0].TherapistName, Is.EqualTo("Jane LV Therapist"));
        Assert.That(visits[0].Status, Is.EqualTo(BRVisitStatus.Scheduled));
    }

    // ── Full workflow ─────────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_EligibilityDiagnosisAcuityDevicesGoals()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set eligibility
        await wf.UpdateBREligibilityAsync(BREligibilityStatus.ServiceConnected,
            "Service-connected visual impairment — Agent Orange exposure");

        // Record diagnosis
        await wf.UpdateBRDiagnosisAsync(
            "Glaucoma", "Cataracts",
            BROnsetType.Acquired, new DateTime(2015, 1, 1),
            true, 50, "H40.10X0", "Bilateral open-angle glaucoma");

        // Record visual acuity
        await wf.RecordVisualAcuityAsync(
            "20/400", "20/600",
            "20/200", "20/400",
            VisualField.SevereConstriction, VisualField.NoField,
            "0.6 log units", DateTime.UtcNow,
            "OPT-010", "Dr. Glaucoma Specialist",
            "Progressive field loss bilaterally");

        // Issue device
        await wf.AddBRDeviceAsync(new BRDeviceEntry
        {
            DeviceName = "Telescope 4x monocular",
            Category = "Low Vision",
            IssuedDate = DateTime.UtcNow,
            IssuedBy = "Low Vision Optometrist"
        });

        // Add training goal
        await wf.AddBRTrainingGoalAsync(
            "Use bioptic telescope for distance spotting",
            BRTrainingArea.LowVision);

        BRPatientState state = await wf.GetBRPatientAsync();
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.ServiceConnected));
        Assert.That(state.PrimaryDiagnosis, Is.EqualTo("Glaucoma"));
        Assert.That(state.RightEyeDistance, Is.EqualTo("20/400"));
        Assert.That(state.VisualFieldLeft, Is.EqualTo(VisualField.NoField));
        Assert.That(state.Devices, Has.Count.EqualTo(1));
        Assert.That(state.TrainingGoals, Has.Count.EqualTo(1));
        Assert.That(state.ServiceConnected, Is.True);
        Assert.That(state.ServiceConnectedPercentage, Is.EqualTo(50));
    }

    // ── Independent patients ──────────────────────────────────────────────────

    [Test]
    public async Task DifferentPatients_HaveIndependentBRRecords()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.UpdateBREligibilityAsync(BREligibilityStatus.LegallyBlind, "20/200 best corrected");
        await wf1.AddBRDeviceAsync(new BRDeviceEntry
        {
            DeviceName = "White cane",
            Category = "Mobility",
            IssuedDate = DateTime.UtcNow,
            IssuedBy = "O&M Specialist"
        });

        await wf2.UpdateBREligibilityAsync(BREligibilityStatus.SevereImpairment, "20/100 best corrected");

        BRPatientState s1 = await wf1.GetBRPatientAsync();
        BRPatientState s2 = await wf2.GetBRPatientAsync();

        Assert.That(s1.EligibilityStatus, Is.EqualTo(BREligibilityStatus.LegallyBlind));
        Assert.That(s1.Devices, Has.Count.EqualTo(1));
        Assert.That(s2.EligibilityStatus, Is.EqualTo(BREligibilityStatus.SevereImpairment));
        Assert.That(s2.Devices, Has.Count.EqualTo(0));
    }
}
