// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class PainAssessmentWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPainAssessmentGrain NewPain()
        => _cluster.GrainFactory.GetGrain<IPainAssessmentGrain>($"NURS-PAIN:{Guid.NewGuid()}");
    private IPatientWorkflowGrain WF(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    // ─── Grain Level ─────────────────────────────────────────────────────────

    [Test]
    public async Task CreatePainAssessment_SetsDraftStatus()
    {
        IPainAssessmentGrain grain = NewPain();
        await grain.CreateAsync(new PainAssessmentState { PatientId = "P1", NurseId = "RN-1", NurseName = "Nurse", Tool = PainAssessmentTool.NumericRatingScale, PainScore = 7, PainLocation = "Right knee" });
        PainAssessmentState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(NursingAssessmentStatus.Draft));
        Assert.That(state.PainScore, Is.EqualTo(7));
    }

    [Test]
    public async Task SignAssessment_SetsSignedStatus()
    {
        IPainAssessmentGrain grain = NewPain();
        await grain.CreateAsync(new PainAssessmentState { PatientId = "P2", NurseId = "RN-1", NurseName = "Nurse", Tool = PainAssessmentTool.DVPRS, PainScore = 5 });
        await grain.SignAsync("RN-1", "Nurse");
        PainAssessmentState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(NursingAssessmentStatus.Signed));
    }

    [Test]
    public async Task RecordReassessment_StoresPostInterventionData()
    {
        IPainAssessmentGrain grain = NewPain();
        await grain.CreateAsync(new PainAssessmentState { PatientId = "P3", NurseId = "RN-1", NurseName = "Nurse", Tool = PainAssessmentTool.NumericRatingScale, PainScore = 8 });
        await grain.RecordReassessmentAsync(3, 30, true);
        PainAssessmentState state = await grain.GetAsync();
        Assert.That(state.PostInterventionScore, Is.EqualTo(3));
        Assert.That(state.MinutesSinceIntervention, Is.EqualTo(30));
        Assert.That(state.InterventionEffective, Is.True);
    }

    // ─── Workflow — NRS ──────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_RecordNRS_ReturnsIdAndStoresScore()
    {
        string pid = $"PAT-PAIN-{Guid.NewGuid()}";
        string id = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.NumericRatingScale, 6, "Lower back", "Aching", "Chronic",
            "Sitting", "Ice pack", "Down left leg", 3, null, null, "Ibuprofen 400mg PO", "RN-1", "Nurse Smith", null);
        Assert.That(id, Is.Not.Null.And.Not.Empty);
        PainAssessmentState state = await WF(pid).GetPainAssessmentAsync(id);
        Assert.That(state.PainScore, Is.EqualTo(6));
        Assert.That(state.Tool, Is.EqualTo(PainAssessmentTool.NumericRatingScale));
        Assert.That(state.Radiation, Is.EqualTo("Down left leg"));
    }

    // ─── Workflow — DVPRS ────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_RecordDVPRS_StoresSupplemental()
    {
        string pid = $"PAT-DVPRS-{Guid.NewGuid()}";
        DvprsSupplemental supp = new() { ActivityInterference = 7, SleepInterference = 8, MoodInterference = 5, StressInterference = 6 };
        string id = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.DVPRS, 7, "Bilateral shoulders", "Throbbing", "Acute",
            null, null, null, 4, supp, null, "Morphine 4mg IV", "RN-2", "Nurse Jones", null);
        PainAssessmentState state = await WF(pid).GetPainAssessmentAsync(id);
        Assert.That(state.DvprsSupplemental, Is.Not.Null);
        Assert.That(state.DvprsSupplemental!.ActivityInterference, Is.EqualTo(7));
        Assert.That(state.DvprsSupplemental.SleepInterference, Is.EqualTo(8));
    }

    // ─── Workflow — FLACC ────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_RecordFLACC_StoresComponents()
    {
        string pid = $"PAT-FLACC-{Guid.NewGuid()}";
        FlaccScore flacc = new() { Face = 2, Legs = 1, Activity = 2, Cry = 2, Consolability = 1, Total = 8 };
        string id = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.FLACC, 8, null, null, null,
            null, null, null, null, null, flacc, null, "RN-3", "Nurse PEDS", "Non-verbal infant patient");
        PainAssessmentState state = await WF(pid).GetPainAssessmentAsync(id);
        Assert.That(state.FlaccComponents, Is.Not.Null);
        Assert.That(state.FlaccComponents!.Total, Is.EqualTo(8));
        Assert.That(state.FlaccComponents.Face, Is.EqualTo(2));
    }

    // ─── Workflow — Reassessment ─────────────────────────────────────────────

    [Test]
    public async Task Workflow_Reassessment_LinksToInitial()
    {
        string pid = $"PAT-REASSESS-{Guid.NewGuid()}";
        string initialId = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.NumericRatingScale, 8, "Abdomen", "Sharp", "Acute",
            null, null, null, 3, null, null, "Dilaudid 0.5mg IV", "RN-1", "Nurse", null);

        string reassessId = await WF(pid).RecordPainReassessmentAsync(
            initialId, PainAssessmentTool.NumericRatingScale, 4, 30, "Dilaudid 0.5mg IV", "RN-1", "Nurse", "Pain improving");

        PainAssessmentState reassess = await WF(pid).GetPainAssessmentAsync(reassessId);
        Assert.That(reassess.IsReassessment, Is.True);
        Assert.That(reassess.InitialAssessmentId, Is.EqualTo(initialId));
        Assert.That(reassess.PostInterventionScore, Is.EqualTo(4));
        Assert.That(reassess.InterventionEffective, Is.True);
    }

    [Test]
    public async Task Workflow_Reassessment_IneffectiveWhenScoreNotDecreased()
    {
        string pid = $"PAT-REASSESS2-{Guid.NewGuid()}";
        string initialId = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.NumericRatingScale, 6, "Head", "Throbbing", null,
            null, null, null, null, null, null, "Tylenol 1000mg PO", "RN-1", "Nurse", null);

        string reassessId = await WF(pid).RecordPainReassessmentAsync(
            initialId, PainAssessmentTool.NumericRatingScale, 7, 45, "Tylenol 1000mg PO", "RN-1", "Nurse", "Pain worsened");

        PainAssessmentState reassess = await WF(pid).GetPainAssessmentAsync(reassessId);
        Assert.That(reassess.InterventionEffective, Is.False);
    }

    // ─── Workflow — Index & History ──────────────────────────────────────────

    [Test]
    public async Task Workflow_GetPainAssessments_ReturnsHistory()
    {
        string pid = $"PAT-PAIN-HIST-{Guid.NewGuid()}";
        await WF(pid).RecordPainAssessmentAsync(PainAssessmentTool.NumericRatingScale, 5, "Knee", null, null, null, null, null, null, null, null, null, "RN-1", "Nurse", null);
        await WF(pid).RecordPainAssessmentAsync(PainAssessmentTool.DVPRS, 7, "Back", null, null, null, null, null, null, null, null, null, "RN-2", "Nurse2", null);
        List<PainAssessmentIndexEntry> list = await WF(pid).GetPainAssessmentsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Workflow_GetLatest_ReturnsNewest()
    {
        string pid = $"PAT-PAIN-LAT-{Guid.NewGuid()}";
        await WF(pid).RecordPainAssessmentAsync(PainAssessmentTool.NumericRatingScale, 3, "Arm", null, null, null, null, null, null, null, null, null, "RN-1", "First", null);
        await WF(pid).RecordPainAssessmentAsync(PainAssessmentTool.WongBakerFACES, 6, "Tummy", null, null, null, null, null, null, null, null, null, "RN-2", "Second", null);
        PainAssessmentIndexEntry? latest = await WF(pid).GetLatestPainAssessmentAsync();
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.NurseName, Is.EqualTo("Second"));
        Assert.That(latest.Tool, Is.EqualTo(PainAssessmentTool.WongBakerFACES));
    }

    [Test]
    public async Task Workflow_WongBakerFACES_RecordsCorrectTool()
    {
        string pid = $"PAT-FACES-{Guid.NewGuid()}";
        string id = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.WongBakerFACES, 4, "Ear", null, null,
            null, null, null, 2, null, null, null, "RN-PEDS", "Pediatric Nurse", "Age 5, used FACES");
        PainAssessmentState state = await WF(pid).GetPainAssessmentAsync(id);
        Assert.That(state.Tool, Is.EqualTo(PainAssessmentTool.WongBakerFACES));
        Assert.That(state.AcceptablePainLevel, Is.EqualTo(2));
    }

    [Test]
    public async Task Workflow_CPOT_RecordsForIntubatedPatient()
    {
        string pid = $"PAT-CPOT-{Guid.NewGuid()}";
        string id = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.CPOT, 5, null, null, null,
            null, null, null, null, null, null, "Fentanyl drip titrated", "RN-ICU", "ICU Nurse", "Intubated/sedated");
        PainAssessmentState state = await WF(pid).GetPainAssessmentAsync(id);
        Assert.That(state.Tool, Is.EqualTo(PainAssessmentTool.CPOT));
    }

    [Test]
    public async Task Workflow_ReassessmentUpdatesInitialAssessment()
    {
        string pid = $"PAT-REASSESS3-{Guid.NewGuid()}";
        string initialId = await WF(pid).RecordPainAssessmentAsync(
            PainAssessmentTool.NumericRatingScale, 9, "Chest", "Stabbing", "Acute",
            null, null, null, null, null, null, "Morphine 2mg IV", "RN-1", "Nurse", null);

        await WF(pid).RecordPainReassessmentAsync(initialId, PainAssessmentTool.NumericRatingScale, 5, 15, null, "RN-1", "Nurse", null);

        // Check that the initial assessment got updated with reassessment data
        PainAssessmentState initial = await WF(pid).GetPainAssessmentAsync(initialId);
        Assert.That(initial.PostInterventionScore, Is.EqualTo(5));
        Assert.That(initial.MinutesSinceIntervention, Is.EqualTo(15));
    }
}
