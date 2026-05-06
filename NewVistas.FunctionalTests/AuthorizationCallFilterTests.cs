// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the AuthorizationCallFilter — verifies that
/// [RequiresSecurityKey] attributes on grain methods are enforced cluster-wide,
/// and that session timeout is checked (§170.315(d)(5)).
/// </summary>
[TestFixture]
public class AuthorizationCallFilterTests
{
    private TestCluster _cluster = null!;

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            // Stores needed by grains under test
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

            // Register the authorization call filter — this is what we're testing
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

    private IPatientWorkflowGrain NewWorkflow() =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PAT-{Guid.NewGuid()}");

    private IAccessControlGrain GetAclGrain(string userId) =>
        _cluster.GrainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");

    /// <summary>
    /// Provision a user with a key and an active session — the common test setup.
    /// Returns the userId. Caller MUST call SetRequestContext() after awaiting this,
    /// because AsyncLocal (RequestContext) does not flow upward from child async contexts.
    /// </summary>
    private async Task<string> ProvisionUserAsync(string keyName)
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        await acl.GrantKeyAsync(keyName, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);
        return userId;
    }

    private void SetRequestContext(string userId, string? userName = null)
    {
        RequestContext.Set(RequestContextKeys.UserId, userId);
        RequestContext.Set(RequestContextKeys.UserName, userName ?? "TEST,USER");
    }

    // ─── Unauthenticated access (no RequestContext) ─────────────────────

    [Test]
    public async Task Filter_NoUserId_RejectsProtectedMethod()
    {
        IPatientWorkflowGrain workflow = NewWorkflow();

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "HYPERTENSION", "I10", null, null, null,
                null, null, null, null, false, null));

        Assert.That(ex!.Message, Does.Contain("no authenticated user"));
    }

    [Test]
    public async Task Filter_NoUserId_AllowsUnprotectedMethod()
    {
        IPatientWorkflowGrain workflow = NewWorkflow();

        List<ProblemSummary> result = await workflow.GetActiveProblemsAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(0));
    }

    // ─── Missing key ────────────────────────────────────────────────────

    [Test]
    public async Task Filter_UserWithoutKey_RejectsProtectedMethod()
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        await acl.GrantKeyAsync(SecurityKeys.PROVIDER, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);

        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "DIABETES", "E11.9", null, null, null,
                null, null, null, null, false, null));

        Assert.That(ex!.Message, Does.Contain(SecurityKeys.GMPL_PROBLEM));
    }

    // ─── Correct key grants access ──────────────────────────────────────

    [Test]
    public async Task Filter_UserWithCorrectKey_AllowsProblemAdd()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMPL_PROBLEM);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        string problemId = await workflow.AddProblemAsync(
            "HYPERTENSION", "I10", null, null, null,
            null, null, null, null, false, null);

        Assert.That(problemId, Is.Not.Empty);
    }

    [Test]
    public async Task Filter_UserWithOresKey_AllowsPlaceOrder()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.ORES);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        string orderId = await workflow.PlaceOrderAsync(
            "LAB", "CBC", null, "PROV-1", "DOE,JOHN",
            null, null, "ROUTINE", null, null);

        Assert.That(orderId, Is.Not.Empty);
    }

    [Test]
    public async Task Filter_UserWithOrelseKey_AllowsPlaceOrder()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.ORELSE);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        string orderId = await workflow.PlaceOrderAsync(
            "LAB", "BMP", null, "PROV-1", "DOE,JOHN",
            null, null, "ROUTINE", null, null);

        Assert.That(orderId, Is.Not.Empty);
    }

    [Test]
    public async Task Filter_UserWithVitalsKey_AllowsRecordVitals()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMRV_VITALS);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        await workflow.RecordVitalsAsync(
            null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { { "BLOOD PRESSURE", "120/80" } },
            null);

        Assert.Pass();
    }

    [Test]
    public async Task Filter_UserWithAllergyKey_AllowsRecordAllergy()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMRA_ALLERGY);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        string allergyId = await workflow.RecordAllergyAsync(
            "PENICILLIN", "DRUG", null, "O",
            new List<string> { "RASH" }, "MODERATE",
            null, null, null);

        Assert.That(allergyId, Is.Not.Empty);
    }

    // ─── XUPROG bypasses all checks ─────────────────────────────────────

    [Test]
    public async Task Filter_XuprogKey_BypassesAllChecks()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Should bypass GMPL_PROBLEM requirement
        string problemId = await workflow.AddProblemAsync(
            "MIGRAINE", "G43.909", null, null, null,
            null, null, null, null, false, null);

        Assert.That(problemId, Is.Not.Empty);
    }

    // ─── Exempt grains skip the filter ──────────────────────────────────

    [Test]
    public async Task Filter_AccessControlGrain_ExemptFromFilter()
    {
        IAccessControlGrain acl = GetAclGrain($"USER-{Guid.NewGuid()}");

        await acl.GrantKeyAsync(SecurityKeys.ORES, "ADMIN", "ADMIN,SYS");

        IReadOnlySet<string> keys = await acl.GetKeysAsync();
        Assert.That(keys, Contains.Item(SecurityKeys.ORES));
    }

    [Test]
    public async Task Filter_NewPersonGrain_ExemptFromFilter()
    {
        INewPersonGrain person = _cluster.GrainFactory
            .GetGrain<INewPersonGrain>($"USER:{Guid.NewGuid()}");

        await person.UpdateProfileAsync(
            "TEST,USER", null, null, null, null, null, null, null, null, null, null);

        string name = await person.GetDisplayNameAsync();
        Assert.That(name, Is.EqualTo("TEST,USER"));
    }

    // ─── Multiple protected methods on same grain ───────────────────────

    [Test]
    public async Task Filter_DifferentMethodsRequireDifferentKeys()
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        await acl.GrantKeyAsync(SecurityKeys.GMPL_PROBLEM, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);

        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Problem add should succeed
        string problemId = await workflow.AddProblemAsync(
            "GERD", "K21.0", null, null, null,
            null, null, null, null, false, null);
        Assert.That(problemId, Is.Not.Empty);

        // Vitals record should fail — user lacks GMRV_VITALS
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.RecordVitalsAsync(
                null, null, null, null, DateTime.UtcNow,
                new Dictionary<string, string> { { "TEMPERATURE", "98.6" } },
                null));
    }

    // ─── Read methods always allowed ────────────────────────────────────

    [Test]
    public async Task Filter_ReadMethods_AlwaysAllowed()
    {
        string userId = $"USER-{Guid.NewGuid()}";
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        List<ProblemSummary> problems = await workflow.GetActiveProblemsAsync();
        List<AllergySummary> allergies = await workflow.GetAllergiesAsync();
        List<VitalSummary> vitals = await workflow.GetLatestVitalsAsync();

        Assert.That(problems, Has.Count.EqualTo(0));
        Assert.That(allergies, Has.Count.EqualTo(0));
        Assert.That(vitals, Has.Count.EqualTo(0));
    }

    // ─── Key revocation takes effect immediately ────────────────────────

    [Test]
    public async Task Filter_KeyRevocation_DeniesAccess()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMPL_PROBLEM);
        SetRequestContext(userId);
        IAccessControlGrain acl = GetAclGrain(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // First call succeeds
        string problemId = await workflow.AddProblemAsync(
            "ASTHMA", "J45.909", null, null, null,
            null, null, null, null, false, null);
        Assert.That(problemId, Is.Not.Empty);

        // Revoke key
        await acl.RevokeKeyAsync(SecurityKeys.GMPL_PROBLEM, "ADMIN", "ADMIN,SYS");

        // Second call should now fail
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "COPD", "J44.1", null, null, null,
                null, null, null, null, false, null));
    }

    // ─── Session timeout enforcement (§170.315(d)(5)) ───────────────────

    [Test]
    public async Task Filter_NoSession_RejectsProtectedMethod()
    {
        // User has the right key but NO active session
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        await acl.GrantKeyAsync(SecurityKeys.GMPL_PROBLEM, "ADMIN", "ADMIN,SYS");
        // No StartSessionAsync call

        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "HYPERTENSION", "I10", null, null, null,
                null, null, null, null, false, null));

        Assert.That(ex!.Message, Does.Contain("session expired"));
    }

    [Test]
    public async Task Filter_ExpiredSession_RejectsProtectedMethod()
    {
        // User has key + session, but session timeout is set to 0 minutes (immediately expired)
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        await acl.GrantKeyAsync(SecurityKeys.GMPL_PROBLEM, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);
        await acl.SetSessionTimeoutAsync(0); // expire immediately

        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Small delay to ensure the 0-minute timeout has passed
        await Task.Delay(50);

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "DIABETES", "E11.9", null, null, null,
                null, null, null, null, false, null));

        Assert.That(ex!.Message, Does.Contain("session expired"));
    }

    [Test]
    public async Task Filter_EndedSession_RejectsProtectedMethod()
    {
        // User logs in, then logs out — subsequent calls should fail
        string userId = await ProvisionUserAsync(SecurityKeys.GMPL_PROBLEM);
        SetRequestContext(userId);
        IAccessControlGrain acl = GetAclGrain(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // First call succeeds (active session)
        string problemId = await workflow.AddProblemAsync(
            "GERD", "K21.0", null, null, null,
            null, null, null, null, false, null);
        Assert.That(problemId, Is.Not.Empty);

        // Logout
        await acl.EndSessionAsync();

        // Second call should fail
        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "COPD", "J44.1", null, null, null,
                null, null, null, null, false, null));

        Assert.That(ex!.Message, Does.Contain("session expired"));
    }

    [Test]
    public async Task Filter_ReloginAfterExpiry_RestoresAccess()
    {
        string userId = await ProvisionUserAsync(SecurityKeys.GMPL_PROBLEM);
        SetRequestContext(userId);
        IAccessControlGrain acl = GetAclGrain(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // End session
        await acl.EndSessionAsync();

        // Verify locked out
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.AddProblemAsync(
                "ASTHMA", "J45.909", null, null, null,
                null, null, null, null, false, null));

        // Re-login (start new session)
        await acl.StartSessionAsync(null, null, null, null);

        // Access restored
        string problemId = await workflow.AddProblemAsync(
            "ASTHMA", "J45.909", null, null, null,
            null, null, null, null, false, null);
        Assert.That(problemId, Is.Not.Empty);
    }
}
