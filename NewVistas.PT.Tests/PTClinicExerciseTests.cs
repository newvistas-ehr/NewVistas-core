// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Tests;

[TestFixture]
public class PTClinicExerciseTests
{
    // ── Add Exercise to Session ─────────────────────────────────────────────────

    [Test]
    public async Task AddExerciseLog_PersistsToSession()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        // Record a session first
        string sessionKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Knee,
            new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc),
            null, "Dr. Smith", null, "PT Clinic",
            Laterality.Right,
            [new() { Movement = Movement.Flexion, ActiveRom = 90m }],
            [],
            "Session with exercises");

        // Add an exercise
        await workflow.AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
        {
            ExerciseName = "Quad Sets",
            Category = ExerciseCategory.Strengthening,
            BodyGroup = BodyGroup.Knee,
            Movement = Movement.Extension,
            Sets = 3,
            Reps = 10,
            Notes = "Good form maintained"
        });

        // Verify
        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(sessionKey);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.Exercises, Has.Count.EqualTo(1));
        Assert.That(state.Exercises[0].ExerciseName, Is.EqualTo("Quad Sets"));
        Assert.That(state.Exercises[0].Category, Is.EqualTo(ExerciseCategory.Strengthening));
        Assert.That(state.Exercises[0].Sets, Is.EqualTo(3));
        Assert.That(state.Exercises[0].Reps, Is.EqualTo(10));
        Assert.That(state.Exercises[0].Movement, Is.EqualTo(Movement.Extension));
    }

    [Test]
    public async Task AddExerciseLog_TimeBased_NullReps()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string sessionKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, DateTime.UtcNow,
            null, "Dr. Jones", null, "PT Clinic",
            Laterality.Left, [], [], null);

        await workflow.AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
        {
            ExerciseName = "Cross-Body Stretch",
            Category = ExerciseCategory.Stretching,
            BodyGroup = BodyGroup.Shoulder,
            DurationSeconds = 30,
            Sets = 3,
            Notes = "Hold 30 seconds each"
        });

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(sessionKey);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.Exercises[0].DurationSeconds, Is.EqualTo(30));
        Assert.That(state.Exercises[0].Reps, Is.Null);
    }

    [Test]
    public async Task AddExerciseLog_RepBased_NullDuration()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string sessionKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Knee, DateTime.UtcNow,
            null, "Dr. Jones", null, "PT Clinic",
            Laterality.Bilateral, [], [], null);

        await workflow.AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
        {
            ExerciseName = "Leg Press",
            Category = ExerciseCategory.Strengthening,
            BodyGroup = BodyGroup.Knee,
            Sets = 3,
            Reps = 12,
            WeightLbs = 50m
        });

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(sessionKey);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.Exercises[0].Reps, Is.EqualTo(12));
        Assert.That(state.Exercises[0].WeightLbs, Is.EqualTo(50m));
        Assert.That(state.Exercises[0].DurationSeconds, Is.Null);
    }

    [Test]
    public async Task MultipleExercises_AllPersisted()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string sessionKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Hip, DateTime.UtcNow,
            null, "Dr. Smith", null, "PT Clinic",
            Laterality.Right, [], [], null);

        await workflow.AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
        {
            ExerciseName = "Hip Abduction", Category = ExerciseCategory.Strengthening,
            BodyGroup = BodyGroup.Hip, Sets = 3, Reps = 15
        });
        await workflow.AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
        {
            ExerciseName = "Piriformis Stretch", Category = ExerciseCategory.Stretching,
            BodyGroup = BodyGroup.Hip, DurationSeconds = 30, Sets = 3
        });
        await workflow.AddClinicExerciseAsync(sessionKey, new ClinicExerciseLog
        {
            ExerciseName = "Stationary Bike", Category = ExerciseCategory.Endurance,
            BodyGroup = BodyGroup.Hip, DurationSeconds = 600
        });

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(sessionKey);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.Exercises, Has.Count.EqualTo(3));
        Assert.That(state.Exercises[0].ExerciseName, Is.EqualTo("Hip Abduction"));
        Assert.That(state.Exercises[1].ExerciseName, Is.EqualTo("Piriformis Stretch"));
        Assert.That(state.Exercises[2].ExerciseName, Is.EqualTo("Stationary Bike"));
    }
}
