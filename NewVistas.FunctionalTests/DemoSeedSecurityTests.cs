// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the DemoSeedHelper SYSTEM/XUPROG context switching pattern.
///
/// The DemoSeedHelper (in NewVistas.WebServer.Infrastructure) sets Orleans RequestContext
/// to a SYSTEM-SEED user with XUPROG (superuser) key, allowing demo/load endpoints to
/// call workflow methods that require security keys the current user may not hold.
///
/// Since functional tests cannot reference NewVistas.WebServer, these tests verify the
/// UNDERLYING PATTERN: that a user with XUPROG can bypass all key checks, that nurse-role
/// users are properly limited, and that RequestContext switching works correctly.
/// </summary>
[TestFixture]
public class DemoSeedSecurityTests
{
    private TestCluster _cluster = null!;

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            // Stores needed by grains under test
            siloBuilder.AddMemoryGrainStorage("patientStore");
            siloBuilder.AddMemoryGrainStorage("patientHistoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("accessControlStore");
            siloBuilder.AddMemoryGrainStorage("problemStore");
            siloBuilder.AddMemoryGrainStorage("allergyStore");
            siloBuilder.AddMemoryGrainStorage("vitalStore");
            siloBuilder.AddMemoryGrainStorage("appointmentStore");
            siloBuilder.AddMemoryGrainStorage("scheduleIndexStore");
            siloBuilder.AddMemoryGrainStorage("clinicStore");
            siloBuilder.AddMemoryGrainStorage("clinicIndexStore");
            siloBuilder.AddMemoryGrainStorage("labTestStore");
            siloBuilder.AddMemoryGrainStorage("labBatchStore");
            siloBuilder.AddMemoryGrainStorage("labSummaryStore");
            siloBuilder.AddMemoryGrainStorage("labIndexStore");
            siloBuilder.AddMemoryGrainStorage("orderStore");
            siloBuilder.AddMemoryGrainStorage("newPersonStore");
            siloBuilder.AddMemoryGrainStorage("siteParametersStore");
            siloBuilder.AddMemoryGrainStorage("patientOrderIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientVitalIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientEnrollmentStore");
            siloBuilder.AddMemoryGrainStorage("adtStore");
            siloBuilder.AddMemoryGrainStorage("wardCensusStore");
            siloBuilder.AddMemoryGrainStorage("wardLocationStore");
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryStreams("LabStreams");

            // Register the authorization call filter — this is what enforces key checks
            siloBuilder.AddIncomingGrainCallFilter<AuthorizationCallFilter>();
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
        // Clear RequestContext between tests to avoid cross-contamination
        RequestContext.Remove(RequestContextKeys.UserId);
        RequestContext.Remove(RequestContextKeys.UserName);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow() =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PAT-{Guid.NewGuid()}");

    private IAccessControlGrain GetAclGrain(string userId) =>
        _cluster.GrainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");

    /// <summary>
    /// Provision a user with one or more security keys and an active session.
    /// Returns the userId. Caller MUST call SetRequestContext() after awaiting this,
    /// because AsyncLocal (RequestContext) does not flow upward from child async contexts.
    /// </summary>
    private async Task<string> ProvisionUserAsync(params string[] keys)
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        foreach (string key in keys)
            await acl.GrantKeyAsync(key, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);
        return userId;
    }

    private void SetRequestContext(string userId, string? userName = null)
    {
        RequestContext.Set(RequestContextKeys.UserId, userId);
        RequestContext.Set(RequestContextKeys.UserName, userName ?? "TEST,USER");
    }

    // ─── XUPROG Bypass Tests ────────────────────────────────────────────

