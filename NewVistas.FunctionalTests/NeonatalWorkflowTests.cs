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
/// Functional tests for the Neonatal / Newborn Nursery module (NEONATAL_CARE) — the OB
/// module's extension that registers a newborn from a delivery and tracks the birth stay.
/// Tests run end-to-end via <see cref="IPatientWorkflowGrain"/> (the mother) and the singleton
/// <see cref="INewbornNurseryGrain"/>. The nursery census is a shared singleton across the whole
/// test run, so assertions about it use membership/contains against this test's unique newborn ids
/// — never exact totals.
/// </summary>
[TestFixture]
public class NeonatalWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private INewbornNurseryGrain Nursery()
        => _cluster.GrainFactory.GetGrain<INewbornNurseryGrain>("NEONATE-NURSERY:DEFAULT");

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Creates an active pregnancy on the mother and records a term live-birth delivery.</summary>
    private async Task<string> CreatePregnancyWithDelivery(IPatientWorkflowGrain wf, DateTime birthDateTime)
    {
        string pregnancyId = await wf.CreatePregnancyAsync(
            lastMenstrualPeriod: birthDateTime.AddDays(-280),
            eddByLmp: birthDateTime,
            eddByUltrasound: birthDateTime,
            definitiveEdd: birthDateTime,
            gravida: 1, para: 0, abortions: 0, living: 0,
            riskLevel: PregnancyRiskLevel.Low,
            riskFactors: null,
            providerId: "PROV-1", providerName: "Dr. Peds",
            locationId: "LOC-1", locationName: "Main Hospital",
            notes: "Uncomplicated pregnancy.");

        Assert.That(pregnancyId, Is.Not.Null.And.Not.Empty);

        await wf.RecordDeliveryAsync(
            pregnancyId,
            new DeliveryInfo
            {
                DeliveryDate = birthDateTime,
                DeliveryMethod = DeliveryMethod.SpontaneousVaginal,
                GestationalAgeAtDeliveryWeeks = 39,
                BirthWeightGrams = 3300,
                Apgar1Min = 8,
                Apgar5Min = 9,
                Presentation = FetalPresentation.Cephalic,
                InfantSex = "F",
            },
            PregnancyOutcome.LiveBirth);

        return pregnancyId;
    }

    /// <summary>Registers a standard term baby girl from the given pregnancy.</summary>
    private Task<string> RegisterBabyGirl(IPatientWorkflowGrain wf, string pregnancyId, DateTime birthDateTime)
        => wf.RegisterNewbornFromDeliveryAsync(
            pregnancyId, "BABY GIRL TEST", NewbornSex.Female, birthDateTime,
            39, 2, DeliveryMethod.SpontaneousVaginal, 3300, 50m, 35m, 8, 9, null,
            1, 1, "PROV-1", "Dr. Peds", "Main Hospital");

    // ── Registration / classification ────────────────────────────────────────────

    [Test]
    public async Task RegisterNewbornFromDelivery_ReturnsId_ReflectsDataAndClassification()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 1, 9, 15, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        Assert.That(newbornId, Is.Not.Null.And.Not.Empty);

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.NewbornId, Is.EqualTo(newbornId));
        Assert.That(newborn.MotherPatientId, Is.EqualTo(patientId));
        Assert.That(newborn.PregnancyId, Is.EqualTo(pregnancyId));
        Assert.That(newborn.Name, Is.EqualTo("BABY GIRL TEST"));
        Assert.That(newborn.Sex, Is.EqualTo(NewbornSex.Female));
        Assert.That(newborn.BirthDateTime, Is.EqualTo(birth));
        Assert.That(newborn.GestationalAgeWeeks, Is.EqualTo(39));
        Assert.That(newborn.GestationalAgeDays, Is.EqualTo(2));
        Assert.That(newborn.DeliveryMethod, Is.EqualTo(DeliveryMethod.SpontaneousVaginal));
        Assert.That(newborn.BirthWeightGrams, Is.EqualTo(3300));
        Assert.That(newborn.Apgar1Min, Is.EqualTo(8));
        Assert.That(newborn.Apgar5Min, Is.EqualTo(9));
        Assert.That(newborn.AttendingProviderName, Is.EqualTo("Dr. Peds"));
        Assert.That(newborn.BirthLocationName, Is.EqualTo("Main Hospital"));
        Assert.That(newborn.Status, Is.EqualTo(NewbornStatus.Admitted));

        // Computed classification (Clinical.NeonatalClassifier).
        Assert.That(newborn.GestationalAgeClassification, Is.EqualTo(GestationalAgeClassification.Term));
        Assert.That(newborn.BirthWeightCategory, Is.EqualTo(BirthWeightCategory.Normal));
        Assert.That(newborn.SizeForGestationalAge, Is.EqualTo(SizeForGestationalAge.AppropriateForGestationalAge));
    }

    [Test]
    public async Task RegisterNewborn_LinksToPregnancyAndMother()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 2, 4, 30, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        List<NewbornState> forPregnancy = await wf.GetNewbornsForPregnancyAsync(pregnancyId);
        Assert.That(forPregnancy.Select(n => n.NewbornId), Contains.Item(newbornId));

        List<NewbornState> forMother = await wf.GetNewbornsForMotherAsync();
        Assert.That(forMother.Select(n => n.NewbornId), Contains.Item(newbornId));
    }

    // ── Nursery census singleton (membership only — shared across the run) ────────

    [Test]
    public async Task Nursery_AfterRegister_ContainsNewborn_WithThreePendingScreens()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 3, 12, 0, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        List<NewbornNurseryEntry> active = await Nursery().GetActiveAsync();
        NewbornNurseryEntry? entry = active.SingleOrDefault(e => e.NewbornId == newbornId);
        Assert.That(entry, Is.Not.Null, "newborn should appear on the active nursery census");
        Assert.That(entry!.PendingScreenCount, Is.EqualTo(3),
            "all three universal screens are pending before any are recorded");
    }

    [Test]
    public async Task RecordNewbornScreening_DrivesPendingScreenCountToZero()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 4, 6, 45, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        DateTime screenDate = birth.AddHours(36);

        // First universal screen: metabolic blood spot.
        await wf.RecordNewbornScreeningAsync(
            newbornId, NewbornScreeningType.MetabolicBloodSpot, NewbornScreeningResult.Pass,
            "normal", screenDate, "Lab", "");
        Assert.That(await PendingScreens(newbornId), Is.EqualTo(2));

        // Remaining two universal screens: CCHD + Hearing.
        await wf.RecordNewbornScreeningAsync(
            newbornId, NewbornScreeningType.CriticalCongenitalHeartDisease, NewbornScreeningResult.Pass,
            "pre-ductal 99% / post-ductal 99%", screenDate, "Lab", "");
        Assert.That(await PendingScreens(newbornId), Is.EqualTo(1));

        await wf.RecordNewbornScreeningAsync(
            newbornId, NewbornScreeningType.Hearing, NewbornScreeningResult.Pass,
            "OAE pass bilaterally", screenDate, "Lab", "");
        Assert.That(await PendingScreens(newbornId), Is.EqualTo(0));

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.Screenings, Has.Count.EqualTo(3));
    }

    private async Task<int> PendingScreens(string newbornId)
    {
        List<NewbornNurseryEntry> active = await Nursery().GetActiveAsync();
        return active.Single(e => e.NewbornId == newbornId).PendingScreenCount;
    }

    // ── Exam + measurements ──────────────────────────────────────────────────────

    [Test]
    public async Task RecordExamAndMeasurement_ShowOnNewborn()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 5, 8, 0, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        await wf.RecordNewbornExamAsync(newbornId, new NewbornExam
        {
            General = "Well-appearing, vigorous.",
            Cardiac = "RRR, no murmur.",
            Impression = "Healthy term newborn.",
            ExaminerName = "Dr. Peds",
            ExamDate = birth.AddHours(2),
        });

        await wf.AddNewbornMeasurementAsync(
            newbornId, birth.AddHours(24), 3250, NewbornFeedingType.Breast,
            null, "Latching well.", "Day 1 weight.");

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.Exam.General, Does.Contain("vigorous"));
        Assert.That(newborn.Exam.Impression, Does.Contain("Healthy term"));
        Assert.That(newborn.Measurements, Has.Count.EqualTo(1));
        Assert.That(newborn.Measurements[0].WeightGrams, Is.EqualTo(3250));
        Assert.That(newborn.Measurements[0].FeedingType, Is.EqualTo(NewbornFeedingType.Breast));
        Assert.That(newborn.Measurements[0].FeedingNotes, Does.Contain("Latching"));
    }

    // ── Nursery level of care ────────────────────────────────────────────────────

    [Test]
    public async Task SetNurseryLevel_ReflectsOnNewbornAndCensus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 6, 10, 30, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        await wf.SetNewbornNurseryLevelAsync(newbornId, NurseryLevelOfCare.SpecialCareLevelII, "obs");

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.NurseryLevel, Is.EqualTo(NurseryLevelOfCare.SpecialCareLevelII));
        Assert.That(newborn.NurseryLevelReason, Is.EqualTo("obs"));

        List<NewbornNurseryEntry> active = await Nursery().GetActiveAsync();
        NewbornNurseryEntry entry = active.Single(e => e.NewbornId == newbornId);
        Assert.That(entry.NurseryLevel, Is.EqualTo(NurseryLevelOfCare.SpecialCareLevelII));
    }

    // ── Discharge ────────────────────────────────────────────────────────────────

    [Test]
    public async Task DischargeNewborn_SetsDischarged_AndRemovesFromActiveCensus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 3, 7, 7, 0, 0);

        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);

        await wf.DischargeNewbornAsync(
            newbornId, birth.AddDays(2), 3200, NewbornFeedingType.Breast,
            "Home with parents", "PCP visit in 48 hours", true);

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.Status, Is.EqualTo(NewbornStatus.Discharged));

        List<NewbornNurseryEntry> active = await Nursery().GetActiveAsync();
        Assert.That(active.Select(e => e.NewbornId), Does.Not.Contain(newbornId));

        List<NewbornNurseryEntry> all = await Nursery().GetAllAsync();
        Assert.That(all.Select(e => e.NewbornId), Contains.Item(newbornId));
    }
}
