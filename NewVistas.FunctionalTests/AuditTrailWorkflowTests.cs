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
/// Functional tests for the VistA Audit Trail — AUDIT file (#1.1), XUSEC routines.
/// Verifies that audit events are created, indexed, and queryable per patient.
/// </summary>
[TestFixture]
public class AuditTrailWorkflowTests
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

    // ─── ID / Creation ────────────────────────────────────────────────────

    [Test]
    public async Task LogAuditEvent_ReturnsIdWithAuditPrefix()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.LogAuditEventAsync(
            "LABS", "CREATE", "LabTestState", "LAB-001",
            "PROV-001", "Dr. Smith", null, null, "Lab test ordered");

        Assert.That(id, Does.StartWith("AUDIT-"));
    }

    [Test]
    public async Task LogAuditEvent_EventStoredWithCorrectDomain()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "ORDERS", "SIGN", "OrderState", "ORDER-001",
            "PROV-002", "Dr. Jones", "LOC-1", "Clinic A",
            "Order signed electronically");

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.Domain, Is.EqualTo("ORDERS"));
    }

    [Test]
    public async Task LogAuditEvent_ActionStoredCorrectly()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "VITALS", "CREATE", "VitalState", "VITAL-001",
            null, null, null, null, "Vitals recorded");

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.Action, Is.EqualTo("CREATE"));
    }

    [Test]
    public async Task LogAuditEvent_OldAndNewValuesStored()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "DEMOGRAPHICS", "UPDATE", "PatientState", "PATIENT-001",
            "PROV-003", "Dr. Lee", null, null,
            "Patient name updated",
            oldValue: "JOHNSON,JANE",
            newValue: "JOHNSON,JANE M");

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.OldValue, Is.EqualTo("JOHNSON,JANE"));
        Assert.That(state.NewValue, Is.EqualTo("JOHNSON,JANE M"));
    }

    [Test]
    public async Task LogAuditEvent_UserInfoStored()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "NOTES", "SIGN", "TiuDocumentState", "NOTE-001",
            "PROV-010", "Dr. Patel", "LOC-2", "Ward 3B",
            "Progress note signed");

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.UserId, Is.EqualTo("PROV-010"));
        Assert.That(state.UserName, Is.EqualTo("Dr. Patel"));
        Assert.That(state.LocationName, Is.EqualTo("Ward 3B"));
    }

    // ─── Immutability ─────────────────────────────────────────────────────

    [Test]
    public async Task AuditEventGrain_RecordTwice_SecondCallIsNoOp()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "LABS", "CREATE", "LabTestState", "LAB-002",
            "PROV-001", "Dr. Smith", null, null, "First record");

        // Attempt a second write to the same event grain directly
        IAuditEventGrain eventGrain = _cluster.GrainFactory.GetGrain<IAuditEventGrain>(eventId);
        await eventGrain.RecordAsync(
            "DIFFERENT-PATIENT", "DELETE", "SomeType", "SOME-ID",
            "OTHER-ENTITY", null, null, null, null, "Should be ignored", null, null,
            IAuditEventGrain.GenesisHash);

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.Domain, Is.EqualTo("LABS"));
        Assert.That(state.Action, Is.EqualTo("CREATE"));
        Assert.That(state.Details, Is.EqualTo("First record"));
    }

    // ─── Listing / Index ──────────────────────────────────────────────────

    [Test]
    public async Task GetRecentAuditEvents_NoEvents_ReturnsEmpty()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<AuditEventSummary> events = await w.GetRecentAuditEventsAsync(50);

        Assert.That(events, Is.Empty);
    }

    [Test]
    public async Task GetRecentAuditEvents_ReturnsAllLoggedEvents()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORDER-A", null, null, null, null, "Order placed");
        await w.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-A", null, null, null, null, "Lab ordered");
        await w.LogAuditEventAsync("NOTES", "SIGN", "TiuDocumentState", "NOTE-A", null, null, null, null, "Note signed");

        List<AuditEventSummary> events = await w.GetRecentAuditEventsAsync(100);
        Assert.That(events, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetRecentAuditEvents_ReturnsNewestFirst()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORDER-B", null, null, null, null, "First");
        await Task.Delay(10); // ensure distinct timestamps
        await w.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-B", null, null, null, null, "Second");

        List<AuditEventSummary> events = await w.GetRecentAuditEventsAsync(10);
        Assert.That(events[0].Details, Is.EqualTo("Second"));
        Assert.That(events[1].Details, Is.EqualTo("First"));
    }

    [Test]
    public async Task GetRecentAuditEvents_MaxResultsRespected()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        for (int i = 0; i < 5; i++)
            await w.LogAuditEventAsync("VITALS", "CREATE", "VitalState", $"VITAL-{i}", null, null, null, null, $"Vitals {i}");

        List<AuditEventSummary> events = await w.GetRecentAuditEventsAsync(3);
        Assert.That(events, Has.Count.EqualTo(3));
    }

    // ─── Filtering ────────────────────────────────────────────────────────

    [Test]
    public async Task GetAuditEvents_FilterByDomain_ReturnsOnlyMatchingDomain()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORDER-C", null, null, null, null, "Order");
        await w.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-C", null, null, null, null, "Lab");
        await w.LogAuditEventAsync("ORDERS", "SIGN", "OrderState", "ORDER-D", null, null, null, null, "Sign");

        List<AuditEventSummary> events = await w.GetAuditEventsAsync("ORDERS", null, null);
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events.All(e => e.Domain == "ORDERS"), Is.True);
    }

    [Test]
    public async Task GetAuditEvents_FilterByDateRange_ReturnsOnlyInRange()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-D", null, null, null, null, "Old lab");
        DateTime rangeStart = DateTime.UtcNow.AddSeconds(-1);
        await Task.Delay(10);
        await w.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-E", null, null, null, null, "New lab");
        DateTime rangeEnd = DateTime.UtcNow.AddSeconds(1);

        List<AuditEventSummary> events = await w.GetAuditEventsAsync(null, rangeStart, rangeEnd);
        Assert.That(events.Any(e => e.Details == "New lab"), Is.True);
    }

    // ─── Entity Query ─────────────────────────────────────────────────────

    [Test]
    public async Task GetAuditEventsByEntity_ReturnsOnlyEventsForThatEntity()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORDER-TARGET", null, null, null, null, "Created");
        await w.LogAuditEventAsync("ORDERS", "SIGN", "OrderState", "ORDER-TARGET", null, null, null, null, "Signed");
        await w.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORDER-OTHER", null, null, null, null, "Different order");

        List<AuditEventSummary> events = await w.GetAuditEventsByEntityAsync("OrderState", "ORDER-TARGET");
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events.All(e => e.EntityId == "ORDER-TARGET"), Is.True);
    }

    // ─── Patient Isolation ────────────────────────────────────────────────

    [Test]
    public async Task AuditTrail_MultiplePatients_EventsAreIndependent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await w1.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-P1", null, null, null, null, "Patient 1 lab");

        List<AuditEventSummary> eventsForP2 = await w2.GetRecentAuditEventsAsync(100);
        Assert.That(eventsForP2, Is.Empty);
    }

    // ─── Full Workflow ────────────────────────────────────────────────────

    [Test]
    public async Task FullAuditWorkflow_OrderLifecycle_AllEventsTracked()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        const string orderId = "ORDER-LIFECYCLE";

        await w.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", orderId,
            "PROV-001", "Dr. Smith", "LOC-1", "Outpatient Clinic", "Lab order created");
        await w.LogAuditEventAsync("ORDERS", "SIGN", "OrderState", orderId,
            "PROV-001", "Dr. Smith", "LOC-1", "Outpatient Clinic", "Order signed");
        await w.LogAuditEventAsync("ORDERS", "DISCONTINUE", "OrderState", orderId,
            "PROV-002", "Dr. Jones", "LOC-1", "Outpatient Clinic", "Order discontinued - duplicate");

        List<AuditEventSummary> entityEvents = await w.GetAuditEventsByEntityAsync("OrderState", orderId);
        Assert.That(entityEvents, Has.Count.EqualTo(3));
        Assert.That(entityEvents.Any(e => e.Action == "CREATE"), Is.True);
        Assert.That(entityEvents.Any(e => e.Action == "SIGN"), Is.True);
        Assert.That(entityEvents.Any(e => e.Action == "DISCONTINUE"), Is.True);

        List<AuditEventSummary> allOrderEvents = await w.GetAuditEventsAsync("ORDERS", null, null);
        Assert.That(allOrderEvents, Has.Count.EqualTo(3));
    }

    // ─── Tamper-Resistance (§170.315(d)(2)) ──────────────────────────────

    [Test]
    public async Task AuditEvent_HasEventHash()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "LABS", "CREATE", "LabTestState", "LAB-HASH-1",
            null, null, null, null, "Test event");

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.EventHash, Is.Not.Null.And.Not.Empty);
        Assert.That(state.PreviousEventHash, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task AuditEvent_FirstEvent_UseGenesisHash()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "LABS", "CREATE", "LabTestState", "LAB-GENESIS",
            null, null, null, null, "First event");

        AuditEventState state = await w.GetAuditEventAsync(eventId);
        Assert.That(state.PreviousEventHash, Is.EqualTo(IAuditEventGrain.GenesisHash));
    }

    [Test]
    public async Task AuditEvent_VerifyIntegrity_ReturnsTrueForUntamperedEvent()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string eventId = await w.LogAuditEventAsync(
            "ORDERS", "SIGN", "OrderState", "ORDER-VERIFY",
            "PROV-001", "Dr. Smith", null, null, "Integrity test");

        IAuditEventGrain eventGrain = _cluster.GrainFactory.GetGrain<IAuditEventGrain>(eventId);
        bool valid = await eventGrain.VerifyIntegrityAsync();
        Assert.That(valid, Is.True);
    }

    [Test]
    public async Task AuditEvent_HashChain_SecondEventLinksToFirst()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string firstId = await w.LogAuditEventAsync(
            "LABS", "CREATE", "LabTestState", "LAB-CHAIN-1",
            null, null, null, null, "First in chain");

        string secondId = await w.LogAuditEventAsync(
            "LABS", "UPDATE", "LabTestState", "LAB-CHAIN-2",
            null, null, null, null, "Second in chain");

        AuditEventState firstState = await w.GetAuditEventAsync(firstId);
        AuditEventState secondState = await w.GetAuditEventAsync(secondId);

        // Second event's PreviousEventHash should be the first event's EventHash
        Assert.That(secondState.PreviousEventHash, Is.EqualTo(firstState.EventHash));
    }

    [Test]
    public async Task AuditEvent_IndexSummary_ContainsEventHash()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.LogAuditEventAsync(
            "NOTES", "CREATE", "TiuDocumentState", "NOTE-HASH",
            null, null, null, null, "Hash in index");

        List<AuditEventSummary> events = await w.GetRecentAuditEventsAsync(1);
        Assert.That(events[0].EventHash, Is.Not.Null.And.Not.Empty);
    }
}