    [Test]
    public async Task XUPROG_BypassesAllKeyChecks_AddProblem()
    {
        // Arrange — user holds only XUPROG, not GMPL_PROBLEM
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(userId, "SYSTEM,SEED");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — AddProblemAsync requires GMPL_PROBLEM, but XUPROG bypasses all checks
        string problemId = await workflow.AddProblemAsync(
            "HYPERTENSION", "I10", null, null, null,
            null, null, null, null, false, null);

        // Assert
        Assert.That(problemId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task XUPROG_BypassesAllKeyChecks_RecordVitals()
    {
        // Arrange — user holds only XUPROG, not GMRV_VITALS
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(userId, "SYSTEM,SEED");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — RecordVitalsAsync requires GMRV_VITALS, but XUPROG bypasses
        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { { "BLOOD PRESSURE", "120/80" } },
            null);

        // Assert — no exception means success; verify data persisted
        List<VitalSummary> vitals = await workflow.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task XUPROG_BypassesAllKeyChecks_ScheduleAppointment()
    {
        // Arrange — user holds only XUPROG, not SD_SCHEDULING
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(userId, "SYSTEM,SEED");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — ScheduleAppointmentAsync requires SD_SCHEDULING, but XUPROG bypasses
        // Use allowDoubleBook=true to skip clinic capacity checks
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            "SD-CLINIC:PRIMARY-CARE", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(7), 30,
            null, null, "ROUTINE VISIT", "REGULAR", allowDoubleBook: true);

        // Assert
        Assert.That(appointmentId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task XUPROG_BypassesAllKeyChecks_RecordAllergy()
    {
        // Arrange — user holds only XUPROG, not GMRA_ALLERGY
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(userId, "SYSTEM,SEED");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — RecordAllergyAsync requires GMRA_ALLERGY, but XUPROG bypasses
        string allergyId = await workflow.RecordAllergyAsync(
            "PENICILLIN", "DRUG", null, "O",
            new List<string> { "RASH", "HIVES" }, "MODERATE",
            null, null, null);

        // Assert
        Assert.That(allergyId, Is.Not.Null.And.Not.Empty);
    }

    // ─── Nurse Key Limitations ──────────────────────────────────────────

    [Test]
    public async Task NurseKeys_CanRecordVitals()
    {
        // Arrange — nurse with GMRV_VITALS key
        string userId = await ProvisionUserAsync(SecurityKeys.GMRV_VITALS);
        SetRequestContext(userId, "NURSE,JANE");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — RecordVitalsAsync requires GMRV_VITALS, which the nurse holds
        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { { "TEMPERATURE", "98.6" } },
            null);

        // Assert — no exception means success
        List<VitalSummary> vitals = await workflow.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task NurseKeys_CannotCollectSpecimen()
    {
        // Arrange — nurse with typical nurse keys but NOT LRLAB
        string userId = await ProvisionUserAsync(
            SecurityKeys.ORELSE,
            SecurityKeys.GMRV_VITALS,
            SecurityKeys.GMRA_ALLERGY,
            SecurityKeys.GMPL_PROBLEM,
            SecurityKeys.SD_SCHEDULING);
        SetRequestContext(userId, "NURSE,JANE");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act & Assert — CollectSpecimenAsync requires LRLAB, which the nurse does not hold
        UnauthorizedAccessException ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.CollectSpecimenAsync(
                $"LAB-{Guid.NewGuid()}", DateTime.UtcNow, "BLOOD", "MAIN LAB"))!;

        Assert.That(ex.Message, Does.Contain(SecurityKeys.LRLAB));
    }

    [Test]
    public async Task NurseKeys_CannotRecordAdmission()
    {
        // Arrange — nurse with typical nurse keys but NOT DG_ADMIT
        string userId = await ProvisionUserAsync(
            SecurityKeys.ORELSE,
            SecurityKeys.GMRV_VITALS,
            SecurityKeys.GMRA_ALLERGY,
            SecurityKeys.GMPL_PROBLEM,
            SecurityKeys.SD_SCHEDULING);
        SetRequestContext(userId, "NURSE,JANE");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act & Assert — RecordAdmissionAsync requires DG_ADMIT, which the nurse does not hold
        UnauthorizedAccessException ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.RecordAdmissionAsync(
                DateTime.UtcNow, "WARD-MED-3A", "MEDICINE 3A",
                "301-A", "INTERNAL MEDICINE",
                null, null, "CHEST PAIN", null))!;

        Assert.That(ex.Message, Does.Contain(SecurityKeys.DG_ADMIT));
    }

    // ─── Context Switching Pattern ──────────────────────────────────────

    [Test]
    public async Task ContextSwitch_SystemUserCanSeedMultipleTypes()
    {
        // Arrange — provision a SYSTEM-SEED user with XUPROG (mimics DemoSeedHelper)
        string systemUserId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(systemUserId, "SYSTEM,SEED");
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — seed multiple types of clinical data in sequence (like demo/load endpoints do)
        string problemId = await workflow.AddProblemAsync(
            "DIABETES MELLITUS TYPE 2", "E11.9", null, null, DateTime.UtcNow.AddYears(-2),
            null, null, null, null, false, null);

        string allergyId = await workflow.RecordAllergyAsync(
            "SULFA DRUGS", "DRUG CLASS", null, "H",
            new List<string> { "ANAPHYLAXIS" }, "SEVERE",
            null, null, "Historical allergy reported by patient");

        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                { "BLOOD PRESSURE", "138/88" },
                { "PULSE", "78" },
                { "TEMPERATURE", "98.4" }
            },
            null);

