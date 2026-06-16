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
/// Functional tests for Nursing Intake/Triage Assessment with ESI scoring.
/// </summary>
[TestFixture]
public class NursingTriageWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private INursingTriageGrain NewTriage()
        => _cluster.GrainFactory.GetGrain<INursingTriageGrain>($"NURS-TRIAGE:{Guid.NewGuid()}");

    private IPatientWorkflowGrain WF(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Grain Level ─────────────────────────────────────────────────────────

    [Test]
    public async Task CreateTriage_SetsDraftStatus()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "P1", ChiefComplaint = "Chest pain", NurseId = "RN-1", NurseName = "Nurse", TriageLevel = TriageLevel.Esi2_Emergent });
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(NursingAssessmentStatus.Draft));
        Assert.That(state.TriageLevel, Is.EqualTo(TriageLevel.Esi2_Emergent));
    }

    [Test]
    public async Task AssignTriageLevel_UpdatesLevel()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "P2", ChiefComplaint = "Ankle sprain", NurseId = "RN-1", NurseName = "Nurse" });
        await grain.AssignTriageLevelAsync(TriageLevel.Esi4_LessUrgent, 1);
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.TriageLevel, Is.EqualTo(TriageLevel.Esi4_LessUrgent));
        Assert.That(state.ExpectedResources, Is.EqualTo(1));
    }

    [Test]
    public async Task SignTriage_SetsSignedStatus()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "P3", ChiefComplaint = "Headache", NurseId = "RN-1", NurseName = "Nurse" });
        await grain.SignAsync("RN-1", "Nurse");
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(NursingAssessmentStatus.Signed));
        Assert.That(state.SignedDate, Is.Not.Null);
    }

    [Test]
    public async Task SetDisposition_UpdatesDisposition()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "P4", ChiefComplaint = "Abdominal pain", NurseId = "RN-1", NurseName = "Nurse" });
        await grain.SetDispositionAsync(TriageDisposition.Admit);
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.Disposition, Is.EqualTo(TriageDisposition.Admit));
    }

    [Test]
    public async Task UpdateVitals_StoresValues()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "P5", ChiefComplaint = "SOB", NurseId = "RN-1", NurseName = "Nurse" });
        await grain.UpdateVitalsAsync(101.2m, 110, 24, 160, 95, 92m, 6);
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.Temperature, Is.EqualTo(101.2m));
        Assert.That(state.HeartRate, Is.EqualTo(110));
        Assert.That(state.SpO2, Is.EqualTo(92m));
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_CreateTriage_ReturnsId()
    {
        string pid = $"PAT-TRI-{Guid.NewGuid()}";
        string id = await WF(pid).CreateTriageAssessmentAsync(
            DateTime.UtcNow, "RN-TRIAGE", "Triage Nurse",
            "ED-1", "Emergency Department", "Chest pain, onset 2 hours ago",
            "Sharp substernal pain radiating to left arm",
            99.1m, 92, 20, 148, 88, 97m, 8,
            TriageLevel.Esi2_Emergent, 3, "ALERT", "AMBULANCE", true, true, "EKG ordered stat");
        Assert.That(id, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Workflow_CreateTriage_StoresVitalsAndComplaint()
    {
        string pid = $"PAT-TRI2-{Guid.NewGuid()}";
        string id = await WF(pid).CreateTriageAssessmentAsync(
            DateTime.UtcNow, "RN-1", "Nurse", null, null,
            "Fall from standing", null, 98.6m, 78, 16, 130, 82, 99m, 4,
            TriageLevel.Esi3_Urgent, 2, "ALERT", "AMBULATORY", false, false, null);
        NursingTriageState state = await WF(pid).GetTriageAssessmentAsync(id);
        Assert.That(state.ChiefComplaint, Is.EqualTo("Fall from standing"));
        Assert.That(state.HeartRate, Is.EqualTo(78));
        Assert.That(state.TriageLevel, Is.EqualTo(TriageLevel.Esi3_Urgent));
    }

    [Test]
    public async Task Workflow_CreateTriage_UpdatesIndex()
    {
        string pid = $"PAT-TRI3-{Guid.NewGuid()}";
        await WF(pid).CreateTriageAssessmentAsync(
            DateTime.UtcNow, "RN-1", "Nurse", null, null,
            "Sore throat", null, null, null, null, null, null, null, 2,
            TriageLevel.Esi5_NonUrgent, 0, null, "AMBULATORY", false, false, null);
        List<NursingTriageIndexEntry> triages = await WF(pid).GetTriageAssessmentsAsync();
        Assert.That(triages, Has.Count.EqualTo(1));
        Assert.That(triages[0].TriageLevel, Is.EqualTo(TriageLevel.Esi5_NonUrgent));
    }

    [Test]
    public async Task Workflow_SignTriage_UpdatesIndexStatus()
    {
        string pid = $"PAT-TRI4-{Guid.NewGuid()}";
        string id = await WF(pid).CreateTriageAssessmentAsync(
            DateTime.UtcNow, "RN-1", "Nurse", null, null,
            "Laceration", null, null, null, null, null, null, null, 3,
            TriageLevel.Esi4_LessUrgent, 1, null, null, false, false, null);
        await WF(pid).SignTriageAssessmentAsync(id, "RN-1", "Nurse");
        List<NursingTriageIndexEntry> triages = await WF(pid).GetTriageAssessmentsAsync();
        Assert.That(triages[0].Status, Is.EqualTo(NursingAssessmentStatus.Signed));
    }

    [Test]
    public async Task Workflow_SetDisposition_UpdatesIndex()
    {
        string pid = $"PAT-TRI5-{Guid.NewGuid()}";
        string id = await WF(pid).CreateTriageAssessmentAsync(
            DateTime.UtcNow, "RN-1", "Nurse", null, null,
            "Syncope", null, null, null, null, null, null, null, null,
            TriageLevel.Esi2_Emergent, null, null, null, false, false, null);
        await WF(pid).SetTriageDispositionAsync(id, TriageDisposition.Observation);
        List<NursingTriageIndexEntry> triages = await WF(pid).GetTriageAssessmentsAsync();
        Assert.That(triages[0].Disposition, Is.EqualTo(TriageDisposition.Observation));
    }

    // ─── ESI Level Coverage ──────────────────────────────────────────────────

    [Test]
    public async Task ESI1_Resuscitation()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "ESI1", ChiefComplaint = "Cardiac arrest", NurseId = "RN-1", NurseName = "Nurse", TriageLevel = TriageLevel.Esi1_Resuscitation, IsAcuteDistress = true });
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.TriageLevel, Is.EqualTo(TriageLevel.Esi1_Resuscitation));
        Assert.That(state.IsAcuteDistress, Is.True);
    }

    [Test]
    public async Task ESI5_NonUrgent()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "ESI5", ChiefComplaint = "Prescription refill", NurseId = "RN-1", NurseName = "Nurse", TriageLevel = TriageLevel.Esi5_NonUrgent, ExpectedResources = 0 });
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.TriageLevel, Is.EqualTo(TriageLevel.Esi5_NonUrgent));
        Assert.That(state.ExpectedResources, Is.EqualTo(0));
    }

    [Test]
    public async Task Disposition_LWBS()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "LWBS", ChiefComplaint = "Cough", NurseId = "RN-1", NurseName = "Nurse" });
        await grain.SetDispositionAsync(TriageDisposition.LWBS);
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.Disposition, Is.EqualTo(TriageDisposition.LWBS));
    }

    [Test]
    public async Task Disposition_AMA()
    {
        INursingTriageGrain grain = NewTriage();
        await grain.CreateAsync(new NursingTriageState { PatientId = "AMA", ChiefComplaint = "Back pain", NurseId = "RN-1", NurseName = "Nurse" });
        await grain.SetDispositionAsync(TriageDisposition.AMA);
        NursingTriageState state = await grain.GetAsync();
        Assert.That(state.Disposition, Is.EqualTo(TriageDisposition.AMA));
    }
}
