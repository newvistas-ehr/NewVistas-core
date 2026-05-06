// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Clinical Reminders — VistA File #811.9.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class ClinicalRemindersWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Create reminder ────────────────────────────────────────────────────────

    [Test]
    public async Task CreateReminder_ReturnsNonEmptyId()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reminderId = await wf.CreateReminderAsync(
            "Influenza Vaccine",
            "VA-FLU-2024",
            "IMMUNIZATION",
            "HIGH",
            "YEARLY",
            new DateTime(2024, 10, 1));

        // Assert
        Assert.That(reminderId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task CreateReminder_ReminderNameStored()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reminderId = await wf.CreateReminderAsync(
            "Colorectal Cancer Screening",
            "VA-CRC-001",
            "SCREENING",
            "NORMAL",
            "10 YEARS",
            new DateTime(2025, 3, 15));

        List<ReminderSummary> reminders = await wf.GetRemindersAsync();

        // Assert
        Assert.That(reminders, Has.Count.EqualTo(1));
        Assert.That(reminders[0].ReminderName, Is.EqualTo("Colorectal Cancer Screening"));
    }

    [Test]
    public async Task CreateReminder_DueDateStored()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        DateTime dueDate = new DateTime(2025, 6, 1);

        // Act
        string reminderId = await wf.CreateReminderAsync(
            "HbA1c Lab",
            "VA-HBA1C",
            "LAB",
            "HIGH",
            "6 MONTHS",
            dueDate);

        List<ReminderSummary> reminders = await wf.GetRemindersAsync();

        // Assert
        Assert.That(reminders, Has.Count.EqualTo(1));
        Assert.That(reminders[0].DueDate, Is.EqualTo(dueDate));
    }

    [Test]
    public async Task GetReminders_ReturnsEmptyByDefault()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        List<ReminderSummary> reminders = await wf.GetRemindersAsync();

        // Assert
        Assert.That(reminders, Is.Empty);
    }

    [Test]
    public async Task CreateReminder_AppearsInList()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reminderId = await wf.CreateReminderAsync(
            "Breast Cancer Screening",
            "VA-MAMMOGRAM",
            "SCREENING",
            "NORMAL",
            "2 YEARS",
            new DateTime(2025, 9, 1));

        List<ReminderSummary> reminders = await wf.GetRemindersAsync();

        // Assert
        Assert.That(reminders, Has.Count.EqualTo(1));
        Assert.That(reminders[0].ReminderId, Is.EqualTo(reminderId));
    }

    [Test]
    public async Task CompleteReminder_UpdatesStatus()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string reminderId = await wf.CreateReminderAsync(
            "Annual Physical",
            "VA-ANNUAL-PHYS",
            "EXAM",
            "NORMAL",
            "YEARLY",
            DateTime.UtcNow);

        // Act
        await wf.CompleteReminderAsync(reminderId, "PROV-001", "Dr. Smith");

        List<ReminderSummary> reminders = await wf.GetRemindersAsync();

        // Assert
        Assert.That(reminders, Has.Count.EqualTo(1));
        Assert.That(reminders[0].Status, Is.EqualTo("DONE"));
    }

    [Test]
    public async Task MultipleReminders_AllAppearInList()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        await wf.CreateReminderAsync(
            "Flu Shot",
            "VA-FLU",
            "IMMUNIZATION",
            "HIGH",
            "YEARLY",
            new DateTime(2024, 10, 1));

        await wf.CreateReminderAsync(
            "Lipid Panel",
            "VA-LIPID",
            "LAB",
            "NORMAL",
            "YEARLY",
            new DateTime(2025, 1, 15));

        await wf.CreateReminderAsync(
            "Depression Screening",
            "VA-PHQ9",
            "SCREENING",
            "HIGH",
            "YEARLY",
            new DateTime(2025, 4, 1));

        List<ReminderSummary> reminders = await wf.GetRemindersAsync();

        // Assert
        Assert.That(reminders, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task FullWorkflow_CreateAndComplete()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act — create
        string reminderId = await wf.CreateReminderAsync(
            "Pneumococcal Vaccine",
            "VA-PNEUMO",
            "IMMUNIZATION",
            "HIGH",
            "ONE TIME",
            new DateTime(2024, 12, 1));

        List<ReminderSummary> beforeComplete = await wf.GetRemindersAsync();
        Assert.That(beforeComplete[0].Status, Is.EqualTo("DUE"));

        // Act — complete
        await wf.CompleteReminderAsync(reminderId, "PROV-002", "Dr. Johnson");

        List<ReminderSummary> afterComplete = await wf.GetRemindersAsync();

        // Assert
        Assert.That(afterComplete, Has.Count.EqualTo(1));
        Assert.That(afterComplete[0].ReminderId, Is.EqualTo(reminderId));
        Assert.That(afterComplete[0].Status, Is.EqualTo("DONE"));
    }

    [Test]
    public async Task CreateReminder_IndependentPatients()
    {
        // Arrange
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        // Act
        await wf1.CreateReminderAsync(
            "Flu Shot", "VA-FLU", "IMMUNIZATION", "HIGH", "YEARLY", DateTime.UtcNow);

        await wf2.CreateReminderAsync(
            "HbA1c", "VA-HBA1C", "LAB", "HIGH", "6 MONTHS", DateTime.UtcNow);

        await wf2.CreateReminderAsync(
            "Eye Exam", "VA-EYE", "EXAM", "NORMAL", "YEARLY", DateTime.UtcNow);

        List<ReminderSummary> p1Reminders = await wf1.GetRemindersAsync();
        List<ReminderSummary> p2Reminders = await wf2.GetRemindersAsync();

        // Assert
        Assert.That(p1Reminders, Has.Count.EqualTo(1));
        Assert.That(p2Reminders, Has.Count.EqualTo(2));
    }
}
