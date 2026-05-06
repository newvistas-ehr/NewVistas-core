// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Depth functional tests for PCE (Patient Care Encounters) — VistA File #9000010.
/// Tests end-to-end encounter workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class PceDepthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── 1 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateEncounter_ReturnsNonEmptyId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        Assert.That(visitId, Is.Not.Null.And.Not.Empty);
    }

    // ── 2 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateEncounter_StoresServiceCategory()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.ServiceCategory, Is.EqualTo("A"));
    }

    // ── 3 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetEncounter_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime visitDateTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        string visitId = await wf.CreateEncounterAsync(
            visitDateTime, "A", "LOC-100", "Primary Care Clinic",
            "ESTABLISHED", "323", "PROV-100", "Dr. Williams",
            "APPT-100", "Annual checkup");

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.VisitDateTime, Is.EqualTo(visitDateTime));
        Assert.That(state.LocationName, Is.EqualTo("Primary Care Clinic"));
        Assert.That(state.PrimaryProviderName, Is.EqualTo("Dr. Williams"));
    }

    // ── 4 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetEncounterList_ReturnsEntries()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.CreateEncounterAsync(
            DateTime.UtcNow.AddDays(-2), "A", null, "Clinic A",
            null, null, null, null, null, null);
        await wf.CreateEncounterAsync(
            DateTime.UtcNow.AddDays(-1), "T", null, "Telehealth",
            null, null, null, null, null, null);

        List<PceVisitEntry> entries = await wf.GetEncounterListAsync(10);
        Assert.That(entries, Has.Count.GreaterThanOrEqualTo(2));
    }

    // ── 5 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CheckOutEncounter_SetsCheckedOutStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        DateTime checkOutTime = DateTime.UtcNow.AddHours(1);
        await wf.CheckOutEncounterAsync(visitId, checkOutTime);

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.Status, Is.EqualTo("CHECKED OUT"));
        Assert.That(state.CheckOutDateTime, Is.Not.Null);
    }

    // ── 6 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddEncounterDiagnosis_AppendsToDiagnosisList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        await wf.AddEncounterDiagnosisAsync(
            visitId, "I10", "Essential hypertension", true, "PROV-200", "Dr. Heart");

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.Diagnoses, Has.Count.EqualTo(1));
        Assert.That(state.Diagnoses[0].Icd10Code, Is.EqualTo("I10"));
        Assert.That(state.Diagnoses[0].Description, Is.EqualTo("Essential hypertension"));
        Assert.That(state.Diagnoses[0].IsPrimary, Is.True);
    }

    // ── 7 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddEncounterProcedure_AppendsToProcedureList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        await wf.AddEncounterProcedureAsync(
            visitId, "99213", "Office visit", 1, null, "PROV-300", "Dr. General");

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.Procedures, Has.Count.EqualTo(1));
        Assert.That(state.Procedures[0].CptCode, Is.EqualTo("99213"));
        Assert.That(state.Procedures[0].Description, Is.EqualTo("Office visit"));
        Assert.That(state.Procedures[0].Quantity, Is.EqualTo(1));
    }

    // ── 8 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task CancelEncounter_SetsCancelledStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        await wf.CancelEncounterAsync(visitId, "Patient no-show");

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient no-show"));
    }

    // ── 9 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task MultipleDiagnoses_AllAppear()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string visitId = await wf.CreateEncounterAsync(
            DateTime.UtcNow, "A", null, null, null, null,
            null, null, null, null);

        await wf.AddEncounterDiagnosisAsync(visitId, "I10", "Essential hypertension", true, null, null);
        await wf.AddEncounterDiagnosisAsync(visitId, "E11.9", "Type 2 diabetes mellitus", false, null, null);
        await wf.AddEncounterDiagnosisAsync(visitId, "J06.9", "Upper respiratory infection", false, null, null);

        VisitState state = await wf.GetEncounterAsync(visitId);
        Assert.That(state.Diagnoses, Has.Count.EqualTo(3));
    }

    // ── 10 ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task FullEncounterLifecycle_CreateDiagnoseProcedureCheckOut()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Create encounter
        DateTime visitDateTime = new DateTime(2025, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        string visitId = await wf.CreateEncounterAsync(
            visitDateTime, "A", "LOC-500", "Medicine Clinic",
            "NEW", "301", "PROV-500", "Dr. Primary",
            null, "New patient visit");

        Assert.That(visitId, Is.Not.Null.And.Not.Empty);

        // Verify initial state
        VisitState created = await wf.GetEncounterAsync(visitId);
        Assert.That(created.Status, Is.EqualTo("OPEN"));
        Assert.That(created.LocationName, Is.EqualTo("Medicine Clinic"));

        // Add diagnoses
        await wf.AddEncounterDiagnosisAsync(visitId, "Z00.00", "General adult medical exam", true, "PROV-500", "Dr. Primary");
        await wf.AddEncounterDiagnosisAsync(visitId, "I10", "Essential hypertension", false, "PROV-500", "Dr. Primary");

        // Add procedure
        await wf.AddEncounterProcedureAsync(visitId, "99203", "New patient office visit", 1, null, "PROV-500", "Dr. Primary");

        // Check out
        DateTime checkOutTime = visitDateTime.AddMinutes(45);
        await wf.CheckOutEncounterAsync(visitId, checkOutTime);

        // Verify final state
        VisitState finalState = await wf.GetEncounterAsync(visitId);
        Assert.That(finalState.Status, Is.EqualTo("CHECKED OUT"));
        Assert.That(finalState.CheckOutDateTime, Is.EqualTo(checkOutTime));
        Assert.That(finalState.Diagnoses, Has.Count.EqualTo(2));
        Assert.That(finalState.Procedures, Has.Count.EqualTo(1));
        Assert.That(finalState.Procedures[0].CptCode, Is.EqualTo("99203"));

        // Verify encounter appears in list
        List<PceVisitEntry> entries = await wf.GetEncounterListAsync(10);
        Assert.That(entries.Any(e => e.VisitId == visitId), Is.True);
        PceVisitEntry entry = entries.First(e => e.VisitId == visitId);
        Assert.That(entry.Status, Is.EqualTo("CHECKED OUT"));
        Assert.That(entry.DiagnosisCount, Is.EqualTo(2));
        Assert.That(entry.ProcedureCount, Is.EqualTo(1));
    }
}
