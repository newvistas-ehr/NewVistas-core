// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Tests;

[TestFixture]
public class PTGoalTests
{
    // ── Add Goal ────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddGoal_ROM_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId = await workflow.AddGoalAsync(BodyGroup.Shoulder, new PTGoal
        {
            GoalType = GoalType.ROM,
            Movement = Movement.Flexion,
            Side = Laterality.Left,
            Description = "Achieve 150 degrees shoulder flexion",
            TargetValue = 150m,
            BaselineValue = 90m,
            CurrentValue = 90m,
            TargetDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Notes = "Post-surgical recovery"
        });

        Assert.That(goalId, Is.Not.Null.And.Not.Empty);

        List<PTGoal> goals = await workflow.GetGoalsForBodyGroupAsync(BodyGroup.Shoulder);
        Assert.That(goals, Has.Count.EqualTo(1));

        PTGoal goal = goals[0];
        Assert.That(goal.GoalId, Is.EqualTo(goalId));
        Assert.That(goal.GoalType, Is.EqualTo(GoalType.ROM));
        Assert.That(goal.BodyGroup, Is.EqualTo(BodyGroup.Shoulder));
        Assert.That(goal.Movement, Is.EqualTo(Movement.Flexion));
        Assert.That(goal.Side, Is.EqualTo(Laterality.Left));
        Assert.That(goal.Description, Is.EqualTo("Achieve 150 degrees shoulder flexion"));
        Assert.That(goal.TargetValue, Is.EqualTo(150m));
        Assert.That(goal.BaselineValue, Is.EqualTo(90m));
        Assert.That(goal.CurrentValue, Is.EqualTo(90m));
        Assert.That(goal.Status, Is.EqualTo(GoalStatus.Active));
        Assert.That(goal.Notes, Is.EqualTo("Post-surgical recovery"));
    }

    [Test]
    public async Task AddGoal_Strength_WithMovement()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId = await workflow.AddGoalAsync(BodyGroup.Knee, new PTGoal
        {
            GoalType = GoalType.Strength,
            Movement = Movement.Extension,
            Description = "Achieve 4/5 knee extension strength",
            TargetValue = 4m,
            BaselineValue = 2.67m,
            CurrentValue = 2.67m
        });

        List<PTGoal> goals = await workflow.GetGoalsForBodyGroupAsync(BodyGroup.Knee);
        Assert.That(goals, Has.Count.EqualTo(1));
        Assert.That(goals[0].GoalType, Is.EqualTo(GoalType.Strength));
        Assert.That(goals[0].Movement, Is.EqualTo(Movement.Extension));
        Assert.That(goals[0].TargetValue, Is.EqualTo(4m));
    }

    [Test]
    public async Task AddGoal_PainReduction()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId = await workflow.AddGoalAsync(BodyGroup.LumbarSpine, new PTGoal
        {
            GoalType = GoalType.PainReduction,
            Description = "Reduce low back pain from 7/10 to 3/10",
            TargetValue = 3m,
            BaselineValue = 7m,
            CurrentValue = 7m
        });

        List<PTGoal> goals = await workflow.GetGoalsForBodyGroupAsync(BodyGroup.LumbarSpine);
        Assert.That(goals, Has.Count.EqualTo(1));
        Assert.That(goals[0].GoalType, Is.EqualTo(GoalType.PainReduction));
        Assert.That(goals[0].BaselineValue, Is.EqualTo(7m));
        Assert.That(goals[0].TargetValue, Is.EqualTo(3m));
    }

    // ── Update Goal ─────────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateGoalStatus_Achieved()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId = await workflow.AddGoalAsync(BodyGroup.Shoulder, new PTGoal
        {
            GoalType = GoalType.ROM,
            Movement = Movement.Flexion,
            TargetValue = 150m,
            BaselineValue = 90m,
            CurrentValue = 90m
        });

        await workflow.UpdateGoalAsync(BodyGroup.Shoulder, goalId, GoalStatus.Achieved, 150m, "Goal met!");

        List<PTGoal> goals = await workflow.GetGoalsForBodyGroupAsync(BodyGroup.Shoulder);
        Assert.That(goals[0].Status, Is.EqualTo(GoalStatus.Achieved));
        Assert.That(goals[0].CurrentValue, Is.EqualTo(150m));
        Assert.That(goals[0].Notes, Is.EqualTo("Goal met!"));
    }

    [Test]
    public async Task UpdateGoalCurrentValue_Updates()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId = await workflow.AddGoalAsync(BodyGroup.Hip, new PTGoal
        {
            GoalType = GoalType.ROM,
            Movement = Movement.Flexion,
            TargetValue = 120m,
            BaselineValue = 60m,
            CurrentValue = 60m
        });

        await workflow.UpdateGoalAsync(BodyGroup.Hip, goalId, null, 85m, null);

        List<PTGoal> goals = await workflow.GetGoalsForBodyGroupAsync(BodyGroup.Hip);
        Assert.That(goals[0].CurrentValue, Is.EqualTo(85m));
        Assert.That(goals[0].Status, Is.EqualTo(GoalStatus.Active));
    }

    // ── Progress Entries ────────────────────────────────────────────────────────

    [Test]
    public async Task AddProgressEntry_AppendsToHistory()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId = await workflow.AddGoalAsync(BodyGroup.Knee, new PTGoal
        {
            GoalType = GoalType.ROM,
            Movement = Movement.Flexion,
            TargetValue = 130m,
            BaselineValue = 70m,
            CurrentValue = 70m
        });

        await workflow.AddGoalProgressAsync(BodyGroup.Knee, goalId, 85m, "Week 2");
        await workflow.AddGoalProgressAsync(BodyGroup.Knee, goalId, 100m, "Week 4");
        await workflow.AddGoalProgressAsync(BodyGroup.Knee, goalId, 115m, "Week 6");

        List<PTGoal> goals = await workflow.GetGoalsForBodyGroupAsync(BodyGroup.Knee);
        Assert.That(goals[0].ProgressEntries, Has.Count.EqualTo(3));
        Assert.That(goals[0].CurrentValue, Is.EqualTo(115m));
        Assert.That(goals[0].ProgressEntries[0].Value, Is.EqualTo(85m));
        Assert.That(goals[0].ProgressEntries[1].Notes, Is.EqualTo("Week 4"));
        Assert.That(goals[0].ProgressEntries[2].Value, Is.EqualTo(115m));
    }

    // ── Active Goals Filtering ──────────────────────────────────────────────────

    [Test]
    public async Task GetActiveGoals_FiltersCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string goalId1 = await workflow.AddGoalAsync(BodyGroup.Shoulder, new PTGoal
        {
            GoalType = GoalType.ROM, TargetValue = 150m, BaselineValue = 90m, CurrentValue = 90m
        });
        await workflow.AddGoalAsync(BodyGroup.Shoulder, new PTGoal
        {
            GoalType = GoalType.Strength, TargetValue = 5m, BaselineValue = 3m, CurrentValue = 3m
        });

        // Discontinue the first goal
        await workflow.UpdateGoalAsync(BodyGroup.Shoulder, goalId1, GoalStatus.Discontinued, null, null);

        IPTGoalGrain goalGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTGoalGrain>($"PTGOAL:{patientId}:{BodyGroup.Shoulder}");
        List<PTGoal> active = await goalGrain.GetActiveGoalsAsync();

        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].GoalType, Is.EqualTo(GoalType.Strength));
    }

    // ── Fan-Out Across Body Groups ──────────────────────────────────────────────

    [Test]
    public async Task GetAllActiveGoals_FansOutAcrossBodyGroups()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        await workflow.AddGoalAsync(BodyGroup.Shoulder, new PTGoal
        {
            GoalType = GoalType.ROM, TargetValue = 150m, BaselineValue = 90m, CurrentValue = 90m
        });
        await workflow.AddGoalAsync(BodyGroup.Knee, new PTGoal
        {
            GoalType = GoalType.Strength, TargetValue = 4m, BaselineValue = 2m, CurrentValue = 2m
        });
        await workflow.AddGoalAsync(BodyGroup.LumbarSpine, new PTGoal
        {
            GoalType = GoalType.PainReduction, TargetValue = 2m, BaselineValue = 8m, CurrentValue = 8m
        });

        List<PTGoal> allActive = await workflow.GetAllActiveGoalsAsync();
        Assert.That(allActive, Has.Count.EqualTo(3));
    }

    // ── Remove Goal ─────────────────────────────────────────────────────────────

    [Test]
    public async Task RemoveGoal_RemovesFromList()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTGoalGrain goalGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTGoalGrain>($"PTGOAL:{patientId}:{BodyGroup.Ankle}");

        string goalId = await goalGrain.AddGoalAsync(new PTGoal
        {
            GoalType = GoalType.ROM, TargetValue = 20m, BaselineValue = 5m, CurrentValue = 5m
        });

        await goalGrain.RemoveGoalAsync(goalId);

        PTGoalState state = await goalGrain.GetGoalsAsync();
        Assert.That(state.Goals, Has.Count.EqualTo(0));
    }
}
