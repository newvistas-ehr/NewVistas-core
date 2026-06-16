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
/// Functional tests for Radiation Therapy — VistA File #135.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class RadiationTherapyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Helper ─────────────────────────────────────────────────────────────────

    private Task<string> CreateCourse(IPatientWorkflowGrain wf)
        => wf.CreateRtCourseAsync(
            courseName: "Prostate IMRT 2025",
            diagnosisCode: "C61",
            diagnosisText: "Malignant neoplasm of prostate",
            treatmentSite: "Prostate and seminal vesicles",
            laterality: RtLaterality.Midline,
            intent: RtIntent.Curative,
            modality: RtModality.IMRT,
            prescribedDoseCgy: 7920,
            fractionsPlanned: 44,
            dosePerFractionCgy: 180,
            beamEnergy: "6 MV",
            oncologistId: "ONC-001",
            oncologistName: "Dr. Chen",
            physicistId: "PHY-001",
            physicistName: "Dr. Kumar",
            dosimetristId: "DOS-001",
            dosimetristName: "Amy Park",
            treatmentMachineId: "LINAC-01",
            treatmentMachineName: "TrueBeam 1",
            planningNotes: "4-field IMRT plan with daily CBCT.");

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateRtCourse_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);

        Assert.That(courseId, Is.Not.Null.And.Not.Empty);

        List<RtCourseIndexEntry> all = await wf.GetRtCoursesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].CourseId, Is.EqualTo(courseId));
        Assert.That(all[0].CourseName, Is.EqualTo("Prostate IMRT 2025"));
        Assert.That(all[0].Status, Is.EqualTo(RtCourseStatus.Planned));
        Assert.That(all[0].Modality, Is.EqualTo(RtModality.IMRT));
        Assert.That(all[0].PrescribedDoseCgy, Is.EqualTo(7920));
        Assert.That(all[0].FractionsPlanned, Is.EqualTo(44));
    }

    [Test]
    public async Task GetRtCourse_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);

        RtCourseState state = await wf.GetRtCourseAsync(courseId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.CourseName, Is.EqualTo("Prostate IMRT 2025"));
        Assert.That(state.DiagnosisCode, Is.EqualTo("C61"));
        Assert.That(state.TreatmentSite, Is.EqualTo("Prostate and seminal vesicles"));
        Assert.That(state.Laterality, Is.EqualTo(RtLaterality.Midline));
        Assert.That(state.Intent, Is.EqualTo(RtIntent.Curative));
        Assert.That(state.Modality, Is.EqualTo(RtModality.IMRT));
        Assert.That(state.DosePerFractionCgy, Is.EqualTo(180));
        Assert.That(state.BeamEnergy, Is.EqualTo("6 MV"));
        Assert.That(state.OncologistName, Is.EqualTo("Dr. Chen"));
        Assert.That(state.TreatmentMachineName, Is.EqualTo("TrueBeam 1"));
    }

    [Test]
    public async Task RecordRtSimulation_SetsDatesAndStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        DateTime simDate = new DateTime(2025, 4, 1);

        await wf.RecordRtSimulationAsync(courseId, simDate, "Supine, arms overhead, Vac-Lok immobilization.");

        RtCourseState state = await wf.GetRtCourseAsync(courseId);
        Assert.That(state.SimulationDate, Is.EqualTo(simDate));
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Simulated));
    }

    [Test]
    public async Task StartRtCourse_SetsActiveStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        DateTime startDate = new DateTime(2025, 4, 15);

        await wf.StartRtCourseAsync(courseId, startDate);

        RtCourseState state = await wf.GetRtCourseAsync(courseId);
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Active));
        Assert.That(state.TreatmentStartDate, Is.EqualTo(startDate));

        List<RtCourseIndexEntry> index = await wf.GetRtCoursesAsync();
        Assert.That(index[0].Status, Is.EqualTo(RtCourseStatus.Active));
    }

    [Test]
    public async Task RecordRtFraction_DeliveredFractionUpdatesCumulativeDose()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        await wf.StartRtCourseAsync(courseId, new DateTime(2025, 4, 15));

        string txId = await wf.RecordRtFractionAsync(
            courseId,
            fractionNumber: 1,
            treatmentDate: new DateTime(2025, 4, 15),
            doseDeliveredCgy: 180,
            treatmentDurationMin: 15,
            machineId: "LINAC-01",
            machineName: "TrueBeam 1",
            technicianId: "RTT-001",
            technicianName: "Sarah Johnson, RTT",
            setupVerified: true,
            setupMethod: "kV CBCT",
            setupDeviationMm: 1.5m,
            interrupted: false,
            interruptionReason: null,
            notes: null);

        Assert.That(txId, Is.Not.Null.And.Not.Empty);

        RtCourseState course = await wf.GetRtCourseAsync(courseId);
        Assert.That(course.TotalDeliveredDoseCgy, Is.EqualTo(180));
        Assert.That(course.FractionsCompleted, Is.EqualTo(1));

        List<RtTreatmentIndexEntry> treatments = await wf.GetRtCourseTreatmentsAsync(courseId);
        Assert.That(treatments, Has.Count.EqualTo(1));
        Assert.That(treatments[0].FractionNumber, Is.EqualTo(1));
        Assert.That(treatments[0].DoseDeliveredCgy, Is.EqualTo(180));
        Assert.That(treatments[0].Status, Is.EqualTo(RtFractionStatus.Delivered));
    }

    [Test]
    public async Task RecordRtSkippedFraction_DoesNotUpdateDose()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        await wf.StartRtCourseAsync(courseId, new DateTime(2025, 4, 15));

        // Deliver fraction 1
        await wf.RecordRtFractionAsync(
            courseId, 1, new DateTime(2025, 4, 15), 180, 15,
            "LINAC-01", "TrueBeam 1", null, null, true, "CBCT", null,
            false, null, null);

        // Skip fraction 2
        string skippedId = await wf.RecordRtSkippedFractionAsync(
            courseId, 2, new DateTime(2025, 4, 16),
            RtFractionStatus.Skipped, "Machine down for maintenance");

        Assert.That(skippedId, Is.Not.Null.And.Not.Empty);

        RtCourseState course = await wf.GetRtCourseAsync(courseId);
        Assert.That(course.TotalDeliveredDoseCgy, Is.EqualTo(180));
        Assert.That(course.FractionsCompleted, Is.EqualTo(1));

        List<RtTreatmentIndexEntry> all = await wf.GetRtCourseTreatmentsAsync(courseId);
        Assert.That(all, Has.Count.EqualTo(2));

        List<RtTreatmentIndexEntry> delivered = await wf.GetRtDeliveredFractionsAsync(courseId);
        Assert.That(delivered, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SetRtBoost_SetsBoostDetails()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);

        await wf.SetRtBoostAsync(courseId, "Prostate only", 1080, 6);

        RtCourseState state = await wf.GetRtCourseAsync(courseId);
        Assert.That(state.BoostFlag, Is.True);
        Assert.That(state.BoostSite, Is.EqualTo("Prostate only"));
        Assert.That(state.BoostDoseCgy, Is.EqualTo(1080));
        Assert.That(state.BoostFractionsPlanned, Is.EqualTo(6));
    }

    [Test]
    public async Task SetRtBrachytherapy_SetsBrachyDetails()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await wf.CreateRtCourseAsync(
            "Cervix HDR Brachy", "C53.9", "Malignant neoplasm of cervix",
            "Cervix", RtLaterality.Midline,
            RtIntent.Curative, RtModality.Brachytherapy,
            2400, 4, 600, null,
            "ONC-002", "Dr. Matsuda",
            null, null, null, null, null, null, null);

        await wf.SetRtBrachytherapyAsync(courseId, BrachytherapyDoseRate.HDR, "Ir-192");

        RtCourseState state = await wf.GetRtCourseAsync(courseId);
        Assert.That(state.BrachyDoseRate, Is.EqualTo(BrachytherapyDoseRate.HDR));
        Assert.That(state.BrachyIsotope, Is.EqualTo("Ir-192"));
    }

    [Test]
    public async Task PlaceAndResumeRtCourse_TransitionsOnHoldAndBack()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        await wf.StartRtCourseAsync(courseId, DateTime.UtcNow);

        await wf.PlaceRtCourseOnHoldAsync(courseId, "Acute mucositis — treatment break needed");

        RtCourseState onHold = await wf.GetRtCourseAsync(courseId);
        Assert.That(onHold.Status, Is.EqualTo(RtCourseStatus.OnHold));

        await wf.ResumeRtCourseAsync(courseId);

        RtCourseState resumed = await wf.GetRtCourseAsync(courseId);
        Assert.That(resumed.Status, Is.EqualTo(RtCourseStatus.Active));
    }

    [Test]
    public async Task CompleteRtCourse_SetsCompletedStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        await wf.StartRtCourseAsync(courseId, new DateTime(2025, 4, 15));

        DateTime completionDate = new DateTime(2025, 6, 15);
        await wf.CompleteRtCourseAsync(courseId, completionDate, "All 44 fractions delivered without interruption.");

        RtCourseState state = await wf.GetRtCourseAsync(courseId);
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Completed));
        Assert.That(state.TreatmentCompletionDate, Is.EqualTo(completionDate));

        List<RtCourseIndexEntry> index = await wf.GetRtCoursesAsync();
        Assert.That(index[0].Status, Is.EqualTo(RtCourseStatus.Completed));
    }

    [Test]
    public async Task DiscontinueRtCourse_SetsDiscontinuedWithReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string courseId = await CreateCourse(wf);
        await wf.StartRtCourseAsync(courseId, DateTime.UtcNow);

        DateTime dcDate = DateTime.UtcNow;
        await wf.DiscontinueRtCourseAsync(courseId, dcDate, "Disease progression on mid-treatment scan", null);

        RtCourseState state = await wf.GetRtCourseAsync(courseId);
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Discontinued));
        Assert.That(state.DiscontinuationDate, Is.EqualTo(dcDate));
        Assert.That(state.DiscontinuationReason, Does.Contain("progression"));
    }

    [Test]
    public async Task GetActiveRtCourses_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string course1 = await CreateCourse(wf);
        string course2 = await wf.CreateRtCourseAsync(
            "Palliative Spine", "C79.51", "Secondary malignant neoplasm of bone",
            "T12-L2 spine", RtLaterality.Midline,
            RtIntent.Palliative, RtModality.Photon3D,
            3000, 10, 300, "6 MV",
            null, null, null, null, null, null, null, null, null);

        await wf.StartRtCourseAsync(course1, DateTime.UtcNow);
        await wf.StartRtCourseAsync(course2, DateTime.UtcNow);
        await wf.CompleteRtCourseAsync(course2, DateTime.UtcNow, null);

        List<RtCourseIndexEntry> active = await wf.GetActiveRtCoursesAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].CourseId, Is.EqualTo(course1));
    }

    [Test]
    public async Task MultiplePatients_IndependentCourses()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await CreateCourse(wf1);
        await CreateCourse(wf2);
        await CreateCourse(wf2);

        List<RtCourseIndexEntry> p1Courses = await wf1.GetRtCoursesAsync();
        List<RtCourseIndexEntry> p2Courses = await wf2.GetRtCoursesAsync();

        Assert.That(p1Courses, Has.Count.EqualTo(1));
        Assert.That(p2Courses, Has.Count.EqualTo(2));
    }
}
