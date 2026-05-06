// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the AuditCallFilter — verifies that [AuditAction] attributes
/// on workflow grain methods produce immutable audit event records automatically.
///
/// ONC §170.315(d)(2) — auditable events and tamper-resistance.
/// ONC §170.315(d)(10) — auditing actions on health information.
/// </summary>
[TestFixture]
public class AuditCallFilterTests
{
    private TestCluster _cluster = null!;

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("patientStore");
            siloBuilder.AddMemoryGrainStorage("accessControlStore");
            siloBuilder.AddMemoryGrainStorage("problemStore");
            siloBuilder.AddMemoryGrainStorage("orderStore");
            siloBuilder.AddMemoryGrainStorage("vitalStore");
            siloBuilder.AddMemoryGrainStorage("allergyStore");
            siloBuilder.AddMemoryGrainStorage("tiuDocumentStore");
            siloBuilder.AddMemoryGrainStorage("newPersonStore");
            siloBuilder.AddMemoryGrainStorage("patientOrderIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientNoteIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientVitalIndexStore");
            siloBuilder.AddMemoryGrainStorage("siteParametersStore");
            siloBuilder.AddMemoryGrainStorage("auditEventStore");
            siloBuilder.AddMemoryGrainStorage("patientAuditIndexStore");

            // Clinical event sourcing — clinical writes (IsClinicalWrite=true) flow
            // through IPatientClinicalEventStreamGrain instead of the audit filter.
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");

            // Federation seam — default no-op sink so the stream grain's constructor
            // dependency resolves in the test cluster.
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IClusterIdentity>(new StaticClusterIdentity("TEST-CLUSTER", "099"));

