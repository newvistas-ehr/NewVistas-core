// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Blind Rehabilitation grain layer.
/// VistA Blind Rehabilitation file (#782).
/// Tests BRPatientGrain, BRCenterIndexGrain, BRAdmissionGrain, BROutpatientVisitGrain,
/// and PatientWorkflowGrain BR methods.
/// </summary>
[TestFixture]
public class BlindRehabilitationTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── BRPatientGrain — initialization ──────────────────────────────────────

    [Test]
    public async Task BRPatientGrain_Initialize_SetsPatientId()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);

        await grain.InitializeAsync("PATIENT-001");
        BRPatientState state = await grain.GetAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.Unknown));
    }

    [Test]
    public async Task BRPatientGrain_InitializeTwice_DoesNotOverwrite()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);

        await grain.InitializeAsync("PATIENT-001");
        await grain.UpdateEligibilityAsync(BREligibilityStatus.LegallyBlind, "20/200 OD");
        await grain.InitializeAsync("PATIENT-001"); // second call should be no-op

        BRPatientState state = await grain.GetAsync();
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.LegallyBlind));
    }

    // ── BRPatientGrain — visual acuity ────────────────────────────────────────

    [Test]
    public async Task BRPatientGrain_RecordVisualAcuity_PersistsAllFields()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);
        await grain.InitializeAsync("PATIENT-002");

        DateTime examDate = new DateTime(2025, 3, 10);

        await grain.RecordVisualAcuityAsync(
            rightEyeDistance:    "20/200",
            leftEyeDistance:     "20/400",
            bestCorrectedRight:  "20/100",
            bestCorrectedLeft:   "20/200",
            visualFieldRight:    VisualField.ModerateConstriction,
            visualFieldLeft:     VisualField.SevereConstriction,
            contrastSensitivity: "Reduced",
            examDate:            examDate,
            examinerId:          "EXAM-001",
            examinerName:        "Dr. A. Ortega",
            notes:               "AMD both eyes, worse in left.");

        BRPatientState state = await grain.GetAsync();

        Assert.That(state.RightEyeDistance,     Is.EqualTo("20/200"));
        Assert.That(state.LeftEyeDistance,      Is.EqualTo("20/400"));
        Assert.That(state.BestCorrectedRight,   Is.EqualTo("20/100"));
        Assert.That(state.BestCorrectedLeft,    Is.EqualTo("20/200"));
        Assert.That(state.VisualFieldRight,     Is.EqualTo(VisualField.ModerateConstriction));
        Assert.That(state.VisualFieldLeft,      Is.EqualTo(VisualField.SevereConstriction));
        Assert.That(state.ContrastSensitivity,  Is.EqualTo("Reduced"));
        Assert.That(state.LastExamDate,         Is.EqualTo(examDate));
        Assert.That(state.ExaminerName,         Is.EqualTo("Dr. A. Ortega"));
        Assert.That(state.AcuityNotes,          Does.Contain("AMD"));
    }

    // ── BRPatientGrain — diagnosis ────────────────────────────────────────────

    [Test]
    public async Task BRPatientGrain_UpdateDiagnosis_PersistsAllFields()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);
        await grain.InitializeAsync("PATIENT-003");

        await grain.UpdateDiagnosisAsync(
            primaryDiagnosis:            "Age-Related Macular Degeneration",
            secondaryDiagnosis:          "Diabetic Retinopathy",
            onsetType:                   BROnsetType.Progressive,
            onsetDate:                   new DateTime(2018, 6, 1),
            serviceConnected:            true,
            serviceConnectedPercentage:  60,
            icd10Code:                   "H35.30",
            notes:                       "Progressive bilateral AMD.");

        BRPatientState state = await grain.GetAsync();

        Assert.That(state.PrimaryDiagnosis,            Is.EqualTo("Age-Related Macular Degeneration"));
        Assert.That(state.SecondaryDiagnosis,           Is.EqualTo("Diabetic Retinopathy"));
        Assert.That(state.OnsetType,                    Is.EqualTo(BROnsetType.Progressive));
        Assert.That(state.ServiceConnected,             Is.True);
        Assert.That(state.ServiceConnectedPercentage,   Is.EqualTo(60));
        Assert.That(state.Icd10Code,                    Is.EqualTo("H35.30"));
    }

    // ── BRPatientGrain — eligibility ──────────────────────────────────────────

    [Test]
    public async Task BRPatientGrain_UpdateEligibility_ChangesStatus()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);
        await grain.InitializeAsync("PATIENT-004");

        Assert.That((await grain.GetAsync()).EligibilityStatus, Is.EqualTo(BREligibilityStatus.Unknown));

        await grain.UpdateEligibilityAsync(BREligibilityStatus.LegallyBlind, "Best corrected 20/200 OU");

        BRPatientState state = await grain.GetAsync();
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.LegallyBlind));
        Assert.That(state.EligibilityReason, Does.Contain("20/200"));
    }

    // ── BRPatientGrain — devices ──────────────────────────────────────────────

    [Test]
    public async Task BRPatientGrain_AddDevice_PersistsEntry()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);
        await grain.InitializeAsync("PATIENT-005");

        BRDeviceEntry device = new()
        {
            DeviceName   = "Long White Cane",
            Category     = "Mobility",
            SerialNumber = "LWC-20240115",
            IssuedDate   = new DateTime(2024, 1, 15),
            IssuedBy     = "O&M Therapist Jones"
        };

        await grain.AddDeviceAsync(device);

        BRPatientState state = await grain.GetAsync();
        Assert.That(state.Devices, Has.Count.EqualTo(1));
        Assert.That(state.Devices[0].DeviceName,  Is.EqualTo("Long White Cane"));
        Assert.That(state.Devices[0].Category,    Is.EqualTo("Mobility"));
        Assert.That(state.Devices[0].IssuedBy,    Is.EqualTo("O&M Therapist Jones"));
        Assert.That(state.Devices[0].Returned,    Is.False);
    }

    [Test]
    public async Task BRPatientGrain_AddMultipleDevices_AccumulatesEntries()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);
        await grain.InitializeAsync("PATIENT-006");

        await grain.AddDeviceAsync(new BRDeviceEntry { DeviceName = "Long White Cane", Category = "Mobility", IssuedDate = DateTime.Today, IssuedBy = "Therapist A" });
        await grain.AddDeviceAsync(new BRDeviceEntry { DeviceName = "Portable CCTV",   Category = "Low Vision", IssuedDate = DateTime.Today, IssuedBy = "Therapist B" });
        await grain.AddDeviceAsync(new BRDeviceEntry { DeviceName = "JAWS Screen Reader", Category = "Technology", IssuedDate = DateTime.Today, IssuedBy = "Therapist C" });

        BRPatientState state = await grain.GetAsync();
        Assert.That(state.Devices, Has.Count.EqualTo(3));
        Assert.That(state.Devices.Select(d => d.DeviceName), Does.Contain("JAWS Screen Reader"));
    }

    // ── BRPatientGrain — training goals ───────────────────────────────────────

    [Test]
    public async Task BRPatientGrain_AddTrainingGoal_PersistsGoalAndArea()
    {
        string patientId = $"BR-PATIENT:{Guid.NewGuid()}";
        IBRPatientGrain grain = _cluster.GrainFactory.GetGrain<IBRPatientGrain>(patientId);
        await grain.InitializeAsync("PATIENT-007");

        await grain.AddTrainingGoalAsync(
            "Navigate independently to cafeteria using white cane",
            BRTrainingArea.OrientationAndMobility);

        BRPatientState state = await grain.GetAsync();
        Assert.That(state.TrainingGoals, Has.Count.EqualTo(1));
        Assert.That(state.TrainingGoals[0].Goal, Does.Contain("cafeteria"));
        Assert.That(state.TrainingGoals[0].Area, Is.EqualTo(BRTrainingArea.OrientationAndMobility));
        Assert.That(state.TrainingGoals[0].Achieved, Is.False);
    }

    // ── BRAdmissionGrain ─────────────────────────────────────────────────────

    [Test]
    public async Task BRAdmissionGrain_Create_PersistsAllFields()
    {
        string admitId = $"BR-ADMIT-{Guid.NewGuid()}";
        IBRAdmissionGrain grain = _cluster.GrainFactory.GetGrain<IBRAdmissionGrain>(admitId);

        DateTime admitDate = new DateTime(2025, 4, 1);

        await grain.CreateAsync(
            admitId:              admitId,
            patientId:            "PATIENT-008",
            centerId:             "BR-CTR-HINES",
            centerName:           "Hines VA Blind Rehabilitation Center",
            admitDate:            admitDate,
            plannedDischargeDate: new DateTime(2025, 6, 30),
            programAreas:         new List<BRTrainingArea> { BRTrainingArea.OrientationAndMobility, BRTrainingArea.ActivitiesOfDailyLiving },
            priority:             BRAdmissionPriority.Routine,
            referringProviderId:  "PROV-001",
            referringProviderName:"Dr. B. Chen",
            goals:                "Achieve independent mobility within facility",
            notes:                null);

        BRAdmissionState state = await grain.GetAsync();

        Assert.That(state.AdmitId,               Is.EqualTo(admitId));
        Assert.That(state.PatientId,             Is.EqualTo("PATIENT-008"));
        Assert.That(state.CenterName,            Is.EqualTo("Hines VA Blind Rehabilitation Center"));
        Assert.That(state.AdmitDate,             Is.EqualTo(admitDate));
        Assert.That(state.Status,                Is.EqualTo(BRAdmissionStatus.Pending));
        Assert.That(state.Priority,              Is.EqualTo(BRAdmissionPriority.Routine));
        Assert.That(state.ProgramAreas,          Has.Count.EqualTo(2));
        Assert.That(state.ProgramAreas,          Does.Contain(BRTrainingArea.OrientationAndMobility));
        Assert.That(state.ReferringProviderName, Is.EqualTo("Dr. B. Chen"));
        Assert.That(state.Goals,                 Does.Contain("independent mobility"));
    }

    [Test]
    public async Task BRAdmissionGrain_AddProgressNote_SetsStatusToActive()
    {
        string admitId = $"BR-ADMIT-{Guid.NewGuid()}";
        IBRAdmissionGrain grain = _cluster.GrainFactory.GetGrain<IBRAdmissionGrain>(admitId);

        await grain.CreateAsync(admitId, "PATIENT-009", "BR-CTR-PALO-ALTO", "Palo Alto BR Center",
            DateTime.Today, null, new List<BRTrainingArea> { BRTrainingArea.LowVision },
            BRAdmissionPriority.Routine, "PROV-002", "Dr. C. Nguyen", null, null);

        await grain.AddProgressNoteAsync("Patient oriented to unit, baseline O&M assessed.", "THERAPIST-01", "Therapist Davis");

        BRAdmissionState state = await grain.GetAsync();
        Assert.That(state.Status,                    Is.EqualTo(BRAdmissionStatus.Active));
        Assert.That(state.ProgressNotes,             Has.Count.EqualTo(1));
        Assert.That(state.ProgressNotes[0].Note,     Does.Contain("O&M assessed"));
        Assert.That(state.ProgressNotes[0].AuthorName, Is.EqualTo("Therapist Davis"));
    }

    [Test]
    public async Task BRAdmissionGrain_Discharge_SetsDischargedStatusAndSummary()
    {
        string admitId = $"BR-ADMIT-{Guid.NewGuid()}";
        IBRAdmissionGrain grain = _cluster.GrainFactory.GetGrain<IBRAdmissionGrain>(admitId);

        await grain.CreateAsync(admitId, "PATIENT-010", "BR-CTR-BILOXI", "Biloxi BR Center",
            new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
            new List<BRTrainingArea> { BRTrainingArea.OrientationAndMobility, BRTrainingArea.ComputerAccessTechnology },
            BRAdmissionPriority.Routine, "PROV-003", "Dr. D. Kim", null, null);

        DateTime dischargeDate = new DateTime(2025, 3, 28);
        await grain.DischargeAsync(
            dischargeDate:    dischargeDate,
            disposition:      BRDischargeDisposition.CompletedProgram,
            dischargeSummary: "Patient completed full O&M and CAT programs. Goals achieved.",
            areasCompleted:   new List<BRTrainingArea> { BRTrainingArea.OrientationAndMobility, BRTrainingArea.ComputerAccessTechnology },
            followUpPlan:     "Follow up with local VIST coordinator within 30 days.");

        BRAdmissionState state = await grain.GetAsync();
        Assert.That(state.Status,               Is.EqualTo(BRAdmissionStatus.Discharged));
        Assert.That(state.ActualDischargeDate,  Is.EqualTo(dischargeDate));
        Assert.That(state.DischargeDisposition, Is.EqualTo(BRDischargeDisposition.CompletedProgram));
        Assert.That(state.DischargeSummary,     Does.Contain("Goals achieved"));
        Assert.That(state.AreasCompleted,       Has.Count.EqualTo(2));
        Assert.That(state.FollowUpPlan,         Does.Contain("VIST coordinator"));
    }

    [Test]
    public async Task BRAdmissionGrain_Cancel_SetsCancelledStatus()
    {
        string admitId = $"BR-ADMIT-{Guid.NewGuid()}";
        IBRAdmissionGrain grain = _cluster.GrainFactory.GetGrain<IBRAdmissionGrain>(admitId);

        await grain.CreateAsync(admitId, "PATIENT-011", "BR-CTR-TUCSON", "Tucson BR Center",
            DateTime.Today.AddDays(14), null, new List<BRTrainingArea> { BRTrainingArea.GuideDog },
            BRAdmissionPriority.Routine, "PROV-004", "Dr. E. Patel", null, null);

        await grain.CancelAsync("Patient declined admission");

        BRAdmissionState state = await grain.GetAsync();
        Assert.That(state.Status,             Is.EqualTo(BRAdmissionStatus.Cancelled));
        Assert.That(state.CancellationReason, Does.Contain("declined"));
    }

    // ── BRAdmissionIndexGrain ─────────────────────────────────────────────────

    [Test]
    public async Task BRAdmissionIndexGrain_AddAndGetAll_ReturnAllEntries()
    {
        string indexKey = $"BR-ADMIT-IDX:{Guid.NewGuid()}";
        IBRAdmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IBRAdmissionIndexGrain>(indexKey);

        await index.AddAsync(new BRAdmissionIndexEntry { AdmitId = "ADMIT-A1", PatientId = "PAT-A", CenterName = "Hines BR", AdmitDate = DateTime.Today, Status = BRAdmissionStatus.Active });
        await index.AddAsync(new BRAdmissionIndexEntry { AdmitId = "ADMIT-A2", PatientId = "PAT-A", CenterName = "Palo Alto BR", AdmitDate = DateTime.Today.AddMonths(-12), Status = BRAdmissionStatus.Discharged });

        List<BRAdmissionIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task BRAdmissionIndexGrain_GetActive_FiltersCorrectly()
    {
        string indexKey = $"BR-ADMIT-IDX:{Guid.NewGuid()}";
        IBRAdmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IBRAdmissionIndexGrain>(indexKey);

        await index.AddAsync(new BRAdmissionIndexEntry { AdmitId = "ADMIT-B1", PatientId = "PAT-B", CenterName = "Hines BR", AdmitDate = DateTime.Today, Status = BRAdmissionStatus.Active });
        await index.AddAsync(new BRAdmissionIndexEntry { AdmitId = "ADMIT-B2", PatientId = "PAT-B", CenterName = "Palo Alto BR", AdmitDate = DateTime.Today.AddMonths(-6), Status = BRAdmissionStatus.Discharged });
        await index.AddAsync(new BRAdmissionIndexEntry { AdmitId = "ADMIT-B3", PatientId = "PAT-B", CenterName = "Biloxi BR", AdmitDate = DateTime.Today.AddDays(-7), Status = BRAdmissionStatus.Accepted });

        List<BRAdmissionIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.Select(a => a.AdmitId), Does.Contain("ADMIT-B1"));
        Assert.That(active.Select(a => a.AdmitId), Does.Contain("ADMIT-B3"));
    }

    [Test]
    public async Task BRAdmissionIndexGrain_UpdateStatus_ChangesEntryStatus()
    {
        string indexKey = $"BR-ADMIT-IDX:{Guid.NewGuid()}";
        IBRAdmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IBRAdmissionIndexGrain>(indexKey);

        await index.AddAsync(new BRAdmissionIndexEntry { AdmitId = "ADMIT-C1", PatientId = "PAT-C", CenterName = "Test BR Center", AdmitDate = DateTime.Today, Status = BRAdmissionStatus.Pending });
        await index.UpdateStatusAsync("ADMIT-C1", BRAdmissionStatus.Discharged);

        List<BRAdmissionIndexEntry> all = await index.GetAllAsync();
        Assert.That(all[0].Status, Is.EqualTo(BRAdmissionStatus.Discharged));
    }

    // ── BROutpatientVisitGrain ────────────────────────────────────────────────

    [Test]
    public async Task BROutpatientVisitGrain_Create_PersistsAllFields()
    {
        string visitId = $"BR-VISIT-{Guid.NewGuid()}";
        IBROutpatientVisitGrain grain = _cluster.GrainFactory.GetGrain<IBROutpatientVisitGrain>(visitId);

        DateTime visitDate = new DateTime(2025, 5, 12);

        await grain.CreateAsync(
            visitId:        visitId,
            patientId:      "PATIENT-012",
            visitDate:      visitDate,
            trainingArea:   BRTrainingArea.ComputerAccessTechnology,
            therapistId:    "THERAPIST-02",
            therapistName:  "Therapist Sullivan",
            location:       "Clinic Room 4B",
            durationMinutes:90,
            sessionNotes:   "Introduction to JAWS screen reader basics.",
            skillsAddressed:new List<string> { "JAWS navigation", "document reading" });

        BROutpatientVisitState state = await grain.GetAsync();

        Assert.That(state.VisitId,          Is.EqualTo(visitId));
        Assert.That(state.PatientId,        Is.EqualTo("PATIENT-012"));
        Assert.That(state.VisitDate,        Is.EqualTo(visitDate));
        Assert.That(state.TrainingArea,     Is.EqualTo(BRTrainingArea.ComputerAccessTechnology));
        Assert.That(state.TherapistName,    Is.EqualTo("Therapist Sullivan"));
        Assert.That(state.DurationMinutes,  Is.EqualTo(90));
        Assert.That(state.Status,           Is.EqualTo(BRVisitStatus.Scheduled));
        Assert.That(state.SkillsAddressed,  Has.Count.EqualTo(2));
        Assert.That(state.SkillsAddressed,  Does.Contain("JAWS navigation"));
    }

    [Test]
    public async Task BROutpatientVisitGrain_Complete_SetsOutcomeAndStatus()
    {
        string visitId = $"BR-VISIT-{Guid.NewGuid()}";
        IBROutpatientVisitGrain grain = _cluster.GrainFactory.GetGrain<IBROutpatientVisitGrain>(visitId);

        await grain.CreateAsync(visitId, "PATIENT-013", DateTime.Today, BRTrainingArea.LowVision,
            "THERAPIST-03", "Therapist Martin", "Low Vision Clinic", 60, null, new List<string>());

        await grain.CompleteAsync("Patient successfully trialed +8 diopter magnifier for near work. Good candidate for CCTV.",
            BRVisitOutcome.ProgressMade);

        BROutpatientVisitState state = await grain.GetAsync();
        Assert.That(state.Status,          Is.EqualTo(BRVisitStatus.Completed));
        Assert.That(state.Outcome,         Is.EqualTo(BRVisitOutcome.ProgressMade));
        Assert.That(state.OutcomeSummary,  Does.Contain("magnifier"));
    }

    [Test]
    public async Task BROutpatientVisitGrain_Cancel_SetsCancelledAndReason()
    {
        string visitId = $"BR-VISIT-{Guid.NewGuid()}";
        IBROutpatientVisitGrain grain = _cluster.GrainFactory.GetGrain<IBROutpatientVisitGrain>(visitId);

        await grain.CreateAsync(visitId, "PATIENT-014", DateTime.Today.AddDays(3), BRTrainingArea.ManualSkills,
            "THERAPIST-04", "Therapist Burke", "Room 2A", 45, null, new List<string>());

        await grain.CancelAsync("Patient hospitalized");

        BROutpatientVisitState state = await grain.GetAsync();
        Assert.That(state.Status,             Is.EqualTo(BRVisitStatus.Cancelled));
        Assert.That(state.CancellationReason, Does.Contain("hospitalized"));
    }

    // ── BRCenterIndexGrain ────────────────────────────────────────────────────

    [Test]
    public async Task BRCenterIndexGrain_SeedDefaults_PopulatesCenters()
    {
        string indexKey = $"BR-CENTER-IDX-{Guid.NewGuid()}";
        IBRCenterIndexGrain index = _cluster.GrainFactory.GetGrain<IBRCenterIndexGrain>(indexKey);

        await index.SeedDefaultsAsync();

        List<BRCenterIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Count, Is.GreaterThan(0));
        Assert.That(all.Any(c => c.City == "Hines"), Is.True);
    }

    [Test]
    public async Task BRCenterIndexGrain_SeedDefaultsIdempotent_DoesNotDuplicate()
    {
        string indexKey = $"BR-CENTER-IDX-{Guid.NewGuid()}";
        IBRCenterIndexGrain index = _cluster.GrainFactory.GetGrain<IBRCenterIndexGrain>(indexKey);

        await index.SeedDefaultsAsync();
        int count1 = (await index.GetAllAsync()).Count;

        await index.SeedDefaultsAsync(); // second call should be no-op
        int count2 = (await index.GetAllAsync()).Count;

        Assert.That(count1, Is.EqualTo(count2));
    }

    [Test]
    public async Task BRCenterIndexGrain_GetAccepting_FiltersCorrectly()
    {
        string indexKey = $"BR-CENTER-IDX-{Guid.NewGuid()}";
        IBRCenterIndexGrain index = _cluster.GrainFactory.GetGrain<IBRCenterIndexGrain>(indexKey);

        await index.UpsertAsync(new BRCenterIndexEntry { CenterId = "CTR-1", Name = "Open Center",   City = "CityA", State = "AK", CenterType = BRCenterType.Comprehensive,  AcceptingPatients = true });
        await index.UpsertAsync(new BRCenterIndexEntry { CenterId = "CTR-2", Name = "Closed Center", City = "CityB", State = "AL", CenterType = BRCenterType.Vist, AcceptingPatients = false });

        List<BRCenterIndexEntry> accepting = await index.GetAcceptingAsync();
        Assert.That(accepting, Has.Count.EqualTo(1));
        Assert.That(accepting[0].CenterId, Is.EqualTo("CTR-1"));
    }

    // ── PatientWorkflowGrain integration ──────────────────────────────────────

    [Test]
    public async Task WorkflowGrain_GetBRPatient_InitializesAndReturnsRecord()
    {
        string patientId = $"PATIENT-BR-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        BRPatientState state = await workflow.GetBRPatientAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.Unknown));
    }

    [Test]
    public async Task WorkflowGrain_RecordVisualAcuity_PersistsValues()
    {
        string patientId = $"PATIENT-BR-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.RecordVisualAcuityAsync(
            rightEyeDistance:    "CF",
            leftEyeDistance:     "20/800",
            bestCorrectedRight:  "CF",
            bestCorrectedLeft:   "20/400",
            visualFieldRight:    VisualField.SevereConstriction,
            visualFieldLeft:     VisualField.ModerateConstriction,
            contrastSensitivity: null,
            examDate:            new DateTime(2025, 6, 1),
            examinerId:          "EXAM-002",
            examinerName:        "Dr. F. Yamamoto",
            notes:               "End-stage glaucoma.");

        BRPatientState state = await workflow.GetBRPatientAsync();

        Assert.That(state.RightEyeDistance,   Is.EqualTo("CF"));
        Assert.That(state.VisualFieldRight,   Is.EqualTo(VisualField.SevereConstriction));
        Assert.That(state.ExaminerName,       Is.EqualTo("Dr. F. Yamamoto"));
    }

    [Test]
    public async Task WorkflowGrain_UpdateBREligibility_ChangesEligibilityStatus()
    {
        string patientId = $"PATIENT-BR-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.UpdateBREligibilityAsync(BREligibilityStatus.ServiceConnected, "SC 100% for visual impairment");

        BRPatientState state = await workflow.GetBRPatientAsync();
        Assert.That(state.EligibilityStatus, Is.EqualTo(BREligibilityStatus.ServiceConnected));
        Assert.That(state.EligibilityReason, Does.Contain("SC 100%"));
    }

    [Test]
    public async Task WorkflowGrain_CreateBRAdmission_ReturnsAdmitIdAndIndexesEntry()
    {
        string patientId = $"PATIENT-BR-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string admitId = await workflow.CreateBRAdmissionAsync(
            centerId:              "BR-CTR-HINES",
            centerName:            "Hines VA BR Center",
            admitDate:             new DateTime(2025, 7, 1),
            plannedDischargeDate:  new DateTime(2025, 9, 30),
            programAreas:          new List<BRTrainingArea> { BRTrainingArea.OrientationAndMobility },
            priority:              BRAdmissionPriority.Routine,
            referringProviderId:   "PROV-005",
            referringProviderName: "Dr. G. Torres",
            goals:                 "Independent O&M in home environment",
            notes:                 null);

        Assert.That(admitId, Does.StartWith("BR-ADMIT-"));

        List<BRAdmissionIndexEntry> admissions = await workflow.GetBRAdmissionsAsync();
        Assert.That(admissions, Has.Count.EqualTo(1));
        Assert.That(admissions[0].AdmitId,     Is.EqualTo(admitId));
        Assert.That(admissions[0].CenterName,  Is.EqualTo("Hines VA BR Center"));
        Assert.That(admissions[0].Status,      Is.EqualTo(BRAdmissionStatus.Pending));
    }

    [Test]
    public async Task WorkflowGrain_ScheduleBROutpatientVisit_ReturnsVisitIdAndIndexesEntry()
    {
        string patientId = $"PATIENT-BR-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string visitId = await workflow.ScheduleBROutpatientVisitAsync(
            visitDate:        new DateTime(2025, 8, 5),
            trainingArea:     BRTrainingArea.ActivitiesOfDailyLiving,
            therapistId:      "THERAPIST-05",
            therapistName:    "Therapist Rivera",
            location:         "ADL Kitchen Suite",
            durationMinutes:  120,
            sessionNotes:     "Focus on meal preparation adaptations.",
            skillsAddressed:  new List<string> { "Pouring safely", "Stove controls" });

        Assert.That(visitId, Does.StartWith("BR-VISIT-"));

        List<BROutpatientVisitIndexEntry> visits = await workflow.GetBROutpatientVisitsAsync();
        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].VisitId,       Is.EqualTo(visitId));
        Assert.That(visits[0].TrainingArea,  Is.EqualTo(BRTrainingArea.ActivitiesOfDailyLiving));
        Assert.That(visits[0].TherapistName, Is.EqualTo("Therapist Rivera"));
        Assert.That(visits[0].Status,        Is.EqualTo(BRVisitStatus.Scheduled));
    }

    [Test]
    public async Task WorkflowGrain_FullBRWorkflow_PatientEligibleWithAdmissionAndVisits()
    {
        string patientId = $"PATIENT-BR-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // 1. Set eligibility
        await workflow.UpdateBREligibilityAsync(BREligibilityStatus.LegallyBlind, "20/200 OU with best correction");

        // 2. Record acuity
        await workflow.RecordVisualAcuityAsync(
            "20/200", "20/200", "20/100", "20/100",
            VisualField.Normal, VisualField.Normal, null,
            DateTime.Today, "EXAM-003", "Dr. H. Park", null);

        // 3. Update diagnosis
        await workflow.UpdateBRDiagnosisAsync(
            "Bilateral Cataracts Post-Op, Residual Macular Scarring",
            null, BROnsetType.Acquired, new DateTime(2020, 1, 1),
            true, 60, "H26.40", null);

        // 4. Create admission
        string admitId = await workflow.CreateBRAdmissionAsync(
            "BR-CTR-WEST-HAVEN", "West Haven BR Center",
            new DateTime(2025, 9, 1), null,
            new List<BRTrainingArea> { BRTrainingArea.OrientationAndMobility, BRTrainingArea.LowVision },
            BRAdmissionPriority.Routine, "PROV-006", "Dr. I. Sharma", null, null);

        // 5. Schedule outpatient visits
        string v1 = await workflow.ScheduleBROutpatientVisitAsync(DateTime.Today, BRTrainingArea.LowVision, "T1", "Therapist T1", "Clinic", 60, null, new List<string>());
        string v2 = await workflow.ScheduleBROutpatientVisitAsync(DateTime.Today.AddDays(7), BRTrainingArea.OrientationAndMobility, "T2", "Therapist T2", "Hallway", 60, null, new List<string>());

        // Assert final state
        BRPatientState patient = await workflow.GetBRPatientAsync();
        Assert.That(patient.EligibilityStatus, Is.EqualTo(BREligibilityStatus.LegallyBlind));
        Assert.That(patient.ServiceConnected,  Is.True);
        Assert.That(patient.PrimaryDiagnosis,  Does.Contain("Cataracts"));

        List<BRAdmissionIndexEntry> admissions = await workflow.GetBRAdmissionsAsync();
        Assert.That(admissions, Has.Count.EqualTo(1));

        List<BROutpatientVisitIndexEntry> visits = await workflow.GetBROutpatientVisitsAsync();
        Assert.That(visits, Has.Count.EqualTo(2));
        Assert.That(visits.Select(v => v.VisitId), Does.Contain(v1));
        Assert.That(visits.Select(v => v.VisitId), Does.Contain(v2));
    }
}
