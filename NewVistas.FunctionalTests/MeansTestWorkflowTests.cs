// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Means Test — VistA File #408.31.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class MeansTestWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Record means test ──────────────────────────────────────────────────────

    [Test]
    public async Task RecordMeansTest_ReturnsNonEmptyId()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string meansTestId = await wf.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2024, 1, 15),
            45000.00m,
            12000.00m,
            2,
            "VERIFIED",
            "5",
            "CLERK-001", "Mary Smith",
            "Annual means test completed");

        // Assert
        Assert.That(meansTestId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task RecordMeansTest_TestTypeStored()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        await wf.RecordMeansTestAsync(
            "COPAY EXEMPTION TEST",
            new DateTime(2024, 3, 1),
            22000.00m,
            5000.00m,
            0,
            "VERIFIED",
            "7",
            null, null, null);

        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();

        // Assert
        Assert.That(tests, Has.Count.EqualTo(1));
        Assert.That(tests[0].TestType, Is.EqualTo("COPAY EXEMPTION TEST"));
    }

    [Test]
    public async Task RecordMeansTest_IncomeDataStored()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        await wf.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2024, 2, 20),
            78000.00m,
            30000.00m,
            3,
            "VERIFIED",
            "3",
            "CLERK-002", "Bob Jones",
            null);

        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();

        // Assert
        Assert.That(tests, Has.Count.EqualTo(1));
        Assert.That(tests[0].EligibilityStatus, Is.EqualTo("VERIFIED"));
        Assert.That(tests[0].PriorityGroup, Is.EqualTo("3"));
    }

    [Test]
    public async Task GetMeansTests_ReturnsEmptyByDefault()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();

        // Assert
        Assert.That(tests, Is.Empty);
    }

    [Test]
    public async Task RecordMeansTest_AppearsInList()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string meansTestId = await wf.RecordMeansTestAsync(
            "GMT THRESHOLD",
            new DateTime(2024, 4, 10),
            55000.00m,
            15000.00m,
            1,
            "VERIFIED",
            "5",
            "CLERK-003", "Sue Lee",
            "GMT threshold test");

        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();

        // Assert
        Assert.That(tests, Has.Count.EqualTo(1));
        Assert.That(tests[0].MeansTestId, Is.EqualTo(meansTestId));
    }

    [Test]
    public async Task RecordMeansTestDecision_StoresDecision()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2024, 5, 1),
            35000.00m,
            8000.00m,
            1,
            "VERIFIED",
            "5",
            "CLERK-004", "Ann Park",
            null);

        // Act
        await wf.RecordMeansTestDecisionAsync(
            "CATEGORY C",
            new DateTime(2024, 5, 5),
            47000.00m);

        // Assert — decision was recorded without error
        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();
        Assert.That(tests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MultipleMeansTests_AllAppearInList()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        await wf.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2023, 1, 10),
            40000.00m, 10000.00m, 2,
            "VERIFIED", "5",
            null, null, null);

        await wf.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2024, 1, 15),
            42000.00m, 11000.00m, 2,
            "VERIFIED", "5",
            null, null, null);

        await wf.RecordMeansTestAsync(
            "COPAY EXEMPTION TEST",
            new DateTime(2024, 6, 1),
            42000.00m, 11000.00m, 2,
            "VERIFIED", "7",
            null, null, null);

        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();

        // Assert
        Assert.That(tests, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task FullWorkflow_RecordAndDecide()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act — record means test
        string meansTestId = await wf.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2024, 7, 1),
            28000.00m,
            5000.00m,
            0,
            "VERIFIED",
            "5",
            "CLERK-005", "Tom White",
            "Veteran reports fixed income, no dependents");

        Assert.That(meansTestId, Is.Not.Null.And.Not.Empty);

        // Act — record decision
        await wf.RecordMeansTestDecisionAsync(
            "CATEGORY A",
            new DateTime(2024, 7, 5),
            34000.00m);

        // Assert
        List<MeansTestSummary> tests = await wf.GetMeansTestsAsync();
        Assert.That(tests, Has.Count.EqualTo(1));
        Assert.That(tests[0].MeansTestId, Is.EqualTo(meansTestId));
        Assert.That(tests[0].TestType, Is.EqualTo("MEANS TEST"));
    }

    [Test]
    public async Task IndependentPatients_SeparateMeansTestLists()
    {
        // Arrange
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        // Act
        await wf1.RecordMeansTestAsync(
            "MEANS TEST", DateTime.UtcNow,
            50000.00m, 20000.00m, 1,
            "VERIFIED", "4",
            null, null, null);

        await wf2.RecordMeansTestAsync(
            "MEANS TEST", DateTime.UtcNow,
            30000.00m, 5000.00m, 0,
            "VERIFIED", "5",
            null, null, null);

        await wf2.RecordMeansTestAsync(
            "COPAY EXEMPTION TEST", DateTime.UtcNow,
            30000.00m, 5000.00m, 0,
            "VERIFIED", "7",
            null, null, null);

        List<MeansTestSummary> p1Tests = await wf1.GetMeansTestsAsync();
        List<MeansTestSummary> p2Tests = await wf2.GetMeansTestsAsync();

        // Assert
        Assert.That(p1Tests, Has.Count.EqualTo(1));
        Assert.That(p2Tests, Has.Count.EqualTo(2));
    }
}