            // Both filters registered — authorization runs first, then audit
            siloBuilder.AddIncomingGrainCallFilter<AuthorizationCallFilter>();
            siloBuilder.AddIncomingGrainCallFilter<AuditCallFilter>();
        }
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        TestClusterBuilder builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        RequestContext.Remove(RequestContextKeys.UserId);
        RequestContext.Remove(RequestContextKeys.UserName);
    }

    private async Task<string> ProvisionUserAsync(string keyName)
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = _cluster.GrainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");
        await acl.GrantKeyAsync(keyName, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);
        return userId;
    }

    private void SetRequestContext(string userId, string userName = "TEST,PROVIDER")
    {
        RequestContext.Set(RequestContextKeys.UserId, userId);
        RequestContext.Set(RequestContextKeys.UserName, userName);
    }

    private IPatientWorkflowGrain NewWorkflow(string? patientId = null) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId ?? $"PAT-{Guid.NewGuid()}");

    private IPatientAuditIndexGrain GetAuditIndex(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientAuditIndexGrain>(patientId);

    private IPatientClinicalEventStreamGrain GetClinicalStream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private async Task<IReadOnlyList<EventEnvelope>> GetClinicalEventsAsync(string patientId)
    {
        IPatientClinicalEventStreamGrain stream = GetClinicalStream(patientId);
        int version = await stream.GetVersionAsync();
        return await stream.ReadAsync(0, version);
    }

    // ─── Audit event creation ───────────────────────────────────────────

    [Test]
    public async Task Audit_PlaceOrder_CreatesAuditEvent()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.ORES);
        SetRequestContext(userId, "DOE,JOHN");
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        string orderId = await workflow.PlaceOrderAsync(
            "LAB", "CBC", null, "PROV-1", "DOE,JOHN",
            null, null, "ROUTINE", null, null);

        IReadOnlyList<EventEnvelope> events = await GetClinicalEventsAsync(patientId);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Domain, Is.EqualTo("ORDERS"));
        Assert.That(events[0].EventType, Is.EqualTo("OrderPlacedV1"));
        Assert.That(events[0].UserName, Is.EqualTo("DOE,JOHN"));
    }

    [Test]
    public async Task Audit_AddProblem_CreatesAuditEvent()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMPL_PROBLEM);
        SetRequestContext(userId, "SMITH,JANE");
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        await workflow.AddProblemAsync(
            "HYPERTENSION", "I10", null, null, null,
            null, null, null, null, false, null);

        IReadOnlyList<EventEnvelope> events = await GetClinicalEventsAsync(patientId);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Domain, Is.EqualTo("PROBLEMS"));
        Assert.That(events[0].EventType, Is.EqualTo("ProblemAddedV1"));
        Assert.That(events[0].UserName, Is.EqualTo("SMITH,JANE"));
    }

    [Test]
    public async Task Audit_RecordVitals_CreatesAuditEvent()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMRV_VITALS);
        SetRequestContext(userId);
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { { "BLOOD PRESSURE", "120/80" } },
            null);

        IReadOnlyList<EventEnvelope> events = await GetClinicalEventsAsync(patientId);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Domain, Is.EqualTo("VITALS"));
        Assert.That(events[0].EventType, Is.EqualTo("VitalRecordedV1"));
    }

    [Test]
    public async Task Audit_RecordAllergy_CreatesAuditEvent()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMRA_ALLERGY);
        SetRequestContext(userId, "NURSE,AMY");
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        await workflow.RecordAllergyAsync(
            "PENICILLIN", "DRUG", null, "O",
            new List<string> { "RASH" }, "MODERATE",
            null, null, null);

        IReadOnlyList<EventEnvelope> events = await GetClinicalEventsAsync(patientId);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Domain, Is.EqualTo("ALLERGIES"));
        Assert.That(events[0].UserName, Is.EqualTo("NURSE,AMY"));
    }

    // ─── No audit for unprotected methods ───────────────────────────────

    [Test]
    public async Task Audit_ReadMethod_NoAuditEvent()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        // Read methods have no [AuditAction] — should produce zero events
        await workflow.GetActiveProblemsAsync();
        await workflow.GetAllergiesAsync();

        await Task.Delay(200);

        List<AuditEventSummary> events = await GetAuditIndex(patientId).GetRecentEventsAsync(10);
        Assert.That(events, Has.Count.EqualTo(0));
    }

    // ─── Multiple actions on same patient ───────────────────────────────

    [Test]
    public async Task Audit_MultipleActions_AllRecorded()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG); // superuser for all keys
        SetRequestContext(userId, "ADMIN,SUPER");
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        await workflow.AddProblemAsync("DIABETES", "E11.9", null, null, null, null, null, null, null, false, null);
        await workflow.RecordAllergyAsync("SULFA", "DRUG", null, "O", null, "MILD", null, null, null);
        await workflow.PlaceOrderAsync("LAB", "BMP", null, "PROV-1", "ADMIN,SUPER", null, null, "ROUTINE", null, null);

        IReadOnlyList<EventEnvelope> events = await GetClinicalEventsAsync(patientId);
        Assert.That(events, Has.Count.EqualTo(3));

        List<string> domains = events.Select(e => e.Domain).OrderBy(d => d).ToList();
        Assert.That(domains, Does.Contain("ALLERGIES"));
        Assert.That(domains, Does.Contain("ORDERS"));
        Assert.That(domains, Does.Contain("PROBLEMS"));
    }

    // ─── Failed operations don't create audit events ────────────────────

    [Test]
    public async Task Audit_FailedAuth_NoAuditEvent()
    {
        // User without the right key — method will throw UnauthorizedAccessException
        string userId = await ProvisionUserAsync(SecurityKeys.PROVIDER); // not GMPL_PROBLEM
        SetRequestContext(userId);
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        try
        {
            await workflow.AddProblemAsync("ASTHMA", "J45.909", null, null, null, null, null, null, null, false, null);
        }
        catch (UnauthorizedAccessException) { /* expected */ }

        await Task.Delay(200);

        // No audit event should exist — the operation was denied
        List<AuditEventSummary> events = await GetAuditIndex(patientId).GetRecentEventsAsync(10);
        Assert.That(events, Has.Count.EqualTo(0));
    }

    // ─── Audit event detail grain is populated ──────────────────────────

    [Test]
    public async Task Audit_EventGrain_HasFullDetails()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.ORES);
        SetRequestContext(userId, "PROVIDER,TEST");
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = NewWorkflow(patientId);

        await workflow.PlaceOrderAsync(
            "PHARMACY", "METFORMIN 500MG", null, "PROV-1", "PROVIDER,TEST",
            null, null, "ROUTINE", null, null);

        IReadOnlyList<EventEnvelope> events = await GetClinicalEventsAsync(patientId);
        Assert.That(events, Has.Count.EqualTo(1));

        EventEnvelope envelope = events[0];
        Assert.That(envelope.PatientId, Is.EqualTo(patientId));
        Assert.That(envelope.Domain, Is.EqualTo("ORDERS"));
        Assert.That(envelope.EventType, Is.EqualTo("OrderPlacedV1"));
        Assert.That(envelope.UserId, Is.EqualTo(userId));
        Assert.That(envelope.UserName, Is.EqualTo("PROVIDER,TEST"));
        Assert.That(envelope.EventId, Does.StartWith("CEV-"));
        Assert.That(envelope.EventHash, Is.Not.Empty);
        Assert.That(envelope.PreviousEventHash, Is.EqualTo(HashChain.GenesisHash));
    }
}
