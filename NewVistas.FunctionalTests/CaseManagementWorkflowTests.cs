// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// R2 (Whole-Person Social Care roadmap) — the case-management goal/outcome spine: a goal with
/// work-steps, follow-ups, and a recorded outcome, with the caseload index tracking active goals.
/// NonParallelizable — toggles the SOCIAL_CARE feature.
/// </summary>
[TestFixture, NonParallelizable]
public class CaseManagementWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string Feature = "SOCIAL_CARE";
    private const string By = "SW1";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(Feature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(Feature);

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private ICaseManagementGrain Case(string patientId) => _cluster.GrainFactory.GetGrain<ICaseManagementGrain>($"CASE-MGMT:{patientId}");

    [Test]
    public async Task GoalLifecycle_StepsFollowUpsOutcome_AndCaseloadIndex()
    {
        string patient = $"CASE-{Guid.NewGuid()}";

        string goalId = await Wf(patient).AddCaseGoalAsync("Obtain stable housing", CaseGoalDomain.Housing,
            new DateTime(2026, 12, 1), "SDOH:housing", By);

        // Work-step → done; follow-up → complete.
        string stepId = await Case(patient).AddWorkStepAsync(goalId, "Submit housing application", new DateTime(2026, 8, 1), By);
        await Case(patient).UpdateWorkStepStatusAsync(goalId, stepId, CaseWorkStepStatus.Done, By);
        string fuId = await Case(patient).AddFollowUpAsync(goalId, new DateTime(2026, 9, 1), "Check application status", By);
        await Case(patient).CompleteFollowUpAsync(goalId, fuId, By);

        CaseManagementState mid = await Wf(patient).GetCaseManagementAsync();
        CaseGoal goal = mid.Goals.Single(g => g.GoalId == goalId);
        Assert.That(goal.Status, Is.EqualTo(CaseGoalStatus.Active));
        Assert.That(goal.WorkSteps.Single().Status, Is.EqualTo(CaseWorkStepStatus.Done));
        Assert.That(goal.FollowUps.Single().Completed, Is.True);
        Assert.That(goal.SourceReference, Is.EqualTo("SDOH:housing"));

        // Caseload index shows 1 active goal.
        List<CaseloadEntry> caseload = await _cluster.GrainFactory.GetGrain<ICaseManagementIndexGrain>("CASE-MGMT-INDEX").GetCaseloadAsync();
        Assert.That(caseload.Single(e => e.PatientId == patient).ActiveGoalCount, Is.EqualTo(1));

        // Record outcome → goal closes → no longer active in the caseload.
        await Case(patient).RecordOutcomeAsync(goalId, achieved: true, "Housed via county program", By);
        CaseManagementState after = await Wf(patient).GetCaseManagementAsync();
        Assert.That(after.Goals.Single().Status, Is.EqualTo(CaseGoalStatus.Achieved));
        Assert.That(after.Goals.Single().OutcomeAchieved, Is.True);

        caseload = await _cluster.GrainFactory.GetGrain<ICaseManagementIndexGrain>("CASE-MGMT-INDEX").GetCaseloadAsync();
        Assert.That(caseload.Single(e => e.PatientId == patient).ActiveGoalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task FlagOff_CaseManagementEmpty()
    {
        string patient = $"CASE-{Guid.NewGuid()}";
        await Wf(patient).AddCaseGoalAsync("x", CaseGoalDomain.Health, null, null, By);

        await SiteParams().DisableFeatureAsync(Feature);
        try
        {
            Assert.That((await Wf(patient).GetCaseManagementAsync()).Goals, Is.Empty);
        }
        finally
        {
            await SiteParams().EnableFeatureAsync(Feature);
        }
    }
}
