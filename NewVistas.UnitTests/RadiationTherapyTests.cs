// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// RadiationTherapyCourseGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class RadiationTherapyCourseGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IRadiationTherapyCourseGrain NewCourse() =>
        _cluster.GrainFactory.GetGrain<IRadiationTherapyCourseGrain>($"RT-COURSE:{Guid.NewGuid()}");

    // ── Create / Basic ─────────────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_CanCreateCourse()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();

        await grain.CreateCourseAsync(
            "PAT-001", "Prostate IMRT 2025", "C61", "Malignant neoplasm of prostate",
            "Prostate and seminal vesicles", RtLaterality.NotApplicable,
            RtIntent.Curative, RtModality.IMRT,
            prescribedDoseCgy: 7600, fractionsPlanned: 38, dosePerFractionCgy: 200,
            beamEnergy: "6 MV",
            oncologistId: "ONCO-001", oncologistName: "Dr. Oncology",
            physicistId: "PHYS-001", physicistName: "Dr. Physics",
            dosimetristId: null, dosimetristName: null,
            treatmentMachineId: "LINAC-1", treatmentMachineName: "Varian TrueBeam",
            planningNotes: "IMRT plan with 7-field arrangement");

        RtCourseState state = await grain.GetCourseAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.CourseName, Is.EqualTo("Prostate IMRT 2025"));
        Assert.That(state.DiagnosisCode, Is.EqualTo("C61"));
        Assert.That(state.TreatmentSite, Is.EqualTo("Prostate and seminal vesicles"));
        Assert.That(state.Laterality, Is.EqualTo(RtLaterality.NotApplicable));
        Assert.That(state.Intent, Is.EqualTo(RtIntent.Curative));
        Assert.That(state.Modality, Is.EqualTo(RtModality.IMRT));
        Assert.That(state.PrescribedDoseCgy, Is.EqualTo(7600));
        Assert.That(state.FractionsPlanned, Is.EqualTo(38));
        Assert.That(state.DosePerFractionCgy, Is.EqualTo(200));
        Assert.That(state.BeamEnergy, Is.EqualTo("6 MV"));
        Assert.That(state.OncologistName, Is.EqualTo("Dr. Oncology"));
        Assert.That(state.TreatmentMachineName, Is.EqualTo("Varian TrueBeam"));
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Planned));
        Assert.That(state.TotalDeliveredDoseCgy, Is.EqualTo(0));
        Assert.That(state.FractionsCompleted, Is.EqualTo(0));
    }

    [Test]
    public async Task RtCourseGrain_CourseId_MatchesGrainKey()
    {
        string key = $"RT-COURSE:{Guid.NewGuid()}";
        IRadiationTherapyCourseGrain grain = _cluster.GrainFactory.GetGrain<IRadiationTherapyCourseGrain>(key);
        await grain.CreateCourseAsync(
            "PAT-002", "Breast VMAT", "C50.9", "Breast cancer",
            "Left breast and axilla", RtLaterality.Left,
            RtIntent.Adjuvant, RtModality.VMAT,
            4500, 25, 180, "6 MV",
            null, null, null, null, null, null, null, null, null);

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.CourseId, Is.EqualTo(key));
    }

    [Test]
    public async Task RtCourseGrain_DefaultStatus_IsPlanned()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-003", "Brain SRS", "C71.9", "Malignant brain tumor",
            "Brain mets", RtLaterality.NotApplicable,
            RtIntent.Palliative, RtModality.SRS,
            2400, 1, 2400, "6 MV FFF",
            null, null, null, null, null, null, null, null, null);

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Planned));
    }

    // ── Simulation ─────────────────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_RecordSimulation_SetsDateAndStatus()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-010", "Lung SBRT", "C34.1", "Lung cancer",
            "Right upper lobe", RtLaterality.Right,
            RtIntent.Curative, RtModality.SBRT,
            5400, 3, 1800, "6 MV FFF",
            null, "Dr. Lung", null, null, null, null, null, "Varian Edge", null);

        DateTime simDate = new DateTime(2025, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        await grain.RecordSimulationAsync(simDate, "4D CT simulation with Abches respiratory gating");

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.SimulationDate, Is.EqualTo(simDate));
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Simulated));
        Assert.That(state.PlanningNotes, Does.Contain("4D CT"));
    }

    // ── Course lifecycle ───────────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_StartCourse_SetsStartDateAndStatus()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-020", "Prostate HDR Brachy", "C61", "Prostate cancer",
            "Prostate", RtLaterality.NotApplicable,
            RtIntent.Curative, RtModality.Brachytherapy,
            3600, 3, 1200, null,
            null, "Dr. Brachy", null, null, null, null, null, null, null);

        DateTime startDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await grain.StartCourseAsync(startDate);

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.TreatmentStartDate, Is.EqualTo(startDate));
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Active));
    }

    [Test]
    public async Task RtCourseGrain_CompleteCourse_SetsCompletionAndStatus()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-030", "Head-Neck VMAT", "C32.0", "Laryngeal cancer",
            "Larynx, pharynx, bilateral neck", RtLaterality.Bilateral,
            RtIntent.Curative, RtModality.VMAT,
            7000, 35, 200, "6 MV",
            null, null, null, null, null, null, null, null, null);
        await grain.StartCourseAsync(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        DateTime completionDate = new DateTime(2025, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        await grain.CompleteCourseAsync(completionDate, "Course completed without interruptions.");

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.TreatmentCompletionDate, Is.EqualTo(completionDate));
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Completed));
        Assert.That(state.Notes, Does.Contain("completed"));
    }

    [Test]
    public async Task RtCourseGrain_DiscontinueCourse_SetsDiscontinuationAndStatus()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-040", "Cervix IMRT", "C53.9", "Cervical cancer",
            "Pelvis", RtLaterality.NotApplicable,
            RtIntent.Curative, RtModality.IMRT,
            4500, 25, 180, "6 MV",
            null, null, null, null, null, null, null, null, null);
        await grain.StartCourseAsync(DateTime.UtcNow.AddDays(-7));

        DateTime discDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await grain.DiscontinueCourseAsync(discDate, "Patient request — moved out of area", "Patient relocated.");

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.DiscontinuationDate, Is.EqualTo(discDate));
        Assert.That(state.DiscontinuationReason, Is.EqualTo("Patient request — moved out of area"));
        Assert.That(state.Status, Is.EqualTo(RtCourseStatus.Discontinued));
    }

    [Test]
    public async Task RtCourseGrain_HoldAndResume()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-050", "Rectal VMAT", "C20", "Rectal cancer",
            "Pelvis", RtLaterality.NotApplicable,
            RtIntent.Neoadjuvant, RtModality.VMAT,
            4500, 25, 180, "6 MV",
            null, null, null, null, null, null, null, null, null);
        await grain.StartCourseAsync(DateTime.UtcNow.AddDays(-5));

        await grain.PlaceCourseOnHoldAsync("Acute toxicity — radiation proctitis");
        RtCourseState held = await grain.GetCourseAsync();
        Assert.That(held.Status, Is.EqualTo(RtCourseStatus.OnHold));

        await grain.ResumeCourseAsync();
        RtCourseState resumed = await grain.GetCourseAsync();
        Assert.That(resumed.Status, Is.EqualTo(RtCourseStatus.Active));
    }

    // ── Fraction dose tracking ─────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_RecordFractionDelivered_AccumulatesDose()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-060", "Endometrial IMRT", "C54.1", "Endometrial cancer",
            "Pelvis", RtLaterality.NotApplicable,
            RtIntent.Adjuvant, RtModality.IMRT,
            4500, 25, 180, "6 MV",
            null, null, null, null, null, null, null, null, null);

        await grain.RecordFractionDeliveredAsync(180);
        await grain.RecordFractionDeliveredAsync(180);
        await grain.RecordFractionDeliveredAsync(180);

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.TotalDeliveredDoseCgy, Is.EqualTo(540));
        Assert.That(state.FractionsCompleted, Is.EqualTo(3));
    }

    // ── Boost ──────────────────────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_SetBoost_StoresBoostDetails()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-070", "Breast VMAT w/Boost", "C50.9", "Breast cancer",
            "Left breast", RtLaterality.Left,
            RtIntent.Adjuvant, RtModality.VMAT,
            4600, 23, 200, "6 MV",
            null, null, null, null, null, null, null, null, null);

        await grain.SetBoostAsync("Tumor bed", 1400, 7);

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.BoostFlag, Is.True);
        Assert.That(state.BoostSite, Is.EqualTo("Tumor bed"));
        Assert.That(state.BoostDoseCgy, Is.EqualTo(1400));
        Assert.That(state.BoostFractionsPlanned, Is.EqualTo(7));
    }

    // ── Brachytherapy ──────────────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_SetBrachytherapy_StoresDoseRate()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-080", "Prostate LDR Seeds", "C61", "Prostate cancer",
            "Prostate", RtLaterality.NotApplicable,
            RtIntent.Curative, RtModality.Brachytherapy,
            14500, 1, 14500, null,
            null, null, null, null, null, null, null, null, null);

        await grain.SetBrachytherapyAsync(BrachytherapyDoseRate.LDR, "I-125");

        RtCourseState state = await grain.GetCourseAsync();
        Assert.That(state.BrachyDoseRate, Is.EqualTo(BrachytherapyDoseRate.LDR));
        Assert.That(state.BrachyIsotope, Is.EqualTo("I-125"));
    }

    // ── LastModifiedDate ───────────────────────────────────────────────────

    [Test]
    public async Task RtCourseGrain_LastModifiedDate_UpdatesOnWrite()
    {
        IRadiationTherapyCourseGrain grain = NewCourse();
        await grain.CreateCourseAsync(
            "PAT-090", "Bladder VMAT", "C67.9", "Bladder cancer",
            "Bladder and pelvis", RtLaterality.NotApplicable,
            RtIntent.Curative, RtModality.VMAT,
            6400, 32, 200, "6 MV",
            null, null, null, null, null, null, null, null, null);

        RtCourseState before = await grain.GetCourseAsync();
        await Task.Delay(10);
        await grain.StartCourseAsync(DateTime.UtcNow);

        RtCourseState after = await grain.GetCourseAsync();
        Assert.That(after.LastModifiedDate, Is.GreaterThan(before.LastModifiedDate));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RadiationTherapyTreatmentGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class RadiationTherapyTreatmentGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IRadiationTherapyTreatmentGrain NewTx() =>
        _cluster.GrainFactory.GetGrain<IRadiationTherapyTreatmentGrain>($"RT-TX:{Guid.NewGuid()}");

    // ── Record delivery ────────────────────────────────────────────────────

    [Test]
    public async Task RtTreatmentGrain_CanRecordDelivery()
    {
        IRadiationTherapyTreatmentGrain grain = NewTx();
        DateTime txDate = new DateTime(2025, 6, 2, 9, 30, 0, DateTimeKind.Utc);

        await grain.RecordDeliveryAsync(
            courseId: "RT-COURSE:abc",
            patientId: "PAT-001",
            fractionNumber: 1,
            treatmentDate: txDate,
            doseDeliveredCgy: 200,
            treatmentDurationMin: 12,
            machineId: "LINAC-1",
            machineName: "Varian TrueBeam",
            technicianId: "TECH-001",
            technicianName: "T. Smith",
            setupVerified: true,
            setupMethod: "kV CBCT",
            setupDeviationMm: 2.1m,
            interrupted: false,
            interruptionReason: null,
            notes: null);

        RtTreatmentState state = await grain.GetTreatmentAsync();

        Assert.That(state.CourseId, Is.EqualTo("RT-COURSE:abc"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.FractionNumber, Is.EqualTo(1));
        Assert.That(state.TreatmentDate, Is.EqualTo(txDate));
        Assert.That(state.DoseDeliveredCgy, Is.EqualTo(200));
        Assert.That(state.TreatmentDurationMin, Is.EqualTo(12));
        Assert.That(state.MachineName, Is.EqualTo("Varian TrueBeam"));
        Assert.That(state.TechnicianName, Is.EqualTo("T. Smith"));
        Assert.That(state.SetupVerified, Is.True);
        Assert.That(state.SetupMethod, Is.EqualTo("kV CBCT"));
        Assert.That(state.SetupDeviationMm, Is.EqualTo(2.1m));
        Assert.That(state.Interrupted, Is.False);
        Assert.That(state.Status, Is.EqualTo(RtFractionStatus.Delivered));
    }

    [Test]
    public async Task RtTreatmentGrain_TreatmentId_MatchesGrainKey()
    {
        string key = $"RT-TX:{Guid.NewGuid()}";
        IRadiationTherapyTreatmentGrain grain = _cluster.GrainFactory.GetGrain<IRadiationTherapyTreatmentGrain>(key);
        await grain.RecordDeliveryAsync(
            "RT-COURSE:xyz", "PAT-002", 1, DateTime.UtcNow, 180,
            null, null, null, null, null, false, null, null, false, null, null);

        RtTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.TreatmentId, Is.EqualTo(key));
    }

    [Test]
    public async Task RtTreatmentGrain_CanRecordInterruption()
    {
        IRadiationTherapyTreatmentGrain grain = NewTx();
        await grain.RecordDeliveryAsync(
            "RT-COURSE:abc", "PAT-003", 5, DateTime.UtcNow, 200,
            15, null, "Elekta Versa", null, null,
            true, "Portal imaging", null,
            interrupted: true,
            interruptionReason: "Machine fault — power outage",
            notes: "Partial delivery, re-planned next day");

        RtTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.Interrupted, Is.True);
        Assert.That(state.InterruptionReason, Does.Contain("power outage"));
        Assert.That(state.Status, Is.EqualTo(RtFractionStatus.Delivered));
    }

    // ── Skip / Cancel ──────────────────────────────────────────────────────

    [Test]
    public async Task RtTreatmentGrain_CanRecordSkippedFraction()
    {
        IRadiationTherapyTreatmentGrain grain = NewTx();
        await grain.RecordSkipAsync(
            "RT-COURSE:abc", "PAT-004", 3,
            new DateTime(2025, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            RtFractionStatus.Skipped,
            "Patient unwell — nausea/vomiting");

        RtTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.FractionNumber, Is.EqualTo(3));
        Assert.That(state.Status, Is.EqualTo(RtFractionStatus.Skipped));
        Assert.That(state.SkipReason, Does.Contain("nausea"));
        Assert.That(state.DoseDeliveredCgy, Is.EqualTo(0));
    }

    [Test]
    public async Task RtTreatmentGrain_CanRecordCancelledFraction()
    {
        IRadiationTherapyTreatmentGrain grain = NewTx();
        await grain.RecordSkipAsync(
            "RT-COURSE:def", "PAT-005", 10,
            DateTime.UtcNow,
            RtFractionStatus.Cancelled,
            "Machine down for scheduled maintenance");

        RtTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.Status, Is.EqualTo(RtFractionStatus.Cancelled));
        Assert.That(state.SkipReason, Does.Contain("maintenance"));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RadiationTherapyTreatmentIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class RadiationTherapyTreatmentIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IRadiationTherapyTreatmentIndexGrain NewTxIndex() =>
        _cluster.GrainFactory.GetGrain<IRadiationTherapyTreatmentIndexGrain>($"RT-TX-IDX:{Guid.NewGuid()}");

    private static RtTreatmentIndexEntry MakeEntry(string id, int fractionNum, RtFractionStatus status, int dose = 200) =>
        new()
        {
            TreatmentId    = id,
            FractionNumber = fractionNum,
            TreatmentDate  = DateTime.UtcNow.AddDays(-fractionNum),
            Status         = status,
            DoseDeliveredCgy = status == RtFractionStatus.Delivered ? dose : 0,
            MachineName    = "TrueBeam",
            TechnicianName = "T. Smith",
            SetupVerified  = status == RtFractionStatus.Delivered,
            Notes          = null
        };

    [Test]
    public async Task RtTreatmentIndexGrain_StartsEmpty()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        List<RtTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task RtTreatmentIndexGrain_UpsertAndRetrieve()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        RtTreatmentIndexEntry entry = MakeEntry("RT-TX-1", 1, RtFractionStatus.Delivered);
        await index.UpsertTreatmentAsync(entry);

        List<RtTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TreatmentId, Is.EqualTo("RT-TX-1"));
    }

    [Test]
    public async Task RtTreatmentIndexGrain_OrderedByFractionNumberAscending()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-A", 3, RtFractionStatus.Delivered));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-B", 1, RtFractionStatus.Delivered));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-C", 2, RtFractionStatus.Delivered));

        List<RtTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all[0].FractionNumber, Is.EqualTo(1));
        Assert.That(all[1].FractionNumber, Is.EqualTo(2));
        Assert.That(all[2].FractionNumber, Is.EqualTo(3));
    }

    [Test]
    public async Task RtTreatmentIndexGrain_FilterDelivered()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-D1", 1, RtFractionStatus.Delivered));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-D2", 2, RtFractionStatus.Skipped));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-D3", 3, RtFractionStatus.Delivered));

        List<RtTreatmentIndexEntry> delivered = await index.GetDeliveredTreatmentsAsync();
        Assert.That(delivered, Has.Count.EqualTo(2));
        Assert.That(delivered.All(t => t.Status == RtFractionStatus.Delivered), Is.True);
    }

    [Test]
    public async Task RtTreatmentIndexGrain_TotalDeliveredDose_SumsDeliveredOnly()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-E1", 1, RtFractionStatus.Delivered, 200));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-E2", 2, RtFractionStatus.Skipped, 0));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-E3", 3, RtFractionStatus.Delivered, 200));
        await index.UpsertTreatmentAsync(MakeEntry("RT-TX-E4", 4, RtFractionStatus.Delivered, 200));

        int total = await index.GetTotalDeliveredDoseCgyAsync();
        int count = await index.GetDeliveredFractionCountAsync();

        Assert.That(total, Is.EqualTo(600));
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task RtTreatmentIndexGrain_RemoveTreatment()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        string id = $"RT-TX:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry(id, 1, RtFractionStatus.Delivered));
        await index.RemoveTreatmentAsync(id);

        List<RtTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task RtTreatmentIndexGrain_UpsertUpdatesExisting()
    {
        IRadiationTherapyTreatmentIndexGrain index = NewTxIndex();
        string id = $"RT-TX:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry(id, 1, RtFractionStatus.Scheduled, 0));
        await index.UpsertTreatmentAsync(MakeEntry(id, 1, RtFractionStatus.Delivered, 200));

        List<RtTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(RtFractionStatus.Delivered));
        Assert.That(all[0].DoseDeliveredCgy, Is.EqualTo(200));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RadiationTherapyCourseIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class RadiationTherapyCourseIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IRadiationTherapyCourseIndexGrain NewCourseIndex() =>
        _cluster.GrainFactory.GetGrain<IRadiationTherapyCourseIndexGrain>($"RT-COURSE-IDX:{Guid.NewGuid()}");

    private static RtCourseIndexEntry MakeEntry(string id, RtCourseStatus status, DateTime? startDate = null) =>
        new()
        {
            CourseId               = id,
            CourseName             = "Test Course",
            Status                 = status,
            Intent                 = RtIntent.Curative,
            Modality               = RtModality.IMRT,
            TreatmentSite          = "Pelvis",
            DiagnosisCode          = "C61",
            PrescribedDoseCgy      = 7600,
            FractionsPlanned       = 38,
            TotalDeliveredDoseCgy  = 0,
            FractionsCompleted     = 0,
            TreatmentStartDate     = startDate,
            TreatmentCompletionDate = status == RtCourseStatus.Completed ? startDate?.AddDays(40) : null,
            OncologistName         = "Dr. Test"
        };

    [Test]
    public async Task RtCourseIndexGrain_StartsEmpty()
    {
        IRadiationTherapyCourseIndexGrain index = NewCourseIndex();
        List<RtCourseIndexEntry> all = await index.GetAllCoursesAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task RtCourseIndexGrain_UpsertAndRetrieve()
    {
        IRadiationTherapyCourseIndexGrain index = NewCourseIndex();
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-A", RtCourseStatus.Active, DateTime.UtcNow));

        List<RtCourseIndexEntry> all = await index.GetAllCoursesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].CourseId, Is.EqualTo("RT-COURSE-A"));
    }

    [Test]
    public async Task RtCourseIndexGrain_FilterActive()
    {
        IRadiationTherapyCourseIndexGrain index = NewCourseIndex();
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-P1", RtCourseStatus.Active, DateTime.UtcNow));
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-P2", RtCourseStatus.Completed, DateTime.UtcNow.AddDays(-60)));
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-P3", RtCourseStatus.OnHold, DateTime.UtcNow.AddDays(-10)));

        List<RtCourseIndexEntry> active = await index.GetActiveCoursesAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(c => c.Status == RtCourseStatus.Active || c.Status == RtCourseStatus.OnHold), Is.True);
    }

    [Test]
    public async Task RtCourseIndexGrain_FilterCompleted()
    {
        IRadiationTherapyCourseIndexGrain index = NewCourseIndex();
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-Q1", RtCourseStatus.Active, DateTime.UtcNow));
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-Q2", RtCourseStatus.Completed, DateTime.UtcNow.AddDays(-90)));
        await index.UpsertCourseAsync(MakeEntry("RT-COURSE-Q3", RtCourseStatus.Discontinued, DateTime.UtcNow.AddDays(-30)));

        List<RtCourseIndexEntry> completed = await index.GetCompletedCoursesAsync();
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(completed[0].Status, Is.EqualTo(RtCourseStatus.Completed));
    }

    [Test]
    public async Task RtCourseIndexGrain_RemoveCourse()
    {
        IRadiationTherapyCourseIndexGrain index = NewCourseIndex();
        string id = $"RT-COURSE:{Guid.NewGuid()}";
        await index.UpsertCourseAsync(MakeEntry(id, RtCourseStatus.Planned));
        await index.RemoveCourseAsync(id);

        List<RtCourseIndexEntry> all = await index.GetAllCoursesAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task RtCourseIndexGrain_RemoveNonExistent_IsIdempotent()
    {
        IRadiationTherapyCourseIndexGrain index = NewCourseIndex();
        Assert.DoesNotThrowAsync(() => index.RemoveCourseAsync("RT-COURSE:nonexistent"));
    }
}
