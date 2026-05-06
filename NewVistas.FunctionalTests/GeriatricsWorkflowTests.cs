// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Geriatrics and Extended Care — VistA GEC File #25.1.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class GeriatricsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IGECAssessmentGrain GetAssessment(string id)
        => _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);

    private IGECAssessmentIndexGrain GetAssessmentIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>($"GEC-ASSESS-IDX:{patientId}");

    private ICLCAdmissionGrain GetAdmission(string id)
        => _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);

    private ICLCAdmissionIndexGrain GetAdmissionIndex()
        => _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX");

    // ── GEC Assessment Tests ─────────────────────────────────────────────────

    [Test]
    public async Task CreateAssessment_SetsInitialFields()
    {
        string assessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}";
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IGECAssessmentGrain grain = GetAssessment(assessmentId);

        DateTime now = DateTime.UtcNow;
        await grain.CreateAssessmentAsync(
            patientId, "DOE,JOHN",
            GECAssessmentType.Initial, now,
            now.AddDays(-7), now,
            GECLevelOfCare.SkilledNursing,
            "RN Smith", "Registered Nurse");

        GECAssessmentState state = await grain.GetAssessmentAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.AssessmentType, Is.EqualTo(GECAssessmentType.Initial));
        Assert.That(state.LevelOfCare, Is.EqualTo(GECLevelOfCare.SkilledNursing));
        Assert.That(state.CompletedBy, Is.EqualTo("RN Smith"));
        Assert.That(state.Status, Is.EqualTo(GECAssessmentStatus.Draft));
    }

    [Test]
    public async Task RecordADLScores_ComputesTotalScore()
    {
        string assessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}";
        IGECAssessmentGrain grain = GetAssessment(assessmentId);

        await grain.CreateAssessmentAsync(
            "PAT-001", "SMITH,JANE", GECAssessmentType.Quarterly,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.LongTermCare, "OT Brown", "Occupational Therapist");

        // Each ADL score is 0-4: bed=2, transfer=3, walk=4, dress=2, eat=1, toilet=3, hygiene=2 = 17
        await grain.RecordADLScoresAsync(
            bedMobility: 2, transfer: 3, walking: 4,
            dressing: 2, eating: 1, toiletUse: 3, personalHygiene: 2);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.ADLBedMobility, Is.EqualTo(2));
        Assert.That(state.ADLTransfer, Is.EqualTo(3));
        Assert.That(state.ADLWalking, Is.EqualTo(4));
        Assert.That(state.ADLTotalScore, Is.EqualTo(17));
    }

    [Test]
    public async Task RecordCognitiveMood_SetsBIMSAndPHQ9()
    {
        string assessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}";
        IGECAssessmentGrain grain = GetAssessment(assessmentId);

        await grain.CreateAssessmentAsync(
            "PAT-002", "GREEN,BOB", GECAssessmentType.Annual,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.DementiaCare, "NP Davis", "Nurse Practitioner");

        await grain.RecordCognitiveMoodAsync(bimsScore: 8, phq9Score: 12);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.BIMSScore, Is.EqualTo(8));
        Assert.That(state.PHQ9Score, Is.EqualTo(12));
    }

    [Test]
    public async Task RecordClinicalIndicators_SetsAllFlags()
    {
        string assessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}";
        IGECAssessmentGrain grain = GetAssessment(assessmentId);

        await grain.CreateAssessmentAsync(
            "PAT-003", "WHITE,TOM", GECAssessmentType.SignificantChange,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.SkilledNursing, "RN Lee", "Registered Nurse");

        await grain.RecordClinicalIndicatorsAsync(
            painPresent: true, painFrequency: "Almost constantly",
            pressureUlcerCount: 2, fallsLast30Days: 1,
            nutritionConcern: true, behaviorSymptoms: false);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.PainPresent, Is.True);
        Assert.That(state.PainFrequency, Is.EqualTo("Almost constantly"));
        Assert.That(state.PressureUlcerCount, Is.EqualTo(2));
        Assert.That(state.FallsLast30Days, Is.EqualTo(1));
        Assert.That(state.NutritionConcern, Is.True);
    }

    [Test]
    public async Task SetRUGCategory_UpdatesClassification()
    {
        string assessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}";
        IGECAssessmentGrain grain = GetAssessment(assessmentId);

        await grain.CreateAssessmentAsync(
            "PAT-004", "KING,DAN", GECAssessmentType.Initial,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.SubAcuteRehabilitation, "PT Adams", "Physical Therapist");

        await grain.SetRUGCategoryAsync(GECRUGCategory.Rehabilitation);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.RUGCategory, Is.EqualTo(GECRUGCategory.Rehabilitation));
    }

    [Test]
    public async Task SubmitAssessment_SetsSubmittedStatus()
    {
        string assessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}";
        IGECAssessmentGrain grain = GetAssessment(assessmentId);

        await grain.CreateAssessmentAsync(
            "PAT-005", "BROWN,SUE", GECAssessmentType.Quarterly,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.LongTermCare, "RN Miller", "Registered Nurse");

        await grain.SubmitAssessmentAsync("RN Miller");

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.Status, Is.EqualTo(GECAssessmentStatus.Submitted));
        Assert.That(state.SubmittedDate, Is.Not.Null);
    }

    // ── Assessment Index Tests ───────────────────────────────────────────────

    [Test]
    public async Task AssessmentIndex_UpsertAndQueryByType()
    {
        string patientId = $"PAT-AIDX-{Guid.NewGuid():N}";
        IGECAssessmentIndexGrain index = GetAssessmentIndex(patientId);

        await index.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = $"GEC-ASSESS-{Guid.NewGuid():N}",
            PatientId = patientId, PatientName = "TEST,PATIENT",
            AssessmentType = GECAssessmentType.Initial,
            AssessmentDate = DateTime.UtcNow,
            Status = GECAssessmentStatus.Submitted,
            RUGCategory = GECRUGCategory.Rehabilitation,
            ADLTotalScore = 14, LevelOfCare = GECLevelOfCare.SubAcuteRehabilitation
        });

        List<GECAssessmentIndexEntry> results = await index.GetAssessmentsByTypeAsync(GECAssessmentType.Initial);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].ADLTotalScore, Is.EqualTo(14));
    }

    // ── CLC Admission Tests ──────────────────────────────────────────────────

    [Test]
    public async Task AdmitPatient_CreatesActiveAdmission()
    {
        string admissionId = $"CLC-ADMIT-{Guid.NewGuid():N}";
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        ICLCAdmissionGrain grain = GetAdmission(admissionId);

        await grain.AdmitPatientAsync(
            patientId, "DOE,JOHN", new DateTime(1940, 5, 10),
            DateTime.UtcNow, CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.SkilledNursing,
            "CLC Ward 3B", "Room 315-A",
            "Dr. Adams", "Hip fracture, s/p ORIF",
            "VA Main Hospital",
            DateTime.UtcNow.AddDays(30),
            "Post-surgical rehabilitation");

        CLCAdmissionState state = await grain.GetAdmissionAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.AdmitSource, Is.EqualTo(CLCAdmitSource.AcuteHospital));
        Assert.That(state.LevelOfCare, Is.EqualTo(GECLevelOfCare.SkilledNursing));
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.Active));
        Assert.That(state.Ward, Is.EqualTo("CLC Ward 3B"));
        Assert.That(state.AttendingPhysician, Is.EqualTo("Dr. Adams"));
    }

    [Test]
    public async Task UpdateLevelOfCare_ChangesLevel()
    {
        string admissionId = $"CLC-ADMIT-{Guid.NewGuid():N}";
        ICLCAdmissionGrain grain = GetAdmission(admissionId);

        await grain.AdmitPatientAsync(
            "PAT-CLC-1", "SMITH,JANE", null,
            DateTime.UtcNow, CLCAdmitSource.Community,
            GECLevelOfCare.Respite, "CLC Ward 1", "Room 101",
            "Dr. Brown", "Caregiver respite", string.Empty, null, string.Empty);

        await grain.UpdateLevelOfCareAsync(GECLevelOfCare.LongTermCare);

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.LevelOfCare, Is.EqualTo(GECLevelOfCare.LongTermCare));
    }

    [Test]
    public async Task DischargePatient_SetsStatusAndDestination()
    {
        string admissionId = $"CLC-ADMIT-{Guid.NewGuid():N}";
        ICLCAdmissionGrain grain = GetAdmission(admissionId);

        await grain.AdmitPatientAsync(
            "PAT-CLC-2", "GREEN,BOB", null,
            DateTime.UtcNow.AddDays(-14), CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.SubAcuteRehabilitation, "CLC Ward 2", "Room 210",
            "Dr. Davis", "Stroke rehab", "VA ER", null, string.Empty);

        await grain.DischargePatientAsync(CLCDischargeDestination.Home, "Patient met all rehab goals");

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.Discharged));
        Assert.That(state.DischargeDestination, Is.EqualTo(CLCDischargeDestination.Home));
        Assert.That(state.DischargeNotes, Does.Contain("rehab goals"));
        Assert.That(state.ActualDischargeDate, Is.Not.Null);
    }

    [Test]
    public async Task MarkOnLeaveAndReturn_TransitionsCorrectly()
    {
        string admissionId = $"CLC-ADMIT-{Guid.NewGuid():N}";
        ICLCAdmissionGrain grain = GetAdmission(admissionId);

        await grain.AdmitPatientAsync(
            "PAT-CLC-3", "WHITE,TOM", null,
            DateTime.UtcNow, CLCAdmitSource.Community,
            GECLevelOfCare.LongTermCare, "CLC Ward 4", "Room 401",
            "Dr. Wilson", "Alzheimer's disease", string.Empty, null, string.Empty);

        await grain.MarkOnLeaveAsync();
        CLCAdmissionState stateLeave = await grain.GetAdmissionAsync();
        Assert.That(stateLeave.Status, Is.EqualTo(CLCAdmissionStatus.OnLeave));

        await grain.ReturnFromLeaveAsync();
        CLCAdmissionState stateReturned = await grain.GetAdmissionAsync();
        Assert.That(stateReturned.Status, Is.EqualTo(CLCAdmissionStatus.Active));
    }

    // ── CLC Admission Index Tests ────────────────────────────────────────────

    [Test]
    public async Task AdmissionIndex_QueryByWard()
    {
        ICLCAdmissionIndexGrain index = GetAdmissionIndex();

        string admissionId = $"CLC-ADMIT-{Guid.NewGuid():N}";
        await index.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = admissionId,
            PatientId = "PAT-CIDX-1", PatientName = "TEST,WARD",
            AdmitDate = DateTime.UtcNow,
            LevelOfCare = GECLevelOfCare.SkilledNursing,
            Status = CLCAdmissionStatus.Active,
            Ward = "CLC Ward 3B", BedRoom = "Room 310",
            AttendingPhysician = "Dr. Test"
        });

        List<CLCAdmissionIndexEntry> results = await index.GetAdmissionsByWardAsync("CLC Ward 3B");
        Assert.That(results.Any(a => a.AdmissionId == admissionId), Is.True);
    }
}
