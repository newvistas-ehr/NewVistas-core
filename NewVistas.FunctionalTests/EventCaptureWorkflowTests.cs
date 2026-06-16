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
/// Functional tests for Event Capture — VistA Files #721, #724.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class EventCaptureWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Encounter creation ────────────────────────────────────────────────────

    [Test]
    public async Task CreateEncounter_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            new DateTime(2024, 7, 15, 9, 0, 0),
            "DSS-101", "Primary Care", "PC",
            "CLINIC-A", "General Medicine Clinic",
            "LOC-01", "Building 1 Room 200",
            "PROV-001", "Dr. Smith",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            "301", null, "Annual wellness visit");

        Assert.That(encounterId, Does.StartWith("EC-ENCOUNTER:"));

        List<EventCaptureIndexEntry> entries = await wf.GetEventCaptureEncountersAsync(10);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].EncounterId, Is.EqualTo(encounterId));
        Assert.That(entries[0].DssUnitName, Is.EqualTo("Primary Care"));
        Assert.That(entries[0].Status, Is.EqualTo(EcEncounterStatus.Open));
    }

    [Test]
    public async Task GetEncounter_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow,
            "DSS-200", "Mental Health", "MH",
            null, null, null, null,
            "PROV-002", "Dr. Jones",
            "PROV-003", "Dr. Attending",
            EcEncounterType.Telephone,
            EcPatientCategory.NonServiceConnected,
            "502", "510", "Telephone follow-up");

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.DssUnitId, Is.EqualTo("DSS-200"));
        Assert.That(state.DssUnitName, Is.EqualTo("Mental Health"));
        Assert.That(state.EncounterType, Is.EqualTo(EcEncounterType.Telephone));
        Assert.That(state.PatientCategory, Is.EqualTo(EcPatientCategory.NonServiceConnected));
        Assert.That(state.AttendingProviderName, Is.EqualTo("Dr. Attending"));
        Assert.That(state.CreditStopCode, Is.EqualTo("510"));
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Open));
    }

    // ── Procedures ────────────────────────────────────────────────────────────

    [Test]
    public async Task AddProcedure_AppearsOnEncounter()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-300", "Surgery", null,
            null, null, null, null,
            "PROV-010", "Dr. Surgeon",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            null, null, null);

        await wf.AddEcProcedureAsync(
            encounterId, "99213", "Office visit level 3", 1,
            "PROV-010", "Dr. Surgeon", null);

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Procedures, Has.Count.EqualTo(1));
        Assert.That(state.Procedures[0].CptCode, Is.EqualTo("99213"));
        Assert.That(state.Procedures[0].ProcedureDescription, Is.EqualTo("Office visit level 3"));
        Assert.That(state.Procedures[0].Quantity, Is.EqualTo(1));
    }

    [Test]
    public async Task AddMultipleProcedures_AllAppearOnEncounter()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-400", "Rehab", null,
            null, null, null, null,
            "PROV-020", "Dr. Rehab",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            null, null, null);

        await wf.AddEcProcedureAsync(encounterId, "97110", "Therapeutic exercises", 2,
            "PROV-020", "Dr. Rehab", null);
        await wf.AddEcProcedureAsync(encounterId, "97140", "Manual therapy", 1,
            "PROV-020", "Dr. Rehab", "-59");

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Procedures, Has.Count.EqualTo(2));
        Assert.That(state.Procedures[1].ModifierCode, Is.EqualTo("-59"));
    }

    // ── Diagnoses ─────────────────────────────────────────────────────────────

    [Test]
    public async Task AddDiagnosis_AppearsOnEncounter()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-500", "Cardiology", null,
            null, null, null, null,
            "PROV-030", "Dr. Heart",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.Medicare,
            null, null, null);

        await wf.AddEcDiagnosisAsync(encounterId, "I10", "Essential hypertension", true);
        await wf.AddEcDiagnosisAsync(encounterId, "E11.9", "Type 2 diabetes mellitus", false);

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Diagnoses, Has.Count.EqualTo(2));
        Assert.That(state.Diagnoses[0].Icd10Code, Is.EqualTo("I10"));
        Assert.That(state.Diagnoses[0].IsPrimary, Is.True);
        Assert.That(state.Diagnoses[1].IsPrimary, Is.False);
    }

    // ── Completion ────────────────────────────────────────────────────────────

    [Test]
    public async Task CompleteEncounter_SetsStatusComplete()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            new DateTime(2024, 8, 1, 8, 30, 0),
            "DSS-600", "Lab", null,
            null, null, null, null,
            "PROV-040", "Dr. Lab",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            null, null, null);

        await wf.AddEcProcedureAsync(encounterId, "36415", "Venipuncture", 1,
            "PROV-040", "Dr. Lab", null);

        await wf.CompleteEventCaptureEncounterAsync(
            encounterId,
            new DateTime(2024, 8, 1, 9, 0, 0),
            30);

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Complete));
        Assert.That(state.CheckOutDateTime, Is.Not.Null);
        Assert.That(state.VisitLengthMinutes, Is.EqualTo(30));

        List<EventCaptureIndexEntry> entries = await wf.GetEventCaptureEncountersAsync(10);
        Assert.That(entries[0].Status, Is.EqualTo(EcEncounterStatus.Complete));
    }

    // ── Deletion ──────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteEncounter_SetsStatusDeleted()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-700", "Nursing", null,
            null, null, null, null,
            "PROV-050", "Nurse Jones",
            null, null,
            EcEncounterType.Inpatient,
            EcPatientCategory.ActiveDuty,
            null, null, null);

        await wf.DeleteEventCaptureEncounterAsync(
            encounterId, "PROV-099", "Admin Smith", "Entered in error");

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Deleted));
        Assert.That(state.DeletedByProviderName, Is.EqualTo("Admin Smith"));
        Assert.That(state.DeleteReason, Is.EqualTo("Entered in error"));
        Assert.That(state.DeletedDate, Is.Not.Null);
    }

    // ── Procedure count in index ──────────────────────────────────────────────

    [Test]
    public async Task AddProcedure_UpdatesProcedureCountInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-800", "Optometry", null,
            null, null, null, null,
            "PROV-060", "Dr. Eyes",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            null, null, null);

        await wf.AddEcProcedureAsync(encounterId, "92004", "Comprehensive eye exam", 1,
            "PROV-060", "Dr. Eyes", null);
        await wf.AddEcProcedureAsync(encounterId, "92083", "Visual field exam", 1,
            "PROV-060", "Dr. Eyes", null);

        List<EventCaptureIndexEntry> entries = await wf.GetEventCaptureEncountersAsync(10);
        Assert.That(entries[0].ProcedureCount, Is.EqualTo(2));
    }

    // ── Multiple encounters per patient ───────────────────────────────────────

    [Test]
    public async Task MultipleEncounters_AllAppearInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.CreateEventCaptureEncounterAsync(
            new DateTime(2024, 1, 10), "DSS-901", "Unit A", null,
            null, null, null, null,
            "PROV-070", "Dr. A", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.ServiceConnected,
            null, null, null);

        await wf.CreateEventCaptureEncounterAsync(
            new DateTime(2024, 2, 15), "DSS-902", "Unit B", null,
            null, null, null, null,
            "PROV-071", "Dr. B", null, null,
            EcEncounterType.Telephone, EcPatientCategory.NonServiceConnected,
            null, null, null);

        await wf.CreateEventCaptureEncounterAsync(
            new DateTime(2024, 3, 20), "DSS-903", "Unit C", null,
            null, null, null, null,
            "PROV-072", "Dr. C", null, null,
            EcEncounterType.Daycase, EcPatientCategory.Medicare,
            null, null, null);

        List<EventCaptureIndexEntry> entries = await wf.GetEventCaptureEncountersAsync(10);
        Assert.That(entries, Has.Count.EqualTo(3));
    }

    // ── Independent patients ──────────────────────────────────────────────────

    [Test]
    public async Task DifferentPatients_HaveIndependentEncounters()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-100", "Unit X", null,
            null, null, null, null,
            "PROV-080", "Dr. X", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.ServiceConnected,
            null, null, null);

        await wf2.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-100", "Unit X", null,
            null, null, null, null,
            "PROV-081", "Dr. Y", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.ServiceConnected,
            null, null, null);
        await wf2.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow, "DSS-100", "Unit X", null,
            null, null, null, null,
            "PROV-082", "Dr. Z", null, null,
            EcEncounterType.Inpatient, EcPatientCategory.ActiveDuty,
            null, null, null);

        List<EventCaptureIndexEntry> p1Entries = await wf1.GetEventCaptureEncountersAsync(10);
        List<EventCaptureIndexEntry> p2Entries = await wf2.GetEventCaptureEncountersAsync(10);

        Assert.That(p1Entries, Has.Count.EqualTo(1));
        Assert.That(p2Entries, Has.Count.EqualTo(2));
    }

    // ── Full workflow: create, add procedures/diagnoses, complete ──────────────

    [Test]
    public async Task FullWorkflow_CreateAddProcsDiagsComplete()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string encounterId = await wf.CreateEventCaptureEncounterAsync(
            new DateTime(2024, 9, 1, 10, 0, 0),
            "DSS-999", "Emergency", "ER",
            "CLINIC-ER", "Emergency Department",
            "LOC-ER", "ER Bay 3",
            "PROV-090", "Dr. Emergency",
            "PROV-091", "Dr. Attending ER",
            EcEncounterType.Outpatient,
            EcPatientCategory.Humanitarian,
            "430", null, "Trauma evaluation");

        await wf.AddEcProcedureAsync(encounterId, "99283", "ER visit level 3", 1,
            "PROV-090", "Dr. Emergency", null);
        await wf.AddEcProcedureAsync(encounterId, "12001", "Wound repair", 1,
            "PROV-090", "Dr. Emergency", null);

        await wf.AddEcDiagnosisAsync(encounterId, "S61.001A", "Laceration of right hand", true);
        await wf.AddEcDiagnosisAsync(encounterId, "W26.0XXA", "Contact with kitchen knife", false);

        await wf.CompleteEventCaptureEncounterAsync(
            encounterId,
            new DateTime(2024, 9, 1, 11, 30, 0),
            90);

        EventCaptureEncounterState state = await wf.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Complete));
        Assert.That(state.Procedures, Has.Count.EqualTo(2));
        Assert.That(state.Diagnoses, Has.Count.EqualTo(2));
        Assert.That(state.VisitLengthMinutes, Is.EqualTo(90));
        Assert.That(state.Comments, Is.EqualTo("Trauma evaluation"));
    }
}
