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
/// Functional tests for the Neonatal NICU depth (Phase 2, NEONATAL_CARE) — respiratory support,
/// phototherapy, the neonatal problem list, nutrition, and bedside procedures. Tests run end-to-end
/// via <see cref="IPatientWorkflowGrain"/> (the mother) and the singleton
/// <see cref="INewbornNurseryGrain"/>. The nursery census is a shared singleton across the whole test
/// run, so assertions about it locate this test's own newborn id — never exact totals.
/// </summary>
[TestFixture]
public class NeonatalNicuWorkflowTests
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

    /// <summary>Creates a mother pregnancy + delivery and registers a newborn, returning its id.</summary>
    private async Task<string> RegisterNewborn(IPatientWorkflowGrain wf, DateTime birth)
    {
        string pregnancyId = await CreatePregnancyWithDelivery(wf, birth);
        string newbornId = await RegisterBabyGirl(wf, pregnancyId, birth);
        Assert.That(newbornId, Is.Not.Null.And.Not.Empty);
        return newbornId;
    }

    private async Task<NewbornNurseryEntry> CensusEntry(string newbornId)
    {
        List<NewbornNurseryEntry> active = await Nursery().GetActiveAsync();
        NewbornNurseryEntry? entry = active.SingleOrDefault(e => e.NewbornId == newbornId);
        Assert.That(entry, Is.Not.Null, "newborn should appear on the active nursery census");
        return entry!;
    }

    // ── Respiratory support ────────────────────────────────────────────────────────

    [Test]
    public async Task RecordRespiratorySupport_TwoEntries_ClosesFirst_LatestOpenIsCpap()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 1, 9, 0, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        // Ventilator first, then de-escalate to CPAP an hour later.
        await wf.RecordNewbornRespiratorySupportAsync(
            newbornId, RespiratorySupportType.ConventionalVentilation, 40,
            "SIMV RR 30, PIP 18, PEEP 5", birth.AddHours(1), "Intubated for RDS.");
        await wf.RecordNewbornRespiratorySupportAsync(
            newbornId, RespiratorySupportType.Cpap, 30,
            "CPAP +6", birth.AddHours(2), "Extubated to CPAP.");

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.RespiratorySupport, Has.Count.EqualTo(2));

        RespiratorySupportEntry first = newborn.RespiratorySupport[0];
        Assert.That(first.SupportType, Is.EqualTo(RespiratorySupportType.ConventionalVentilation));
        Assert.That(first.EndedAt, Is.EqualTo(birth.AddHours(2)),
            "the prior open episode is closed at the time the next is recorded");

        RespiratorySupportEntry open = newborn.RespiratorySupport.Single(e => e.EndedAt == null);
        Assert.That(open.SupportType, Is.EqualTo(RespiratorySupportType.Cpap));
    }

    [Test]
    public async Task RecordRespiratorySupport_NonRoomAir_SetsCensusOnRespiratorySupport()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 2, 3, 0, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        await wf.RecordNewbornRespiratorySupportAsync(
            newbornId, RespiratorySupportType.HighFlowNasalCannula, 35,
            "HFNC 5 L/min", birth.AddHours(1), "");

        NewbornNurseryEntry entry = await CensusEntry(newbornId);
        Assert.That(entry.OnRespiratorySupport, Is.True);
    }

    [Test]
    public async Task RecordRespiratorySupport_RoomAir_ClearsCensusOnRespiratorySupport()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 3, 5, 30, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        // On support first…
        await wf.RecordNewbornRespiratorySupportAsync(
            newbornId, RespiratorySupportType.NasalCannula, 25,
            "NC 0.5 L/min", birth.AddHours(1), "");
        Assert.That((await CensusEntry(newbornId)).OnRespiratorySupport, Is.True);

        // …then weaned to room air → census flag clears.
        await wf.RecordNewbornRespiratorySupportAsync(
            newbornId, RespiratorySupportType.RoomAir, 21,
            "room air", birth.AddHours(6), "Weaned off support.");

        NewbornNurseryEntry entry = await CensusEntry(newbornId);
        Assert.That(entry.OnRespiratorySupport, Is.False);
    }

    // ── Phototherapy ───────────────────────────────────────────────────────────────

    [Test]
    public async Task PhototherapyStartThenEnd_SetsEndedAtOnEpisode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 4, 7, 0, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        DateTime start = birth.AddHours(30);
        DateTime end = birth.AddHours(54);

        await wf.StartNewbornPhototherapyAsync(
            newbornId, PhototherapyIntensity.Double, "Hyperbilirubinemia",
            15.2m, start, "Above treatment threshold for age.");
        await wf.EndNewbornPhototherapyAsync(newbornId, end, "TSB down-trending, off lights.");

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.Phototherapy, Has.Count.EqualTo(1));

        PhototherapyEntry episode = newborn.Phototherapy[0];
        Assert.That(episode.Intensity, Is.EqualTo(PhototherapyIntensity.Double));
        Assert.That(episode.StartedAt, Is.EqualTo(start));
        Assert.That(episode.EndedAt, Is.EqualTo(end));
        Assert.That(episode.BilirubinAtStartMgDl, Is.EqualTo(15.2m));
    }

    // ── Problem list + census acuity ───────────────────────────────────────────────

    [Test]
    public async Task AddProblem_AppearsActive_AndIncrementsCensusActiveProblemCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 5, 8, 15, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        int before = (await CensusEntry(newbornId)).ActiveProblemCount;

        string problemId = await wf.AddNewbornProblemAsync(
            newbornId, "Respiratory distress syndrome", "P22.0", birth, "Surfactant given.");
        Assert.That(problemId, Is.Not.Null.And.Not.Empty);

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        NeonatalProblemEntry problem = newborn.Problems.Single(p => p.ProblemId == problemId);
        Assert.That(problem.Problem, Is.EqualTo("Respiratory distress syndrome"));
        Assert.That(problem.Icd10Code, Is.EqualTo("P22.0"));
        Assert.That(problem.Status, Is.EqualTo(NeonatalProblemStatus.Active));

        Assert.That((await CensusEntry(newbornId)).ActiveProblemCount, Is.EqualTo(before + 1));
    }

    [Test]
    public async Task ResolveProblem_FlipsStatusResolved_AndDecrementsCensusActiveProblemCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 6, 10, 0, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        string problemId = await wf.AddNewbornProblemAsync(
            newbornId, "Neonatal jaundice", "P59.9", birth, "");
        int afterAdd = (await CensusEntry(newbornId)).ActiveProblemCount;
        Assert.That(afterAdd, Is.GreaterThanOrEqualTo(1));

        await wf.ResolveNewbornProblemAsync(newbornId, problemId);

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        NeonatalProblemEntry problem = newborn.Problems.Single(p => p.ProblemId == problemId);
        Assert.That(problem.Status, Is.EqualTo(NeonatalProblemStatus.Resolved));

        Assert.That((await CensusEntry(newbornId)).ActiveProblemCount, Is.EqualTo(afterAdd - 1));
    }

    // ── Nutrition ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task RecordNutrition_EntryAppearsWithRouteAndDetail()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 7, 6, 0, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        DateTime when = birth.AddHours(12);
        await wf.RecordNewbornNutritionAsync(
            newbornId, when, NeonatalNutritionRoute.Tpn, 80,
            "TPN: dextrose 12.5%, AA 3.5 g/kg, lipids 3 g/kg", "Day 1 parenteral nutrition.");

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        Assert.That(newborn.Nutrition, Has.Count.EqualTo(1));

        NeonatalNutritionEntry entry = newborn.Nutrition[0];
        Assert.That(entry.RecordedAt, Is.EqualTo(when));
        Assert.That(entry.Route, Is.EqualTo(NeonatalNutritionRoute.Tpn));
        Assert.That(entry.TotalFluidMlPerKgPerDay, Is.EqualTo(80));
        Assert.That(entry.Detail, Does.Contain("dextrose 12.5%"));
    }

    // ── Procedures ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task RecordProcedure_ReturnsId_AndAppearsWithType()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime birth = new DateTime(2026, 4, 8, 4, 45, 0);
        string newbornId = await RegisterNewborn(wf, birth);

        string procedureId = await wf.RecordNewbornProcedureAsync(
            newbornId, NeonatalProcedureType.UmbilicalVenousCatheter, birth.AddHours(1),
            "Dr. Peds", "UVC placed, tip confirmed by film.");
        Assert.That(procedureId, Is.Not.Null.And.Not.Empty);

        NewbornState newborn = await wf.GetNewbornAsync(newbornId);
        NeonatalProcedureEntry procedure = newborn.Procedures.Single(p => p.ProcedureId == procedureId);
        Assert.That(procedure.ProcedureType, Is.EqualTo(NeonatalProcedureType.UmbilicalVenousCatheter));
        Assert.That(procedure.PerformedBy, Is.EqualTo("Dr. Peds"));
        Assert.That(procedure.PerformedAt, Is.EqualTo(birth.AddHours(1)));
    }
}
