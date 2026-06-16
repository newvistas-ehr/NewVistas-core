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
/// Functional tests for Prenatal / OB — IHS Prenatal Care Module.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class PrenatalWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Create pregnancy ────────────────────────────────────────────────────

    [Test]
    public async Task CreatePregnancy_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime edd = new DateTime(2025, 9, 15);
        string pregId = await wf.CreatePregnancyAsync(
            lastMenstrualPeriod: new DateTime(2024, 12, 9),
            eddByLmp: edd,
            eddByUltrasound: null,
            definitiveEdd: edd,
            gravida: 1, para: 0, abortions: 0, living: 0,
            riskLevel: PregnancyRiskLevel.Low,
            riskFactors: null,
            providerId: null, providerName: "Dr. Adams",
            locationId: null, locationName: "OB Clinic",
            notes: null);

        Assert.That(pregId, Does.StartWith("OB-PREG:"));

        List<PregnancyIndexEntry> index = await wf.GetPregnanciesAsync();
        Assert.That(index, Has.Count.EqualTo(1));
        Assert.That(index[0].PregnancyId, Is.EqualTo(pregId));
        Assert.That(index[0].Status, Is.EqualTo(PregnancyStatus.Active));
        Assert.That(index[0].DefinitiveEdd, Is.EqualTo(edd));
    }

    [Test]
    public async Task CreatePregnancy_HighRisk_IndexReflectsRiskLevel()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 10, 1),
            3, 2, 0, 2,
            PregnancyRiskLevel.High,
            new List<string> { "Advanced maternal age", "Diabetes" },
            null, "Dr. Chen", null, null, null);

        PregnancyIndexEntry? active = await wf.GetActivePregnancyAsync();

        Assert.That(active, Is.Not.Null);
        Assert.That(active!.RiskLevel, Is.EqualTo(PregnancyRiskLevel.High));
        Assert.That(active.Gravida, Is.EqualTo(3));
    }

    // ── Get pregnancy detail ────────────────────────────────────────────────

    [Test]
    public async Task GetPregnancy_ReturnsFullState()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime lmp = new DateTime(2024, 11, 1);
        DateTime edd = new DateTime(2025, 8, 8);

        string pregId = await wf.CreatePregnancyAsync(
            lmp, edd, null, edd,
            2, 1, 0, 1,
            PregnancyRiskLevel.Low, null,
            null, "Dr. Park", null, null, "Second pregnancy");

        PregnancyState state = await wf.GetPregnancyAsync(pregId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.LastMenstrualPeriod, Is.EqualTo(lmp));
        Assert.That(state.Gravida, Is.EqualTo(2));
        Assert.That(state.Para, Is.EqualTo(1));
        Assert.That(state.Notes, Is.EqualTo("Second pregnancy"));
    }

    // ── Update risk ─────────────────────────────────────────────────────────

    [Test]
    public async Task UpdatePregnancyRisk_ChangesRiskLevel_AndSyncsToIndex()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await wf.UpdatePregnancyRiskAsync(pregId,
            PregnancyRiskLevel.High,
            new List<string> { "Pre-eclampsia risk", "Twin pregnancy" });

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.RiskLevel, Is.EqualTo(PregnancyRiskLevel.High));
        Assert.That(state.RiskFactors, Has.Count.EqualTo(2));

        List<PregnancyIndexEntry> index = await wf.GetPregnanciesAsync();
        Assert.That(index[0].RiskLevel, Is.EqualTo(PregnancyRiskLevel.High));
    }

    // ── Add & resolve problems ──────────────────────────────────────────────

    [Test]
    public async Task AddPrenatalProblem_AppearsonPregnancyState()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        PrenatalProblemEntry problem = new()
        {
            ProblemId = "PROB-WF-001",
            Description = "Gestational diabetes",
            Priority = PrenatalProblemPriority.High,
            Scope = PrenatalProblemScope.CurrentPregnancy,
            IsActive = true,
        };

        await wf.AddPrenatalProblemAsync(pregId, problem);

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.Problems, Has.Count.EqualTo(1));
        Assert.That(state.Problems[0].Description, Is.EqualTo("Gestational diabetes"));
    }

    [Test]
    public async Task ResolvePrenatalProblem_MarksInactive()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        PrenatalProblemEntry problem = new()
        {
            ProblemId = "PROB-WF-RES",
            Description = "Nausea",
            IsActive = true,
        };

        await wf.AddPrenatalProblemAsync(pregId, problem);
        await wf.ResolvePrenatalProblemAsync(pregId, "PROB-WF-RES");

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.Problems[0].IsActive, Is.False);
    }

    // ── Record delivery ─────────────────────────────────────────────────────

    [Test]
    public async Task RecordDelivery_TransitionsToDelivered_SyncsIndex()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 7, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        DeliveryInfo delivery = new()
        {
            DeliveryDate = new DateTime(2025, 7, 10, 14, 30, 0),
            DeliveryMethod = DeliveryMethod.SpontaneousVaginal,
            GestationalAgeAtDeliveryWeeks = 39,
            BirthWeightGrams = 3200,
            Apgar1Min = 8,
            Apgar5Min = 9,
            Presentation = FetalPresentation.Cephalic,
            InfantSex = "M",
        };

        await wf.RecordDeliveryAsync(pregId, delivery, PregnancyOutcome.LiveBirth);

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Delivered));
        Assert.That(state.Outcome, Is.EqualTo(PregnancyOutcome.LiveBirth));
        Assert.That(state.Delivery!.BirthWeightGrams, Is.EqualTo(3200));

        List<PregnancyIndexEntry> index = await wf.GetPregnanciesAsync();
        Assert.That(index[0].Status, Is.EqualTo(PregnancyStatus.Delivered));
        Assert.That(index[0].Outcome, Is.EqualTo(PregnancyOutcome.LiveBirth));
    }

    // ── Record postpartum ───────────────────────────────────────────────────

    [Test]
    public async Task RecordPostpartum_TransitionsToPostpartum_SyncsIndex()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 7, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await wf.RecordDeliveryAsync(pregId,
            new DeliveryInfo { DeliveryDate = new DateTime(2025, 7, 10) },
            PregnancyOutcome.LiveBirth);

        PostpartumInfo postpartum = new()
        {
            PostpartumVisitDate = new DateTime(2025, 8, 7),
            BreastfeedingStatus = "Partial",
            ContraceptiveMethod = "Oral Contraceptives",
            EpdsScore = 5,
        };

        await wf.RecordPostpartumAsync(pregId, postpartum);

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Postpartum));
        Assert.That(state.Postpartum!.BreastfeedingStatus, Is.EqualTo("Partial"));

        List<PregnancyIndexEntry> index = await wf.GetPregnanciesAsync();
        Assert.That(index[0].Status, Is.EqualTo(PregnancyStatus.Postpartum));
    }

    // ── Update status ───────────────────────────────────────────────────────

    [Test]
    public async Task UpdatePregnancyStatus_CancelledStatus_SyncsIndex()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await wf.UpdatePregnancyStatusAsync(pregId, PregnancyStatus.Ectopic);

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Ectopic));

        List<PregnancyIndexEntry> index = await wf.GetPregnanciesAsync();
        Assert.That(index[0].Status, Is.EqualTo(PregnancyStatus.Ectopic));
    }

    // ── Update EDD ──────────────────────────────────────────────────────────

    [Test]
    public async Task UpdatePregnancyEdd_ChangesDefinitiveEdd()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        DateTime newEdd = new DateTime(2025, 8, 25);
        await wf.UpdatePregnancyEddAsync(pregId, newEdd, newEdd);

        PregnancyState state = await wf.GetPregnancyAsync(pregId);
        Assert.That(state.DefinitiveEdd, Is.EqualTo(newEdd));
        Assert.That(state.EddByUltrasound, Is.EqualTo(newEdd));
    }

    // ── Prenatal Visits ─────────────────────────────────────────────────────

    [Test]
    public async Task CreatePrenatalVisit_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        string visitId = await wf.CreatePrenatalVisitAsync(
            pregId,
            visitDate: new DateTime(2025, 3, 1),
            gestationalAgeWeeks: 16, gestationalAgeDays: 2,
            weight: 140m,
            bloodPressureSystolic: 118, bloodPressureDiastolic: 72,
            fundalHeightCm: 16m, fetalHeartRate: 152,
            FetalPresentation.Unknown, fetalMovement: null,
            urineProtein: "Negative", urineGlucose: "Negative", edema: "None",
            cervicalDilationCm: null, cervicalEffacementPercent: null, fetalStation: null,
            providerId: null, providerName: "Dr. Williams",
            notes: "Routine 16-week visit", nextVisitDate: new DateTime(2025, 4, 1));

        Assert.That(visitId, Does.StartWith("OB-VISIT:"));

        List<PrenatalVisitIndexEntry> visits = await wf.GetPrenatalVisitsAsync(pregId);
        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].VisitId, Is.EqualTo(visitId));
        Assert.That(visits[0].GestationalAgeWeeks, Is.EqualTo(16));
        Assert.That(visits[0].FetalHeartRate, Is.EqualTo(152));
    }

    [Test]
    public async Task CreateMultiplePrenatalVisits_FlowsheetOrder()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await wf.CreatePrenatalVisitAsync(
            pregId, new DateTime(2025, 1, 15), 12, 0,
            135m, 110, 70, 12m, 160,
            FetalPresentation.Unknown, null,
            null, null, null, null, null, null,
            null, "Dr. A", null, null);

        await wf.CreatePrenatalVisitAsync(
            pregId, new DateTime(2025, 2, 15), 16, 0,
            140m, 115, 72, 16m, 155,
            FetalPresentation.Unknown, null,
            null, null, null, null, null, null,
            null, "Dr. A", null, null);

        await wf.CreatePrenatalVisitAsync(
            pregId, new DateTime(2025, 3, 15), 20, 0,
            145m, 118, 75, 20m, 148,
            FetalPresentation.Cephalic, true,
            null, null, null, null, null, null,
            null, "Dr. A", null, null);

        List<PrenatalVisitIndexEntry> visits = await wf.GetPrenatalVisitsAsync(pregId);

        Assert.That(visits, Has.Count.EqualTo(3));
        // Newest first
        Assert.That(visits[0].GestationalAgeWeeks, Is.EqualTo(20));
        Assert.That(visits[1].GestationalAgeWeeks, Is.EqualTo(16));
        Assert.That(visits[2].GestationalAgeWeeks, Is.EqualTo(12));
    }

    [Test]
    public async Task GetPrenatalVisit_ReturnsFullState()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        string visitId = await wf.CreatePrenatalVisitAsync(
            pregId, new DateTime(2025, 5, 1), 32, 4,
            160m, 125, 80, 32m, 142,
            FetalPresentation.Cephalic, true,
            "Trace", "Negative", "Trace",
            null, null, null,
            null, "Dr. Brown", "Third trimester check", null);

        PrenatalVisitState state = await wf.GetPrenatalVisitAsync(visitId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PregnancyId, Is.EqualTo(pregId));
        Assert.That(state.GestationalAgeWeeks, Is.EqualTo(32));
        Assert.That(state.Weight, Is.EqualTo(160m));
        Assert.That(state.FetalHeartRate, Is.EqualTo(142));
        Assert.That(state.UrineProtein, Is.EqualTo("Trace"));
    }

    [Test]
    public async Task GetPrenatalVisitCount_ReturnsCorrectCount()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string pregId = await wf.CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        Assert.That(await wf.GetPrenatalVisitCountAsync(pregId), Is.EqualTo(0));

        for (int i = 0; i < 4; i++)
        {
            await wf.CreatePrenatalVisitAsync(
                pregId, DateTime.UtcNow.AddDays(-i * 14), 12 + (i * 4), 0,
                null, null, null, null, null,
                FetalPresentation.Unknown, null,
                null, null, null, null, null, null,
                null, null, null, null);
        }

        Assert.That(await wf.GetPrenatalVisitCountAsync(pregId), Is.EqualTo(4));
    }

    // ── Multi-patient isolation ─────────────────────────────────────────────

    [Test]
    public async Task MultiplePatients_IndependentPregnancyRecords()
    {
        string patient1 = $"PRENATAL-PAT-{Guid.NewGuid()}";
        string patient2 = $"PRENATAL-PAT-{Guid.NewGuid()}";

        await Workflow(patient1).CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 6, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await Workflow(patient2).CreatePregnancyAsync(
            null, null, null,
            new DateTime(2025, 9, 1),
            2, 1, 0, 1,
            PregnancyRiskLevel.High,
            new List<string> { "Twin pregnancy" },
            null, null, null, null, null);

        List<PregnancyIndexEntry> p1 = await Workflow(patient1).GetPregnanciesAsync();
        List<PregnancyIndexEntry> p2 = await Workflow(patient2).GetPregnanciesAsync();

        Assert.That(p1, Has.Count.EqualTo(1));
        Assert.That(p2, Has.Count.EqualTo(1));
        Assert.That(p1[0].Gravida, Is.EqualTo(1));
        Assert.That(p2[0].Gravida, Is.EqualTo(2));
        Assert.That(p2[0].RiskLevel, Is.EqualTo(PregnancyRiskLevel.High));
    }

    // ── Full pregnancy lifecycle ────────────────────────────────────────────

    [Test]
    public async Task FullPregnancyLifecycle_CreateToPostpartum()
    {
        string patientId = $"PRENATAL-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // 1. Create pregnancy
        string pregId = await wf.CreatePregnancyAsync(
            new DateTime(2024, 10, 1), new DateTime(2025, 7, 8), null,
            new DateTime(2025, 7, 8),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, "Dr. OB", null, "OB Clinic", null);

        Assert.That((await wf.GetActivePregnancyAsync())!.PregnancyId, Is.EqualTo(pregId));

        // 2. Add a visit
        string visitId = await wf.CreatePrenatalVisitAsync(
            pregId, new DateTime(2025, 1, 15), 16, 0,
            138m, 115, 72, 16m, 155,
            FetalPresentation.Unknown, null,
            "Negative", "Negative", "None",
            null, null, null,
            null, "Dr. OB", "Routine visit", null);

        Assert.That(await wf.GetPrenatalVisitCountAsync(pregId), Is.EqualTo(1));

        // 3. Add a problem
        await wf.AddPrenatalProblemAsync(pregId, new PrenatalProblemEntry
        {
            ProblemId = "PROB-LC-001",
            Description = "Gestational hypertension",
            Priority = PrenatalProblemPriority.High,
            Scope = PrenatalProblemScope.CurrentPregnancy,
            IsActive = true,
        });

        // 4. Update risk
        await wf.UpdatePregnancyRiskAsync(pregId, PregnancyRiskLevel.Moderate,
            new List<string> { "Gestational hypertension" });

        // 5. Record delivery
        await wf.RecordDeliveryAsync(pregId,
            new DeliveryInfo
            {
                DeliveryDate = new DateTime(2025, 7, 5, 8, 15, 0),
                DeliveryMethod = DeliveryMethod.SpontaneousVaginal,
                GestationalAgeAtDeliveryWeeks = 39,
                BirthWeightGrams = 3450,
                Apgar1Min = 9,
                Apgar5Min = 9,
                Presentation = FetalPresentation.Cephalic,
                InfantSex = "F",
            },
            PregnancyOutcome.LiveBirth);

        PregnancyState afterDelivery = await wf.GetPregnancyAsync(pregId);
        Assert.That(afterDelivery.Status, Is.EqualTo(PregnancyStatus.Delivered));

        // 6. Record postpartum
        await wf.RecordPostpartumAsync(pregId, new PostpartumInfo
        {
            PostpartumVisitDate = new DateTime(2025, 8, 2),
            BreastfeedingStatus = "Exclusive",
            ContraceptiveMethod = "IUD",
            EpdsScore = 2,
        });

        PregnancyState finalState = await wf.GetPregnancyAsync(pregId);
        Assert.That(finalState.Status, Is.EqualTo(PregnancyStatus.Postpartum));
        Assert.That(finalState.Postpartum!.EpdsScore, Is.EqualTo(2));
        Assert.That(finalState.Delivery!.BirthWeightGrams, Is.EqualTo(3450));

        // No active pregnancy should remain
        Assert.That(await wf.GetActivePregnancyAsync(), Is.Null);
    }
}
