// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Prenatal / OB grain layer — IHS Prenatal Care Module.
/// Tests pregnancy, prenatal visit, and index grains directly via Orleans TestCluster.
/// </summary>
[TestFixture]
public class PrenatalGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Pregnancy Grain — creation ──────────────────────────────────────────

    [Test]
    public async Task PregnancyGrain_Create_PersistsAllFields()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        DateTime edd = new DateTime(2025, 7, 15);
        DateTime lmp = new DateTime(2024, 10, 8);

        await grain.CreateAsync(
            patientId: "PATIENT-OB-001",
            lastMenstrualPeriod: lmp,
            eddByLmp: edd,
            eddByUltrasound: null,
            definitiveEdd: edd,
            gravida: 2, para: 1, abortions: 0, living: 1,
            riskLevel: PregnancyRiskLevel.Low,
            riskFactors: null,
            providerId: "PROV-OB-001",
            providerName: "Dr. Smith",
            locationId: "LOC-001",
            locationName: "OB Clinic",
            notes: "Normal pregnancy");

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-OB-001"));
        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Active));
        Assert.That(state.DefinitiveEdd, Is.EqualTo(edd));
        Assert.That(state.LastMenstrualPeriod, Is.EqualTo(lmp));
        Assert.That(state.Gravida, Is.EqualTo(2));
        Assert.That(state.Para, Is.EqualTo(1));
        Assert.That(state.Abortions, Is.EqualTo(0));
        Assert.That(state.Living, Is.EqualTo(1));
        Assert.That(state.RiskLevel, Is.EqualTo(PregnancyRiskLevel.Low));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.Outcome, Is.EqualTo(PregnancyOutcome.Ongoing));
    }

    [Test]
    public async Task PregnancyGrain_Create_HighRiskWithFactors()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        List<string> riskFactors = new()
        {
            "Advanced maternal age",
            "History of cesarean section",
            "Diabetes mellitus"
        };

        await grain.CreateAsync(
            "PATIENT-OB-002", null, null, null,
            new DateTime(2025, 8, 1),
            3, 2, 0, 2,
            PregnancyRiskLevel.High, riskFactors,
            null, "Dr. Jones", null, null,
            "High risk — multiple comorbidities");

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.RiskLevel, Is.EqualTo(PregnancyRiskLevel.High));
        Assert.That(state.RiskFactors, Has.Count.EqualTo(3));
        Assert.That(state.RiskFactors, Contains.Item("Diabetes mellitus"));
    }

    // ── Pregnancy Grain — risk update ───────────────────────────────────────

    [Test]
    public async Task PregnancyGrain_UpdateRisk_ChangesLevelAndFactors()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-003", null, null, null,
            new DateTime(2025, 9, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await grain.UpdateRiskAsync(PregnancyRiskLevel.Moderate,
            new List<string> { "Gestational diabetes" });

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.RiskLevel, Is.EqualTo(PregnancyRiskLevel.Moderate));
        Assert.That(state.RiskFactors, Has.Count.EqualTo(1));
        Assert.That(state.RiskFactors[0], Is.EqualTo("Gestational diabetes"));
    }

    // ── Pregnancy Grain — problems ──────────────────────────────────────────

    [Test]
    public async Task PregnancyGrain_AddProblem_AppendsToProblemList()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-004", null, null, null,
            new DateTime(2025, 10, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        PrenatalProblemEntry problem = new()
        {
            ProblemId = "PROB-001",
            Description = "Nausea",
            Priority = PrenatalProblemPriority.Low,
            Scope = PrenatalProblemScope.CurrentPregnancy,
            IsActive = true,
            Notes = "Mild nausea in first trimester",
        };

        await grain.AddProblemAsync(problem);

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.Problems, Has.Count.EqualTo(1));
        Assert.That(state.Problems[0].Description, Is.EqualTo("Nausea"));
        Assert.That(state.Problems[0].Priority, Is.EqualTo(PrenatalProblemPriority.Low));
        Assert.That(state.Problems[0].IsActive, Is.True);
    }

    [Test]
    public async Task PregnancyGrain_AddDuplicateProblem_DoesNotAddTwice()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-005", null, null, null,
            new DateTime(2025, 10, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        PrenatalProblemEntry problem = new()
        {
            ProblemId = "PROB-DUP",
            Description = "Edema",
        };

        await grain.AddProblemAsync(problem);
        await grain.AddProblemAsync(problem);

        PregnancyState state = await grain.GetAsync();
        Assert.That(state.Problems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PregnancyGrain_ResolveProblem_SetsInactive()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-006", null, null, null,
            new DateTime(2025, 10, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        PrenatalProblemEntry problem = new()
        {
            ProblemId = "PROB-RES",
            Description = "Heartburn",
            IsActive = true,
        };

        await grain.AddProblemAsync(problem);
        await grain.ResolveProblemAsync("PROB-RES");

        PregnancyState state = await grain.GetAsync();
        Assert.That(state.Problems[0].IsActive, Is.False);
    }

    // ── Pregnancy Grain — delivery ──────────────────────────────────────────

    [Test]
    public async Task PregnancyGrain_RecordDelivery_SetsDeliveredStatusAndInfo()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-007", null, null, null,
            new DateTime(2025, 7, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        DeliveryInfo delivery = new()
        {
            DeliveryDate = new DateTime(2025, 7, 10, 14, 30, 0),
            DeliveryMethod = DeliveryMethod.SpontaneousVaginal,
            GestationalAgeAtDeliveryWeeks = 39,
            BirthWeightGrams = 3400,
            Apgar1Min = 8,
            Apgar5Min = 9,
            Presentation = FetalPresentation.Cephalic,
            InfantSex = "F",
            Notes = "Uncomplicated vaginal delivery",
        };

        await grain.RecordDeliveryAsync(delivery, PregnancyOutcome.LiveBirth);

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Delivered));
        Assert.That(state.Outcome, Is.EqualTo(PregnancyOutcome.LiveBirth));
        Assert.That(state.Delivery, Is.Not.Null);
        Assert.That(state.Delivery!.BirthWeightGrams, Is.EqualTo(3400));
        Assert.That(state.Delivery.Apgar1Min, Is.EqualTo(8));
        Assert.That(state.Delivery.Apgar5Min, Is.EqualTo(9));
        Assert.That(state.Delivery.DeliveryMethod, Is.EqualTo(DeliveryMethod.SpontaneousVaginal));
        Assert.That(state.Delivery.InfantSex, Is.EqualTo("F"));
    }

    // ── Pregnancy Grain — postpartum ────────────────────────────────────────

    [Test]
    public async Task PregnancyGrain_RecordPostpartum_SetsPostpartumStatusAndInfo()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-008", null, null, null,
            new DateTime(2025, 7, 15),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await grain.RecordDeliveryAsync(
            new DeliveryInfo { DeliveryDate = new DateTime(2025, 7, 10) },
            PregnancyOutcome.LiveBirth);

        PostpartumInfo postpartum = new()
        {
            PostpartumVisitDate = new DateTime(2025, 8, 7),
            BreastfeedingStatus = "Exclusive",
            ContraceptiveMethod = "IUD",
            DepressionScreeningResult = "Negative",
            EpdsScore = 3,
        };

        await grain.RecordPostpartumAsync(postpartum);

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Postpartum));
        Assert.That(state.Postpartum, Is.Not.Null);
        Assert.That(state.Postpartum!.BreastfeedingStatus, Is.EqualTo("Exclusive"));
        Assert.That(state.Postpartum.EpdsScore, Is.EqualTo(3));
        Assert.That(state.Postpartum.ContraceptiveMethod, Is.EqualTo("IUD"));
    }

    // ── Pregnancy Grain — status & EDD updates ──────────────────────────────

    [Test]
    public async Task PregnancyGrain_UpdateStatus_TransitionsCorrectly()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        await grain.CreateAsync(
            "PATIENT-OB-009", null, null, null,
            new DateTime(2025, 10, 1),
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        await grain.UpdateStatusAsync(PregnancyStatus.Cancelled);

        PregnancyState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PregnancyStatus.Cancelled));
    }

    [Test]
    public async Task PregnancyGrain_UpdateEdd_UpdatesUltrasoundAndDefinitive()
    {
        string id = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = _cluster.GrainFactory.GetGrain<IPregnancyGrain>(id);

        DateTime originalEdd = new DateTime(2025, 10, 1);
        await grain.CreateAsync(
            "PATIENT-OB-010", null, null, null,
            originalEdd,
            1, 0, 0, 0,
            PregnancyRiskLevel.Low, null,
            null, null, null, null, null);

        DateTime newEddUs = new DateTime(2025, 9, 25);
        await grain.UpdateEddAsync(newEddUs, newEddUs);

        PregnancyState state = await grain.GetAsync();

        Assert.That(state.EddByUltrasound, Is.EqualTo(newEddUs));
        Assert.That(state.DefinitiveEdd, Is.EqualTo(newEddUs));
    }

    // ── Pregnancy Index Grain ───────────────────────────────────────────────

    [Test]
    public async Task PregnancyIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        string indexKey = $"OB-PREG-IDX:PATIENT-{Guid.NewGuid()}";
        IPregnancyIndexGrain index = _cluster.GrainFactory.GetGrain<IPregnancyIndexGrain>(indexKey);

        string id1 = $"OB-PREG:{Guid.NewGuid()}";
        string id2 = $"OB-PREG:{Guid.NewGuid()}";

        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = id1,
            PatientId = "P-IDX-001",
            Status = PregnancyStatus.Delivered,
            DefinitiveEdd = new DateTime(2024, 5, 1),
            Gravida = 1, Para = 1,
            Outcome = PregnancyOutcome.LiveBirth,
        });

        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = id2,
            PatientId = "P-IDX-001",
            Status = PregnancyStatus.Active,
            DefinitiveEdd = new DateTime(2025, 8, 1),
            Gravida = 2, Para = 1,
            Outcome = PregnancyOutcome.Ongoing,
        });

        List<PregnancyIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].PregnancyId, Is.EqualTo(id2));
        Assert.That(all[1].PregnancyId, Is.EqualTo(id1));
    }

    [Test]
    public async Task PregnancyIndexGrain_GetActive_ReturnsOnlyActivePregnancy()
    {
        string indexKey = $"OB-PREG-IDX:PATIENT-{Guid.NewGuid()}";
        IPregnancyIndexGrain index = _cluster.GrainFactory.GetGrain<IPregnancyIndexGrain>(indexKey);

        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = $"OB-PREG:{Guid.NewGuid()}",
            Status = PregnancyStatus.Delivered,
            Outcome = PregnancyOutcome.LiveBirth,
        });

        string activeId = $"OB-PREG:{Guid.NewGuid()}";
        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = activeId,
            Status = PregnancyStatus.Active,
            Outcome = PregnancyOutcome.Ongoing,
        });

        PregnancyIndexEntry? active = await index.GetActiveAsync();

        Assert.That(active, Is.Not.Null);
        Assert.That(active!.PregnancyId, Is.EqualTo(activeId));
    }

    [Test]
    public async Task PregnancyIndexGrain_GetByStatus_FiltersCorrectly()
    {
        string indexKey = $"OB-PREG-IDX:PATIENT-{Guid.NewGuid()}";
        IPregnancyIndexGrain index = _cluster.GrainFactory.GetGrain<IPregnancyIndexGrain>(indexKey);

        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = $"OB-PREG:{Guid.NewGuid()}",
            Status = PregnancyStatus.Delivered,
        });

        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = $"OB-PREG:{Guid.NewGuid()}",
            Status = PregnancyStatus.Miscarriage,
        });

        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = $"OB-PREG:{Guid.NewGuid()}",
            Status = PregnancyStatus.Delivered,
        });

        List<PregnancyIndexEntry> delivered = await index.GetByStatusAsync(PregnancyStatus.Delivered);

        Assert.That(delivered, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PregnancyIndexGrain_UpdateEntry_ChangesStatusAndOutcome()
    {
        string indexKey = $"OB-PREG-IDX:PATIENT-{Guid.NewGuid()}";
        IPregnancyIndexGrain index = _cluster.GrainFactory.GetGrain<IPregnancyIndexGrain>(indexKey);

        string pregId = $"OB-PREG:{Guid.NewGuid()}";
        await index.AddEntryAsync(new PregnancyIndexEntry
        {
            PregnancyId = pregId,
            Status = PregnancyStatus.Active,
            Outcome = PregnancyOutcome.Ongoing,
            RiskLevel = PregnancyRiskLevel.Low,
        });

        await index.UpdateEntryAsync(pregId,
            PregnancyStatus.Delivered, PregnancyOutcome.LiveBirth, PregnancyRiskLevel.Low);

        List<PregnancyIndexEntry> all = await index.GetAllAsync();

        Assert.That(all[0].Status, Is.EqualTo(PregnancyStatus.Delivered));
        Assert.That(all[0].Outcome, Is.EqualTo(PregnancyOutcome.LiveBirth));
    }

    // ── Prenatal Visit Grain ────────────────────────────────────────────────

    [Test]
    public async Task PrenatalVisitGrain_Create_PersistsAllFields()
    {
        string id = $"OB-VISIT:{Guid.NewGuid()}";
        IPrenatalVisitGrain grain = _cluster.GrainFactory.GetGrain<IPrenatalVisitGrain>(id);

        await grain.CreateAsync(
            pregnancyId: "OB-PREG:TEST",
            patientId: "PATIENT-VISIT-001",
            visitDate: new DateTime(2025, 3, 15, 10, 0, 0),
            gestationalAgeWeeks: 28,
            gestationalAgeDays: 3,
            weight: 155.5m,
            bloodPressureSystolic: 120,
            bloodPressureDiastolic: 78,
            fundalHeightCm: 28.0m,
            fetalHeartRate: 145,
            fetalPresentation: FetalPresentation.Cephalic,
            fetalMovement: true,
            urineProtein: "Negative",
            urineGlucose: "Negative",
            edema: "None",
            cervicalDilationCm: null,
            cervicalEffacementPercent: null,
            fetalStation: null,
            providerId: "PROV-001",
            providerName: "Dr. Garcia",
            notes: "Routine 28-week visit",
            nextVisitDate: new DateTime(2025, 4, 12));

        PrenatalVisitState state = await grain.GetAsync();

        Assert.That(state.PregnancyId, Is.EqualTo("OB-PREG:TEST"));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-VISIT-001"));
        Assert.That(state.GestationalAgeWeeks, Is.EqualTo(28));
        Assert.That(state.GestationalAgeDays, Is.EqualTo(3));
        Assert.That(state.Weight, Is.EqualTo(155.5m));
        Assert.That(state.BloodPressureSystolic, Is.EqualTo(120));
        Assert.That(state.BloodPressureDiastolic, Is.EqualTo(78));
        Assert.That(state.FundalHeightCm, Is.EqualTo(28.0m));
        Assert.That(state.FetalHeartRate, Is.EqualTo(145));
        Assert.That(state.FetalPresentation, Is.EqualTo(FetalPresentation.Cephalic));
        Assert.That(state.FetalMovement, Is.True);
        Assert.That(state.UrineProtein, Is.EqualTo("Negative"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Garcia"));
        Assert.That(state.Notes, Does.Contain("28-week"));
        Assert.That(state.NextVisitDate, Is.EqualTo(new DateTime(2025, 4, 12)));
    }

    [Test]
    public async Task PrenatalVisitGrain_CervicalExam_PersistsAllFields()
    {
        string id = $"OB-VISIT:{Guid.NewGuid()}";
        IPrenatalVisitGrain grain = _cluster.GrainFactory.GetGrain<IPrenatalVisitGrain>(id);

        await grain.CreateAsync(
            "OB-PREG:TEST2", "PATIENT-VISIT-002",
            new DateTime(2025, 6, 20), 38, 0,
            weight: 165m,
            bloodPressureSystolic: 130, bloodPressureDiastolic: 85,
            fundalHeightCm: 37m, fetalHeartRate: 140,
            FetalPresentation.Cephalic, true,
            "Trace", "Negative", "1+",
            cervicalDilationCm: 2.0m,
            cervicalEffacementPercent: 50,
            fetalStation: -2,
            null, "Dr. Lee", "Late-term check", null);

        PrenatalVisitState state = await grain.GetAsync();

        Assert.That(state.CervicalDilationCm, Is.EqualTo(2.0m));
        Assert.That(state.CervicalEffacementPercent, Is.EqualTo(50));
        Assert.That(state.FetalStation, Is.EqualTo(-2));
        Assert.That(state.Edema, Is.EqualTo("1+"));
        Assert.That(state.UrineProtein, Is.EqualTo("Trace"));
    }

    [Test]
    public async Task PrenatalVisitGrain_UpdateNotes_ChangesNotes()
    {
        string id = $"OB-VISIT:{Guid.NewGuid()}";
        IPrenatalVisitGrain grain = _cluster.GrainFactory.GetGrain<IPrenatalVisitGrain>(id);

        await grain.CreateAsync(
            "OB-PREG:TEST3", "PATIENT-VISIT-003",
            DateTime.UtcNow, 20, 0,
            null, null, null, null, null,
            FetalPresentation.Unknown, null,
            null, null, null, null, null, null,
            null, null, "Initial notes", null);

        await grain.UpdateNotesAsync("Updated with new findings");

        PrenatalVisitState state = await grain.GetAsync();
        Assert.That(state.Notes, Is.EqualTo("Updated with new findings"));
    }

    // ── Prenatal Visit Index Grain ──────────────────────────────────────────

    [Test]
    public async Task PrenatalVisitIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        string indexKey = $"OB-VISIT-IDX:PREG-{Guid.NewGuid()}";
        IPrenatalVisitIndexGrain index =
            _cluster.GrainFactory.GetGrain<IPrenatalVisitIndexGrain>(indexKey);

        string v1 = $"OB-VISIT:{Guid.NewGuid()}";
        string v2 = $"OB-VISIT:{Guid.NewGuid()}";

        await index.AddEntryAsync(new PrenatalVisitIndexEntry
        {
            VisitId = v1,
            PregnancyId = "PREG-001",
            VisitDate = new DateTime(2025, 1, 15),
            GestationalAgeWeeks = 12,
            FetalHeartRate = 150,
        });

        await index.AddEntryAsync(new PrenatalVisitIndexEntry
        {
            VisitId = v2,
            PregnancyId = "PREG-001",
            VisitDate = new DateTime(2025, 3, 15),
            GestationalAgeWeeks = 20,
            FetalHeartRate = 145,
            FundalHeightCm = 20m,
        });

        List<PrenatalVisitIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].VisitId, Is.EqualTo(v2));
        Assert.That(all[1].VisitId, Is.EqualTo(v1));
    }

    [Test]
    public async Task PrenatalVisitIndexGrain_GetVisitCount_ReturnsCorrectCount()
    {
        string indexKey = $"OB-VISIT-IDX:PREG-{Guid.NewGuid()}";
        IPrenatalVisitIndexGrain index =
            _cluster.GrainFactory.GetGrain<IPrenatalVisitIndexGrain>(indexKey);

        Assert.That(await index.GetVisitCountAsync(), Is.EqualTo(0));

        for (int i = 0; i < 5; i++)
        {
            await index.AddEntryAsync(new PrenatalVisitIndexEntry
            {
                VisitId = $"OB-VISIT:{Guid.NewGuid()}",
                PregnancyId = "PREG-COUNT",
                VisitDate = DateTime.UtcNow.AddDays(-i * 14),
                GestationalAgeWeeks = 12 + (i * 2),
            });
        }

        Assert.That(await index.GetVisitCountAsync(), Is.EqualTo(5));
    }
}
