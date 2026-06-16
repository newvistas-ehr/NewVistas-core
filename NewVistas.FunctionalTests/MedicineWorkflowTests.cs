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
/// Functional tests for Medicine (Procedures) — VistA Files #691-699.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class MedicineWorkflowTests
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

    private Task<string> OrderEcg(IPatientWorkflowGrain wf)
        => wf.OrderMedProcedureAsync(
            category: MedProcedureCategory.Electrocardiogram,
            procedureCode: "93000",
            procedureDescription: "12-lead ECG with interpretation",
            orderedDate: DateTime.UtcNow,
            providerId: "PROV-001",
            providerName: "Dr. Vasquez",
            locationId: "LOC-001",
            locationName: "Cardiology Clinic",
            indication: "Chest pain, rule out ACS");

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task OrderMedProcedure_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await OrderEcg(wf);

        Assert.That(procedureId, Is.Not.Null.And.Not.Empty);

        List<MedProcedureIndexEntry> all = await wf.GetMedProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ProcedureId, Is.EqualTo(procedureId));
        Assert.That(all[0].Category, Is.EqualTo(MedProcedureCategory.Electrocardiogram));
        Assert.That(all[0].ProcedureCode, Is.EqualTo("93000"));
        Assert.That(all[0].Status, Is.EqualTo(MedProcedureStatus.Ordered));
    }

    [Test]
    public async Task GetMedProcedure_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await OrderEcg(wf);

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Category, Is.EqualTo(MedProcedureCategory.Electrocardiogram));
        Assert.That(state.ProcedureDescription, Is.EqualTo("12-lead ECG with interpretation"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Vasquez"));
        Assert.That(state.LocationName, Is.EqualTo("Cardiology Clinic"));
        Assert.That(state.Indication, Is.EqualTo("Chest pain, rule out ACS"));
    }

    [Test]
    public async Task ScheduleMedProcedure_SetsScheduledDate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await OrderEcg(wf);
        DateTime scheduledDate = DateTime.UtcNow.AddDays(3);

        await wf.ScheduleMedProcedureAsync(procedureId, scheduledDate);

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Scheduled));
        Assert.That(state.ScheduledDate, Is.EqualTo(scheduledDate));
    }

    [Test]
    public async Task CompleteMedProcedure_SetsCompletedWithFindings()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await OrderEcg(wf);
        DateTime performedDate = DateTime.UtcNow;

        await wf.CompleteMedProcedureAsync(
            procedureId, performedDate,
            "Normal sinus rhythm at 72 bpm. No ST-T wave changes.",
            "Normal ECG.",
            "Patient tolerated procedure well.");

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Completed));
        Assert.That(state.PerformedDate, Is.EqualTo(performedDate));
        Assert.That(state.Findings, Does.Contain("Normal sinus rhythm"));
        Assert.That(state.Impression, Is.EqualTo("Normal ECG."));

        List<MedProcedureIndexEntry> index = await wf.GetMedProceduresAsync();
        Assert.That(index[0].Status, Is.EqualTo(MedProcedureStatus.Completed));
    }

    [Test]
    public async Task CancelMedProcedure_SetsCancelledWithReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await OrderEcg(wf);

        await wf.CancelMedProcedureAsync(procedureId, "Patient refused testing");

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient refused testing"));
    }

    [Test]
    public async Task RecordMedEcgResults_SetsEcgMeasurements()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await OrderEcg(wf);
        await wf.CompleteMedProcedureAsync(procedureId, DateTime.UtcNow, null, null, null);

        await wf.RecordMedEcgResultsAsync(
            procedureId,
            rate: 72,
            rhythm: CardiacRhythm.Normal,
            prIntervalMs: 160,
            qrsDurationMs: 88,
            qtcMs: 420,
            axisDegrees: 60,
            interpretation: "Normal sinus rhythm. Normal axis. No ischemic changes.",
            isNormal: true);

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.EcgRate, Is.EqualTo(72));
        Assert.That(state.EcgRhythm, Is.EqualTo(CardiacRhythm.Normal));
        Assert.That(state.EcgPrIntervalMs, Is.EqualTo(160));
        Assert.That(state.EcgQrsDurationMs, Is.EqualTo(88));
        Assert.That(state.EcgQtcMs, Is.EqualTo(420));
        Assert.That(state.EcgAxisDegrees, Is.EqualTo(60));
        Assert.That(state.EcgIsNormal, Is.True);
    }

    [Test]
    public async Task RecordMedEchoResults_SetsEchocardiogramValues()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await wf.OrderMedProcedureAsync(
            MedProcedureCategory.Cardiology, "93306",
            "Transthoracic echocardiogram", DateTime.UtcNow,
            "PROV-002", "Dr. Kim", null, null, "CHF evaluation");
        await wf.CompleteMedProcedureAsync(procedureId, DateTime.UtcNow, null, null, null);

        await wf.RecordMedEchoResultsAsync(
            procedureId,
            lvEjectionFraction: 35m,
            lvDiastolicFunction: "Grade II (pseudonormal)",
            valvularFindings: "Moderate mitral regurgitation. Trace aortic insufficiency.");

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.LvEjectionFraction, Is.EqualTo(35m));
        Assert.That(state.LvDiastolicFunction, Is.EqualTo("Grade II (pseudonormal)"));
        Assert.That(state.ValvularFindings, Does.Contain("mitral regurgitation"));
    }

    [Test]
    public async Task RecordMedStressTestResults_SetsStressValues()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await wf.OrderMedProcedureAsync(
            MedProcedureCategory.Cardiology, "93015",
            "Exercise treadmill stress test", DateTime.UtcNow,
            null, null, null, null, "Exertional dyspnea");
        await wf.CompleteMedProcedureAsync(procedureId, DateTime.UtcNow, null, null, null);

        await wf.RecordMedStressTestResultsAsync(
            procedureId,
            peakMets: 10.2m,
            targetHeartRatePct: 92m,
            inducibleIschemia: false);

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.PeakMets, Is.EqualTo(10.2m));
        Assert.That(state.TargetHeartRatePct, Is.EqualTo(92m));
        Assert.That(state.InducibleIschemia, Is.False);
    }

    [Test]
    public async Task RecordMedPftResults_SetsSpiromeryAndLungVolumes()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await wf.OrderMedProcedureAsync(
            MedProcedureCategory.PulmonaryFunction, "94010",
            "Complete pulmonary function test", DateTime.UtcNow,
            "PROV-003", "Dr. Patel", null, null, "COPD staging");
        await wf.CompleteMedProcedureAsync(procedureId, DateTime.UtcNow, null, null, null);

        await wf.RecordMedPftResultsAsync(
            procedureId,
            fev1: 1.8m,
            fev1PctPredicted: 55m,
            fvc: 3.2m,
            fvcPctPredicted: 78m,
            fev1FvcRatio: 0.56m,
            dlco: 15m,
            dlcoPctPredicted: 60m,
            tlc: 6.0m,
            rv: 2.8m,
            obstructive: true,
            restrictive: false,
            bronchodilatorResponse: true);

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.PftFev1, Is.EqualTo(1.8m));
        Assert.That(state.PftFev1PctPredicted, Is.EqualTo(55m));
        Assert.That(state.PftFvc, Is.EqualTo(3.2m));
        Assert.That(state.PftFev1FvcRatio, Is.EqualTo(0.56m));
        Assert.That(state.PftDlco, Is.EqualTo(15m));
        Assert.That(state.PftObstructive, Is.True);
        Assert.That(state.PftRestrictive, Is.False);
        Assert.That(state.PftBronchodilatorResponse, Is.True);
    }

    [Test]
    public async Task RecordMedAbgResults_SetsBloodGasValues()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await wf.OrderMedProcedureAsync(
            MedProcedureCategory.PulmonaryFunction, "82803",
            "Arterial blood gas", DateTime.UtcNow,
            null, null, null, null, "Hypoxemia evaluation");
        await wf.CompleteMedProcedureAsync(procedureId, DateTime.UtcNow, null, null, null);

        await wf.RecordMedAbgResultsAsync(
            procedureId,
            ph: 7.38m,
            pao2: 62m,
            paco2: 48m,
            hco3: 28m,
            sao2: 90m);

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.AbgPh, Is.EqualTo(7.38m));
        Assert.That(state.AbgPao2, Is.EqualTo(62m));
        Assert.That(state.AbgPaco2, Is.EqualTo(48m));
        Assert.That(state.AbgHco3, Is.EqualTo(28m));
        Assert.That(state.AbgSao2, Is.EqualTo(90m));
    }

    [Test]
    public async Task RecordMedEndoscopyResults_SetsColonoscopyFindings()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string procedureId = await wf.OrderMedProcedureAsync(
            MedProcedureCategory.GIEndoscopy, "45378",
            "Screening colonoscopy", DateTime.UtcNow,
            "PROV-004", "Dr. Nguyen", null, null, "Average risk CRC screening");
        await wf.CompleteMedProcedureAsync(procedureId, DateTime.UtcNow,
            "Two sessile polyps in ascending colon.", "Polypectomy completed.", null);

        await wf.RecordMedEndoscopyResultsAsync(
            procedureId,
            endoscopyType: EndoscopyType.Colonoscopy,
            bowelPrepQuality: BowelPrepQuality.Good,
            cecumReached: true,
            scopeAdvancedCm: 120,
            biopsyTaken: true,
            biopsySites: new List<string> { "Ascending colon 60cm", "Ascending colon 65cm" },
            polypCount: 2,
            polypDescriptions: new List<string> { "5mm sessile polyp at 60cm", "8mm sessile polyp at 65cm" },
            endoscopicInterventions: new List<string> { "Snare polypectomy x2" });

        MedProcedureState state = await wf.GetMedProcedureAsync(procedureId);
        Assert.That(state.EndoscopyType, Is.EqualTo(EndoscopyType.Colonoscopy));
        Assert.That(state.BowelPrepQuality, Is.EqualTo(BowelPrepQuality.Good));
        Assert.That(state.CecumReached, Is.True);
        Assert.That(state.ScopeAdvancedCm, Is.EqualTo(120));
        Assert.That(state.BiopsyTaken, Is.True);
        Assert.That(state.BiopsySites, Has.Count.EqualTo(2));
        Assert.That(state.PolypCount, Is.EqualTo(2));
        Assert.That(state.EndoscopicInterventions, Contains.Item("Snare polypectomy x2"));
    }

    [Test]
    public async Task GetMedProceduresByCategory_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await OrderEcg(wf);
        await wf.OrderMedProcedureAsync(
            MedProcedureCategory.PulmonaryFunction, "94010",
            "PFT", DateTime.UtcNow, null, null, null, null, null);
        await wf.OrderMedProcedureAsync(
            MedProcedureCategory.GIEndoscopy, "45378",
            "Colonoscopy", DateTime.UtcNow, null, null, null, null, null);

        List<MedProcedureIndexEntry> ecgs = await wf.GetMedProceduresByCategoryAsync(MedProcedureCategory.Electrocardiogram);
        List<MedProcedureIndexEntry> pfts = await wf.GetMedProceduresByCategoryAsync(MedProcedureCategory.PulmonaryFunction);
        List<MedProcedureIndexEntry> gis = await wf.GetMedProceduresByCategoryAsync(MedProcedureCategory.GIEndoscopy);

        Assert.That(ecgs, Has.Count.EqualTo(1));
        Assert.That(pfts, Has.Count.EqualTo(1));
        Assert.That(gis, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetCompletedMedProcedures_FiltersCompletedOnly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string proc1 = await OrderEcg(wf);
        string proc2 = await wf.OrderMedProcedureAsync(
            MedProcedureCategory.Cardiology, "93306",
            "Echo", DateTime.UtcNow, null, null, null, null, null);

        await wf.CompleteMedProcedureAsync(proc1, DateTime.UtcNow, "Normal", "WNL", null);

        List<MedProcedureIndexEntry> completed = await wf.GetCompletedMedProceduresAsync();
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(completed[0].ProcedureId, Is.EqualTo(proc1));
    }

    [Test]
    public async Task MultiplePatients_IndependentProcedures()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await OrderEcg(wf1);
        await OrderEcg(wf2);
        await wf2.OrderMedProcedureAsync(
            MedProcedureCategory.PulmonaryFunction, "94010",
            "PFT", DateTime.UtcNow, null, null, null, null, null);

        List<MedProcedureIndexEntry> p1Procs = await wf1.GetMedProceduresAsync();
        List<MedProcedureIndexEntry> p2Procs = await wf2.GetMedProceduresAsync();

        Assert.That(p1Procs, Has.Count.EqualTo(1));
        Assert.That(p2Procs, Has.Count.EqualTo(2));
    }
}