        // Assert — all three operations succeeded
        Assert.That(problemId, Is.Not.Null.And.Not.Empty);
        Assert.That(allergyId, Is.Not.Null.And.Not.Empty);

        List<ProblemSummary> problems = await workflow.GetActiveProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(1));

        List<AllergySummary> allergies = await workflow.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(1));

        List<VitalSummary> vitals = await workflow.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task ContextSwitch_OriginalUserRestoredAfterSeed()
    {
        // Arrange — provision a nurse user and a system seed user
        string nurseUserId = await ProvisionUserAsync(
            SecurityKeys.GMRV_VITALS,
            SecurityKeys.GMRA_ALLERGY,
            SecurityKeys.GMPL_PROBLEM);
        string systemUserId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act Step 1 — nurse records vitals (should succeed)
        SetRequestContext(nurseUserId, "NURSE,JANE");
        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { { "PULSE", "72" } },
            null);

        // Act Step 2 — switch to system context (mimics DemoSeedHelper pushing context)
        SetRequestContext(systemUserId, "SYSTEM,SEED");
        string problemId = await workflow.AddProblemAsync(
            "MIGRAINE", "G43.909", null, null, null,
            null, null, null, null, false, null);
        Assert.That(problemId, Is.Not.Null.And.Not.Empty);

        // Act Step 3 — switch back to nurse context (mimics DemoSeedHelper restoring context)
        SetRequestContext(nurseUserId, "NURSE,JANE");
        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { { "RESPIRATION", "16" } },
            null);

        // Assert — nurse keys still work after context restoration
        List<VitalSummary> vitals = await workflow.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task ContextSwitch_NurseStillBlockedAfterSystemSeed()
    {
        // Arrange — provision a nurse user and a system seed user
        string nurseUserId = await ProvisionUserAsync(
            SecurityKeys.GMRV_VITALS,
            SecurityKeys.GMRA_ALLERGY,
            SecurityKeys.GMPL_PROBLEM);
        string systemUserId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act Step 1 — system seed user seeds lab data (requires LRLAB, bypassed by XUPROG)
        SetRequestContext(systemUserId, "SYSTEM,SEED");

        // Order a lab first (requires ORES/ORELSE, bypassed by XUPROG)
        string labOrderId = await workflow.OrderLabTestAsync(
            "CBC", "COMPLETE BLOOD COUNT", "CBC",
            null, "PROV-SEED", "SYSTEM,SEED", "BLOOD", "CHEMISTRY");
        Assert.That(labOrderId, Is.Not.Null.And.Not.Empty);

        // Act Step 2 — switch back to nurse context
        SetRequestContext(nurseUserId, "NURSE,JANE");

        // Assert — nurse is still blocked from lab specimen collection (requires LRLAB)
        UnauthorizedAccessException ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.CollectSpecimenAsync(
                labOrderId, DateTime.UtcNow, "BLOOD", "MAIN LAB"))!;

        Assert.That(ex.Message, Does.Contain(SecurityKeys.LRLAB));
    }
}
