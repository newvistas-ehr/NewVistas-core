// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Tests;

[TestFixture]
public class PTHomeExerciseProgramTests
{
    // ── Add Prescription ────────────────────────────────────────────────────────

    [Test]
    public async Task AddPrescription_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string prescriptionId = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Quad Sets",
            Instructions = "Tighten thigh muscle, hold 5 seconds, relax",
            Frequency = "3x daily",
            Sets = 3,
            Reps = 10,
            BodyGroup = BodyGroup.Knee,
            Movement = Movement.Extension,
            Side = Laterality.Right,
            Category = ExerciseCategory.Strengthening,
            PrescribedBy = "Dr. Smith",
            Notes = "Start with no resistance"
        });

        Assert.That(prescriptionId, Is.Not.Null.And.Not.Empty);

        List<HepPrescription> active = await workflow.GetActiveHepPrescriptionsAsync();
        Assert.That(active, Has.Count.EqualTo(1));

        HepPrescription rx = active[0];
        Assert.That(rx.PrescriptionId, Is.EqualTo(prescriptionId));
        Assert.That(rx.ExerciseName, Is.EqualTo("Quad Sets"));
        Assert.That(rx.Instructions, Is.EqualTo("Tighten thigh muscle, hold 5 seconds, relax"));
        Assert.That(rx.Frequency, Is.EqualTo("3x daily"));
        Assert.That(rx.Sets, Is.EqualTo(3));
        Assert.That(rx.Reps, Is.EqualTo(10));
        Assert.That(rx.BodyGroup, Is.EqualTo(BodyGroup.Knee));
        Assert.That(rx.Movement, Is.EqualTo(Movement.Extension));
        Assert.That(rx.Side, Is.EqualTo(Laterality.Right));
        Assert.That(rx.Category, Is.EqualTo(ExerciseCategory.Strengthening));
        Assert.That(rx.PrescribedBy, Is.EqualTo("Dr. Smith"));
        Assert.That(rx.Status, Is.EqualTo(HepStatus.Active));
    }

    [Test]
    public async Task AddPrescription_TimeBased()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Hamstring Stretch",
            Instructions = "Lie on back, pull knee to chest, straighten leg",
            Frequency = "2x daily",
            DurationSeconds = 30,
            Sets = 3,
            BodyGroup = BodyGroup.Knee,
            Category = ExerciseCategory.Stretching,
            PrescribedBy = "Dr. Jones"
        });

        List<HepPrescription> active = await workflow.GetActiveHepPrescriptionsAsync();
        Assert.That(active[0].DurationSeconds, Is.EqualTo(30));
        Assert.That(active[0].Reps, Is.Null);
    }

    // ── Update Prescription Status ──────────────────────────────────────────────

    [Test]
    public async Task GetActivePrescriptions_FiltersDiscontinued()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string rxId1 = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Quad Sets", Frequency = "3x daily", Sets = 3, Reps = 10,
            BodyGroup = BodyGroup.Knee, Category = ExerciseCategory.Strengthening, PrescribedBy = "Dr. Smith"
        });
        await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Wall Slides", Frequency = "2x daily", Sets = 3, Reps = 15,
            BodyGroup = BodyGroup.Knee, Category = ExerciseCategory.Strengthening, PrescribedBy = "Dr. Smith"
        });

        await workflow.UpdateHepPrescriptionStatusAsync(rxId1, HepStatus.Discontinued);

        List<HepPrescription> active = await workflow.GetActiveHepPrescriptionsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ExerciseName, Is.EqualTo("Wall Slides"));
    }

    [Test]
    public async Task UpdatePrescriptionStatus_Completed()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string rxId = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Ankle Pumps", Frequency = "hourly", Sets = 1, Reps = 20,
            BodyGroup = BodyGroup.Ankle, Category = ExerciseCategory.Functional, PrescribedBy = "Dr. Smith"
        });

        await workflow.UpdateHepPrescriptionStatusAsync(rxId, HepStatus.Completed);

        List<HepPrescription> active = await workflow.GetActiveHepPrescriptionsAsync();
        Assert.That(active, Has.Count.EqualTo(0));

        // Verify via direct grain access that it's completed, not removed
        IPTHomeExerciseProgramGrain hepGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTHomeExerciseProgramGrain>($"PTHEP:{patientId}");
        PTHomeExerciseProgramState state = await hepGrain.GetProgramAsync();
        Assert.That(state.Prescriptions[0].Status, Is.EqualTo(HepStatus.Completed));
    }

    // ── Completion Logging ──────────────────────────────────────────────────────

    [Test]
    public async Task LogCompletion_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string rxId = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Quad Sets", Frequency = "3x daily", Sets = 3, Reps = 10,
            BodyGroup = BodyGroup.Knee, Category = ExerciseCategory.Strengthening, PrescribedBy = "Dr. Smith"
        });

        DateTime completedDate = new(2026, 4, 4, 8, 0, 0, DateTimeKind.Utc);
        string logId = await workflow.LogHepCompletionAsync(new HepCompletionLog
        {
            PrescriptionId = rxId,
            CompletedDate = completedDate,
            CompletedBy = "Patient",
            SetsCompleted = 3,
            RepsCompleted = 10,
            PainLevel = 2,
            Notes = "Felt good"
        });

        Assert.That(logId, Is.Not.Null.And.Not.Empty);

        List<HepCompletionLog> logs = await workflow.GetHepCompletionLogsAsync(rxId, null, null);
        Assert.That(logs, Has.Count.EqualTo(1));

        HepCompletionLog log = logs[0];
        Assert.That(log.LogId, Is.EqualTo(logId));
        Assert.That(log.PrescriptionId, Is.EqualTo(rxId));
        Assert.That(log.CompletedDate, Is.EqualTo(completedDate));
        Assert.That(log.CompletedBy, Is.EqualTo("Patient"));
        Assert.That(log.SetsCompleted, Is.EqualTo(3));
        Assert.That(log.RepsCompleted, Is.EqualTo(10));
        Assert.That(log.PainLevel, Is.EqualTo(2));
        Assert.That(log.Notes, Is.EqualTo("Felt good"));
    }

    // ── Completion Log Filtering ────────────────────────────────────────────────

    [Test]
    public async Task GetCompletionLogs_FiltersByPrescription()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string rxId1 = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Quad Sets", Frequency = "3x daily", BodyGroup = BodyGroup.Knee,
            Category = ExerciseCategory.Strengthening, PrescribedBy = "Dr. Smith"
        });
        string rxId2 = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Hamstring Curl", Frequency = "2x daily", BodyGroup = BodyGroup.Knee,
            Category = ExerciseCategory.Strengthening, PrescribedBy = "Dr. Smith"
        });

        await workflow.LogHepCompletionAsync(new HepCompletionLog
        { PrescriptionId = rxId1, CompletedBy = "Patient", CompletedDate = DateTime.UtcNow });
        await workflow.LogHepCompletionAsync(new HepCompletionLog
        { PrescriptionId = rxId2, CompletedBy = "Patient", CompletedDate = DateTime.UtcNow });
        await workflow.LogHepCompletionAsync(new HepCompletionLog
        { PrescriptionId = rxId1, CompletedBy = "Patient", CompletedDate = DateTime.UtcNow });

        List<HepCompletionLog> rx1Logs = await workflow.GetHepCompletionLogsAsync(rxId1, null, null);
        Assert.That(rx1Logs, Has.Count.EqualTo(2));

        List<HepCompletionLog> rx2Logs = await workflow.GetHepCompletionLogsAsync(rxId2, null, null);
        Assert.That(rx2Logs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetCompletionLogs_FiltersByDateRange()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string rxId = await workflow.AddHepPrescriptionAsync(new HepPrescription
        {
            ExerciseName = "Quad Sets", Frequency = "daily", BodyGroup = BodyGroup.Knee,
            Category = ExerciseCategory.Strengthening, PrescribedBy = "Dr. Smith"
        });

        await workflow.LogHepCompletionAsync(new HepCompletionLog
        {
            PrescriptionId = rxId, CompletedBy = "Patient",
            CompletedDate = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
        });
        await workflow.LogHepCompletionAsync(new HepCompletionLog
        {
            PrescriptionId = rxId, CompletedBy = "Patient",
            CompletedDate = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc)
        });
        await workflow.LogHepCompletionAsync(new HepCompletionLog
        {
            PrescriptionId = rxId, CompletedBy = "Patient",
            CompletedDate = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
        });

        List<HepCompletionLog> aprilLogs = await workflow.GetHepCompletionLogsAsync(
            null,
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(aprilLogs, Has.Count.EqualTo(1));
        Assert.That(aprilLogs[0].CompletedDate.Month, Is.EqualTo(4));
    }
}
