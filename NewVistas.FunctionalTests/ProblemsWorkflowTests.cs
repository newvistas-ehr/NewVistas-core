// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Problem List — GMPLSAVE.m / GMPLEDIT.m.
/// File #9000011 (PROBLEM LIST).
/// Problems are now embedded on the patient grain as ProblemEntry.
/// </summary>
[TestFixture]
public class ProblemsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    // ─── ID / Creation ────────────────────────────────────────────────────

    [Test]
    public async Task AddProblem_ReturnsIdWithProbPrefix()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.AddProblemAsync(
            "Type 2 Diabetes Mellitus", "E11.9", "CHRONIC", "CHRONIC",
            null, null, null, null, null, false, null);

        Assert.That(id, Does.StartWith("PROB-"));
    }

    [Test]
    public async Task AddProblem_StatusIsActiveByDefault()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await w.AddProblemAsync(
            "Hypertension", "I10", null, null,
            null, null, null, null, null, false, null);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Status, Is.EqualTo("ACTIVE"));
    }

    // ─── Get Active / All ─────────────────────────────────────────────────

    [Test]
    public async Task GetActiveProblems_ReturnsOnlyActiveProblems()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string activeId = await w.AddProblemAsync("GERD", "K21.0", null, null, null, null, null, null, null, false, null);
        string inactiveId = await w.AddProblemAsync("Acute Sinusitis", "J01.90", null, null, null, null, null, null, null, false, null);
        await w.InactivateProblemAsync(inactiveId, DateTime.UtcNow);

        List<ProblemSummary> active = await w.GetActiveProblemsAsync();
        Assert.That(active.All(p => p.Status == "ACTIVE"), Is.True);
        Assert.That(active.Any(p => p.ProblemId == activeId), Is.True);
        Assert.That(active.Any(p => p.ProblemId == inactiveId), Is.False);
    }

    [Test]
    public async Task GetAllProblems_ReturnsActiveAndInactive()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id1 = await w.AddProblemAsync("Hyperlipidemia", "E78.5", null, null, null, null, null, null, null, false, null);
        string id2 = await w.AddProblemAsync("Anxiety", "F41.9", null, null, null, null, null, null, null, false, null);
        await w.InactivateProblemAsync(id2, DateTime.UtcNow);

        List<ProblemSummary> all = await w.GetAllProblemsAsync();
        Assert.That(all, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(all.Any(p => p.ProblemId == id1), Is.True);
        Assert.That(all.Any(p => p.ProblemId == id2), Is.True);
    }

    // ─── Inactivate ───────────────────────────────────────────────────────

    [Test]
    public async Task InactivateProblem_ChangesStatusToInactive()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await w.AddProblemAsync("Acute Bronchitis", "J20.9", null, null, null, null, null, null, null, false, null);

        await w.InactivateProblemAsync(id, DateTime.UtcNow);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Status, Is.EqualTo("INACTIVE"));
    }

    [Test]
    public async Task InactivateProblem_SetsDateResolved()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await w.AddProblemAsync("URI", "J06.9", null, null, null, null, null, null, null, false, null);
        DateTime resolved = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        await w.InactivateProblemAsync(id, resolved);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DateResolved?.Date, Is.EqualTo(resolved.Date));
    }

    [Test]
    public async Task GetActiveProblems_AfterInactivation_ExcludesProblem()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        string id = await w.AddProblemAsync("Plantar Fasciitis", null, null, null, null, null, null, null, null, false, null);

        await w.InactivateProblemAsync(id, DateTime.UtcNow);

        List<ProblemSummary> active = await w.GetActiveProblemsAsync();
        Assert.That(active.Any(p => p.ProblemId == id), Is.False);
    }

    // ─── Field Persistence ────────────────────────────────────────────────

    [Test]
    public async Task AddProblem_WithDiagnosisCode_PreservesCode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await w.AddProblemAsync(
            "Atrial Fibrillation", "I48.91", null, null, null, null, null, null, null, false, null);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DiagnosisCode, Is.EqualTo("I48.91"));
    }

    [Test]
    public async Task AddProblem_ServiceConnectedFlag_Stored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await w.AddProblemAsync(
            "PTSD", "F43.10", null, null, null, null, null, null, null, true, null);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.IsServiceConnected, Is.True);
    }

    [Test]
    public async Task AddProblem_WithOnsetDate_DatePreserved()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        DateTime onset = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        string id = await w.AddProblemAsync(
            "Chronic Kidney Disease", "N18.3", null, null, onset, null, null, null, null, false, null);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DateOfOnset?.Date, Is.EqualTo(onset.Date));
    }

    [Test]
    public async Task AddProblem_WithCondition_ConditionOnEntry()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await w.AddProblemAsync(
            "Heart Failure", "I50.9", "CHRONIC", null, null, null, null, null, null, false, null);

        ProblemEntry? entry = await GetPatient(patientId).GetProblemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Condition, Is.EqualTo("CHRONIC"));
    }

    // ─── Patient Linkage ─────────────────────────────────────────────────

    [Test]
    public async Task AddProblem_LinksToPatientProblems()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.AddProblemAsync("Migraine", "G43.909", null, null, null, null, null, null, null, false, null);

        List<ProblemEntry> entries = await GetPatient(patientId).GetProblemsAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ProblemId, Does.StartWith("PROB-"));
    }

    [Test]
    public async Task AddMultipleProblems_AllAppearInGetAll()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id1 = await w.AddProblemAsync("Osteoarthritis", "M19.90", null, null, null, null, null, null, null, false, null);
        string id2 = await w.AddProblemAsync("Obesity", "E66.9", null, null, null, null, null, null, null, false, null);
        string id3 = await w.AddProblemAsync("Hypothyroidism", "E03.9", null, null, null, null, null, null, null, false, null);

        List<ProblemSummary> all = await w.GetAllProblemsAsync();
        Assert.That(all, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(all.Any(p => p.ProblemId == id1), Is.True);
        Assert.That(all.Any(p => p.ProblemId == id2), Is.True);
        Assert.That(all.Any(p => p.ProblemId == id3), Is.True);
    }

    [Test]
    public async Task GetActiveProblems_Empty_ReturnsEmpty()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<ProblemSummary> active = await w.GetActiveProblemsAsync();

        Assert.That(active, Is.Empty);
    }

    [Test]
    public async Task MultiplePatients_ProblemsAreIndependent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await w1.AddProblemAsync("Asthma", "J45.909", null, null, null, null, null, null, null, false, null);

        List<ProblemSummary> problems2 = await w2.GetActiveProblemsAsync();
        Assert.That(problems2, Is.Empty);
    }

    // ─── Full Workflow ────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_AddInactivateAdd_ActiveCountIsOne()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        // Add first problem and resolve it
        string id1 = await w.AddProblemAsync("Pneumonia", "J18.9", "ACUTE", null, null, null, null, null, null, false, null);
        await w.InactivateProblemAsync(id1, DateTime.UtcNow);

        // Add a second, ongoing problem
        string id2 = await w.AddProblemAsync("Hypertension", "I10", "CHRONIC", null, null, null, null, null, null, false, null);

        List<ProblemSummary> active = await w.GetActiveProblemsAsync();
        List<ProblemSummary> all = await w.GetAllProblemsAsync();

        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ProblemId, Is.EqualTo(id2));
        Assert.That(all, Has.Count.EqualTo(2));
    }
}
