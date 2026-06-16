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
/// Functional tests for Nursing Care Plan wired to workflow —
/// NANDA diagnoses, goals, interventions, outcomes (VistA File #212).
/// </summary>
[TestFixture]
public class NursingCarePlanWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain WF(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [Test]
    public async Task AddDiagnosis_ReturnsNonEmptyId()
    {
        string pid = $"PAT-NCP-{Guid.NewGuid()}";
        string id = await WF(pid).AddNursingDiagnosisAsync("Acute Pain", "surgical incision", "reports 7/10 pain", 1, "RN-1", "Nurse Smith");
        Assert.That(id, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task AddDiagnosis_AppearsInCarePlan()
    {
        string pid = $"PAT-NCP2-{Guid.NewGuid()}";
        await WF(pid).AddNursingDiagnosisAsync("Impaired Physical Mobility", "hip fracture", "unable to bear weight", 2, "RN-1", "Nurse");
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems, Has.Count.EqualTo(1));
        Assert.That(cp.Problems[0].NursingDiagnosis, Is.EqualTo("Impaired Physical Mobility"));
        Assert.That(cp.Problems[0].Status, Is.EqualTo(NursingCarePlanStatus.Active));
    }

    [Test]
    public async Task AddGoal_AttachesToDiagnosis()
    {
        string pid = $"PAT-NCP3-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Risk for Falls", "impaired gait", null, null, null, null);
        await WF(pid).AddCarePlanGoalAsync(probId, "Patient will ambulate with walker without falling by discharge", DateTime.UtcNow.AddDays(3));
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].Goals, Has.Count.EqualTo(1));
        Assert.That(cp.Problems[0].Goals[0].Status, Is.EqualTo(NursingGoalStatus.Pending));
    }

    [Test]
    public async Task AddIntervention_AttachesToDiagnosis()
    {
        string pid = $"PAT-NCP4-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Impaired Skin Integrity", "pressure", "stage 2 sacral ulcer", null, null, null);
        await WF(pid).AddCarePlanInterventionAsync(probId, "Reposition patient every 2 hours", "Q2H", "RN-1", "Nurse");
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].Interventions, Has.Count.EqualTo(1));
        Assert.That(cp.Problems[0].Interventions[0].Frequency, Is.EqualTo("Q2H"));
    }

    [Test]
    public async Task RecordOutcome_AppearsInEvaluations()
    {
        string pid = $"PAT-NCP5-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Acute Pain", "surgery", null, null, null, null);
        await WF(pid).RecordCarePlanOutcomeAsync(probId, NursingOutcomeRating.GoalPartiallyMet, "RN-1", "Nurse Smith", "Pain at 4/10, improving");
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].OutcomeEvaluations, Has.Count.EqualTo(1));
        Assert.That(cp.Problems[0].OutcomeEvaluations[0].OutcomeRating, Is.EqualTo(NursingOutcomeRating.GoalPartiallyMet));
    }

    [Test]
    public async Task UpdateGoalStatus_ChangesStatus()
    {
        string pid = $"PAT-NCP6-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Risk for Infection", null, null, null, null, null);
        await WF(pid).AddCarePlanGoalAsync(probId, "No signs of infection by POD3", null);
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        string goalId = cp.Problems[0].Goals[0].GoalId;
        await WF(pid).UpdateCarePlanGoalStatusAsync(probId, goalId, NursingGoalStatus.Achieved);
        cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].Goals[0].Status, Is.EqualTo(NursingGoalStatus.Achieved));
    }

    [Test]
    public async Task ResolveDiagnosis_SetsResolvedStatus()
    {
        string pid = $"PAT-NCP7-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Nausea", "medication side effect", null, null, null, null);
        await WF(pid).ResolveNursingDiagnosisAsync(probId, "Resolved — nausea subsided after med change");
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].Status, Is.EqualTo(NursingCarePlanStatus.Resolved));
    }

    [Test]
    public async Task MultipleDiagnoses_AllTracked()
    {
        string pid = $"PAT-NCP8-{Guid.NewGuid()}";
        await WF(pid).AddNursingDiagnosisAsync("Acute Pain", "fracture", null, 1, null, null);
        await WF(pid).AddNursingDiagnosisAsync("Anxiety", "hospitalization", null, 2, null, null);
        await WF(pid).AddNursingDiagnosisAsync("Risk for DVT", "immobility", null, 3, null, null);
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task CarePlan_PersistsAcrossReads()
    {
        string pid = $"PAT-NCP9-{Guid.NewGuid()}";
        await WF(pid).AddNursingDiagnosisAsync("Ineffective Breathing Pattern", "pain", "shallow respirations", null, null, null);
        NursingCarePlanState cp1 = await WF(pid).GetNursingCarePlanAsync();
        NursingCarePlanState cp2 = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp2.Problems, Has.Count.EqualTo(cp1.Problems.Count));
    }

    [Test]
    public async Task MultipleGoalsAndInterventions()
    {
        string pid = $"PAT-NCP10-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Impaired Physical Mobility", null, null, null, null, null);
        await WF(pid).AddCarePlanGoalAsync(probId, "Ambulate 50ft with walker by POD2", DateTime.UtcNow.AddDays(2));
        await WF(pid).AddCarePlanGoalAsync(probId, "Transfer bed→chair independently by POD3", DateTime.UtcNow.AddDays(3));
        await WF(pid).AddCarePlanInterventionAsync(probId, "Assist with ambulation TID", "TID", null, null);
        await WF(pid).AddCarePlanInterventionAsync(probId, "PT consult for gait training", null, null, null);
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].Goals, Has.Count.EqualTo(2));
        Assert.That(cp.Problems[0].Interventions, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RecordAcuity_PersistsLevel()
    {
        string pid = $"PAT-NCP11-{Guid.NewGuid()}";
        await WF(pid).RecordNursingAcuityAsync(AcuityLevel.IntensiveCare, 4, "RN-1", "Nurse", "DAY", "Post-op ICU");
        NursingAcuityState acuity = await WF(pid).GetNursingAcuityAsync();
        Assert.That(acuity.CurrentAcuityLevel, Is.EqualTo(AcuityLevel.IntensiveCare));
    }

    [Test]
    public async Task Acuity_RecordsHistory()
    {
        string pid = $"PAT-NCP12-{Guid.NewGuid()}";
        await WF(pid).RecordNursingAcuityAsync(AcuityLevel.IntensiveCare, 4, "RN-1", "Nurse", "DAY", null);
        await WF(pid).RecordNursingAcuityAsync(AcuityLevel.AboveAverageCare, 3, "RN-2", "Nurse2", "EVE", null);
        NursingAcuityState acuity = await WF(pid).GetNursingAcuityAsync();
        Assert.That(acuity.AcuityHistory, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(acuity.CurrentAcuityLevel, Is.EqualTo(AcuityLevel.AboveAverageCare));
    }

    [Test]
    public async Task Assessment_CreateAndSign()
    {
        string pid = $"PAT-NCP13-{Guid.NewGuid()}";
        string assessId = await WF(pid).CreateNursingAssessmentAsync(
            DateTime.UtcNow, "Shift", "RN-1", "Nurse Test", null, "WARD-MED-3A",
            "Alert", new List<string> { "Person", "Place", "Time" },
            "Clear bilateral", null, 98m, "Regular", null, "Intact", 18,
            3, "Left knee", "Active", "Good", 400m, false,
            "None", "Cooperative", 25, "Low", new List<string> { "Call bell within reach" },
            "Ambulatory with walker", "Routine shift assessment");
        await WF(pid).SignNursingAssessmentAsync(assessId, "RN-1", "Nurse Test");
        NursingAssessmentState assess = await WF(pid).GetNursingAssessmentAsync(assessId);
        Assert.That(assess.Status, Is.EqualTo(NursingAssessmentStatus.Signed));
    }

    [Test]
    public async Task Assessment_AppearsInIndex()
    {
        string pid = $"PAT-NCP14-{Guid.NewGuid()}";
        await WF(pid).CreateNursingAssessmentAsync(
            DateTime.UtcNow, "Initial", "RN-1", "Nurse", null, null,
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, false, null, null, null, null, null, null, null);
        List<NursingAssessmentIndexEntry> list = await WF(pid).GetNursingAssessmentsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OutcomeEvaluation_GoalMet()
    {
        string pid = $"PAT-NCP15-{Guid.NewGuid()}";
        string probId = await WF(pid).AddNursingDiagnosisAsync("Acute Pain", null, null, null, null, null);
        await WF(pid).AddCarePlanGoalAsync(probId, "Pain <=3/10 within 30min of intervention", null);
        await WF(pid).RecordCarePlanOutcomeAsync(probId, NursingOutcomeRating.GoalMet, "RN-1", "Nurse", "Patient reports pain 2/10");
        NursingCarePlanState cp = await WF(pid).GetNursingCarePlanAsync();
        Assert.That(cp.Problems[0].OutcomeEvaluations[0].OutcomeRating, Is.EqualTo(NursingOutcomeRating.GoalMet));
    }
}
