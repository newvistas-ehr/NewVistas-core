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
/// Functional tests for Nursing Task Worklist — aggregated task view
/// combining MAR, vitals, care plan interventions, and ad-hoc tasks.
/// </summary>
[TestFixture]
public class NursingTaskWorklistWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private INursingTaskWorklistGrain NewWorklist()
        => _cluster.GrainFactory.GetGrain<INursingTaskWorklistGrain>($"NURS-TASKS:{Guid.NewGuid()}");

    private IPatientWorkflowGrain WF(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Grain Level ─────────────────────────────────────────────────────────

    [Test]
    public async Task AddTask_AppearsInWorklist()
    {
        INursingTaskWorklistGrain wl = NewWorklist();
        await wl.AddTaskAsync(new NursingTask
        {
            TaskId = "T-001", PatientId = "P1", Category = NursingTaskCategory.VitalSigns,
            Priority = NursingTaskPriority.Routine, Status = NursingTaskStatus.Due,
            Description = "Record vital signs", DueDateTime = DateTime.UtcNow,
        });
        NursingTaskWorklistState state = await wl.GetAsync();
        Assert.That(state.Tasks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CompleteTask_SetsCompletedStatus()
    {
        INursingTaskWorklistGrain wl = NewWorklist();
        await wl.AddTaskAsync(new NursingTask
        {
            TaskId = "T-002", PatientId = "P2", Category = NursingTaskCategory.Medication,
            Priority = NursingTaskPriority.STAT, Status = NursingTaskStatus.Due,
            Description = "Administer morphine 4mg IV", DueDateTime = DateTime.UtcNow,
        });
        await wl.CompleteTaskAsync("T-002", "RN-1", "Nurse Smith", "Given without incident");
        NursingTaskWorklistState state = await wl.GetAsync();
        Assert.That(state.Tasks[0].Status, Is.EqualTo(NursingTaskStatus.Completed));
        Assert.That(state.Tasks[0].CompletedByNurseName, Is.EqualTo("Nurse Smith"));
    }

    [Test]
    public async Task DeferTask_SetsDeferredStatus()
    {
        INursingTaskWorklistGrain wl = NewWorklist();
        await wl.AddTaskAsync(new NursingTask
        {
            TaskId = "T-003", PatientId = "P3", Category = NursingTaskCategory.Assessment,
            Priority = NursingTaskPriority.Routine, Status = NursingTaskStatus.Due,
            Description = "Wound assessment", DueDateTime = DateTime.UtcNow,
        });
        await wl.DeferTaskAsync("T-003", "Patient sleeping — will reassess in 1 hour");
        NursingTaskWorklistState state = await wl.GetAsync();
        Assert.That(state.Tasks[0].Status, Is.EqualTo(NursingTaskStatus.Deferred));
    }

    [Test]
    public async Task GetDueTasks_FiltersCorrectly()
    {
        INursingTaskWorklistGrain wl = NewWorklist();
        await wl.AddTaskAsync(new NursingTask { TaskId = "DUE-1", PatientId = "P4", Category = NursingTaskCategory.VitalSigns, Status = NursingTaskStatus.Due, Description = "Vitals", DueDateTime = DateTime.UtcNow });
        await wl.AddTaskAsync(new NursingTask { TaskId = "DONE-1", PatientId = "P4", Category = NursingTaskCategory.Medication, Status = NursingTaskStatus.Completed, Description = "Med given", DueDateTime = DateTime.UtcNow });
        await wl.AddTaskAsync(new NursingTask { TaskId = "OD-1", PatientId = "P4", Category = NursingTaskCategory.Intervention, Status = NursingTaskStatus.Overdue, Description = "Reposition", DueDateTime = DateTime.UtcNow.AddMinutes(-30) });

        List<NursingTask> due = await wl.GetDueTasksAsync();
        Assert.That(due, Has.Count.EqualTo(2)); // Due + Overdue
    }

    [Test]
    public async Task GetTasksByCategory_FiltersCorrectly()
    {
        INursingTaskWorklistGrain wl = NewWorklist();
        await wl.AddTaskAsync(new NursingTask { TaskId = "M1", PatientId = "P5", Category = NursingTaskCategory.Medication, Status = NursingTaskStatus.Due, Description = "Med1", DueDateTime = DateTime.UtcNow });
        await wl.AddTaskAsync(new NursingTask { TaskId = "V1", PatientId = "P5", Category = NursingTaskCategory.VitalSigns, Status = NursingTaskStatus.Due, Description = "Vitals", DueDateTime = DateTime.UtcNow });
        await wl.AddTaskAsync(new NursingTask { TaskId = "M2", PatientId = "P5", Category = NursingTaskCategory.Medication, Status = NursingTaskStatus.Due, Description = "Med2", DueDateTime = DateTime.UtcNow });

        List<NursingTask> meds = await wl.GetTasksByCategoryAsync(NursingTaskCategory.Medication);
        Assert.That(meds, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RefreshAsync_ReplacesActiveTasks()
    {
        INursingTaskWorklistGrain wl = NewWorklist();
        // Add a completed task that should survive refresh
        await wl.AddTaskAsync(new NursingTask { TaskId = "OLD-DONE", PatientId = "P6", Category = NursingTaskCategory.VitalSigns, Status = NursingTaskStatus.Completed, Description = "Done", DueDateTime = DateTime.UtcNow });

        List<NursingTask> newVitals = new() { new() { TaskId = "NEW-VS", PatientId = "P6", Category = NursingTaskCategory.VitalSigns, Status = NursingTaskStatus.Due, Description = "New vitals", DueDateTime = DateTime.UtcNow } };
        await wl.RefreshAsync(new List<NursingTask>(), newVitals, new List<NursingTask>(), new List<NursingTask>());

        NursingTaskWorklistState state = await wl.GetAsync();
        Assert.That(state.Tasks.Any(t => t.TaskId == "OLD-DONE"), Is.True); // completed preserved
        Assert.That(state.Tasks.Any(t => t.TaskId == "NEW-VS"), Is.True);   // new task added
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_RefreshWorklist_GeneratesTasks()
    {
        string pid = $"PAT-NTW-{Guid.NewGuid()}";
        NursingTaskWorklistState wl = await WF(pid).RefreshNursingTaskWorklistAsync();
        // Should always have at least a vital signs task
        Assert.That(wl.Tasks, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(wl.Tasks.Any(t => t.Category == NursingTaskCategory.VitalSigns), Is.True);
    }

    [Test]
    public async Task Workflow_RefreshWorklist_IncludesCarePlanInterventions()
    {
        string pid = $"PAT-NTW2-{Guid.NewGuid()}";
        // Add a care plan with active intervention
        string probId = await WF(pid).AddNursingDiagnosisAsync("Impaired Skin Integrity", "pressure", null, null, null, null);
        await WF(pid).AddCarePlanInterventionAsync(probId, "Reposition patient every 2 hours", "Q2H", null, null);

        NursingTaskWorklistState wl = await WF(pid).RefreshNursingTaskWorklistAsync();
        Assert.That(wl.Tasks.Any(t => t.Category == NursingTaskCategory.Intervention), Is.True);
        Assert.That(wl.Tasks.Any(t => t.Description.Contains("Reposition")), Is.True);
    }

    [Test]
    public async Task Workflow_CompleteTask_MarksCompleted()
    {
        string pid = $"PAT-NTW3-{Guid.NewGuid()}";
        await WF(pid).RefreshNursingTaskWorklistAsync();
        NursingTaskWorklistState wl = await WF(pid).GetNursingTaskWorklistAsync();
        string taskId = wl.Tasks.First(t => t.Status == NursingTaskStatus.Due).TaskId;

        await WF(pid).CompleteNursingTaskAsync(taskId, "RN-1", "Nurse Smith", "Completed");
        wl = await WF(pid).GetNursingTaskWorklistAsync();
        Assert.That(wl.Tasks.First(t => t.TaskId == taskId).Status, Is.EqualTo(NursingTaskStatus.Completed));
    }

    [Test]
    public async Task Workflow_DeferTask_MarksDeferred()
    {
        string pid = $"PAT-NTW4-{Guid.NewGuid()}";
        await WF(pid).RefreshNursingTaskWorklistAsync();
        NursingTaskWorklistState wl = await WF(pid).GetNursingTaskWorklistAsync();
        string taskId = wl.Tasks.First(t => t.Status == NursingTaskStatus.Due).TaskId;

        await WF(pid).DeferNursingTaskAsync(taskId, "Patient in procedure");
        wl = await WF(pid).GetNursingTaskWorklistAsync();
        Assert.That(wl.Tasks.First(t => t.TaskId == taskId).Status, Is.EqualTo(NursingTaskStatus.Deferred));
    }

    [Test]
    public async Task Workflow_AddAdhocTask_AppearsInWorklist()
    {
        string pid = $"PAT-NTW5-{Guid.NewGuid()}";
        await WF(pid).AddNursingTaskAsync(
            NursingTaskCategory.PatientEducation, NursingTaskPriority.Routine,
            "Teach patient wound care for discharge", DateTime.UtcNow.AddHours(2), null, null);

        NursingTaskWorklistState wl = await WF(pid).GetNursingTaskWorklistAsync();
        Assert.That(wl.Tasks.Any(t => t.Category == NursingTaskCategory.PatientEducation), Is.True);
    }

    [Test]
    public async Task Workflow_GetDueTasks_ReturnsOnlyDue()
    {
        string pid = $"PAT-NTW6-{Guid.NewGuid()}";
        await WF(pid).RefreshNursingTaskWorklistAsync();
        List<NursingTask> due = await WF(pid).GetDueNursingTasksAsync();
        Assert.That(due.All(t => t.Status == NursingTaskStatus.Due || t.Status == NursingTaskStatus.Overdue), Is.True);
    }

    [Test]
    public async Task Workflow_STATTask_HasStatPriority()
    {
        string pid = $"PAT-NTW7-{Guid.NewGuid()}";
        await WF(pid).AddNursingTaskAsync(
            NursingTaskCategory.Lab, NursingTaskPriority.STAT,
            "STAT blood draw — CBC, BMP, Troponin", DateTime.UtcNow, null, "ORDER");

        NursingTaskWorklistState wl = await WF(pid).GetNursingTaskWorklistAsync();
        NursingTask stat = wl.Tasks.First(t => t.Priority == NursingTaskPriority.STAT);
        Assert.That(stat.Description, Does.Contain("Troponin"));
    }
}
