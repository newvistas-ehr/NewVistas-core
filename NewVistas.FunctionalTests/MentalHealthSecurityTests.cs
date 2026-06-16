// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for mental health security key enforcement ([RequiresSecurityKey(YS_MH_INSTRUMENT)])
/// and 42 CFR Part 2 consent tracking on IPatientAccessControlGrain.
/// </summary>
[TestFixture]
public class MentalHealthSecurityTests
{
    private TestCluster _cluster = null!;

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("patientStore");
            siloBuilder.AddMemoryGrainStorage("patientHistoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("accessControlStore");
            siloBuilder.AddMemoryGrainStorage("mentalHealthStore");
            siloBuilder.AddMemoryGrainStorage("newPersonStore");
            siloBuilder.AddMemoryGrainStorage("patientAccessStore");
            siloBuilder.AddMemoryGrainStorage("siteParametersStore");
            siloBuilder.AddIncomingGrainCallFilter<AuthorizationCallFilter>();
        }
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var builder = new TestClusterBuilder(1);
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

    private IPatientWorkflowGrain NewWorkflow() =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PAT-{Guid.NewGuid()}");

    private IAccessControlGrain GetAclGrain(string userId) =>
        _cluster.GrainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");

    private async Task<string> ProvisionUserAsync(string keyName)
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        await acl.GrantKeyAsync(keyName, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);
        return userId;
    }

    private async Task<string> ProvisionUserWithKeysAsync(params string[] keyNames)
    {
        string userId = $"USER-{Guid.NewGuid()}";
        IAccessControlGrain acl = GetAclGrain(userId);
        foreach (string key in keyNames)
        {
            await acl.GrantKeyAsync(key, "ADMIN", "ADMIN,SYS");
        }
        await acl.StartSessionAsync(null, null, null, null);
        return userId;
    }

    private void SetRequestContext(string userId, string? userName = null)
    {
        RequestContext.Set(RequestContextKeys.UserId, userId);
        RequestContext.Set(RequestContextKeys.UserName, userName ?? "TEST,USER");
    }

    // ─── Mental Health Security Key Enforcement ─────────────────────────

    [Test]
    public async Task MentalHealth_NoKey_RejectsRecordScreen()
    {
        // Arrange — user with a session but NO YS_MH_INSTRUMENT key
        string userId = await ProvisionUserAsync(SecurityKeys.PROVIDER);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act & Assert
        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.RecordMentalHealthScreenAsync(
                "PHQ-9", DateTime.UtcNow,
                12m, "MODERATE", true,
                null, userId, "TEST,USER",
                null, null, null));

        Assert.That(ex!.Message, Does.Contain(SecurityKeys.YS_MH_INSTRUMENT));
    }

    [Test]
    public async Task MentalHealth_NoKey_RejectsGetScreens()
    {
        // Arrange — user with a session but NO YS_MH_INSTRUMENT key
        string userId = await ProvisionUserAsync(SecurityKeys.PROVIDER);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act & Assert
        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.GetMentalHealthScreensAsync());

        Assert.That(ex!.Message, Does.Contain(SecurityKeys.YS_MH_INSTRUMENT));
    }

    [Test]
    public async Task MentalHealth_WithKey_AllowsRecordScreen()
    {
        // Arrange — user WITH YS_MH_INSTRUMENT key
        string userId = await ProvisionUserAsync(SecurityKeys.YS_MH_INSTRUMENT);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act
        string screenId = await workflow.RecordMentalHealthScreenAsync(
            "PHQ-9", DateTime.UtcNow,
            15m, "MODERATELY_SEVERE", true,
            new Dictionary<string, string> { { "Q1", "3" }, { "Q2", "2" } },
            userId, "TEST,USER",
            "LOC-1", "MENTAL HEALTH CLINIC", "Initial screening");

        // Assert
        Assert.That(screenId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task MentalHealth_WithKey_AllowsGetScreens()
    {
        // Arrange — user WITH YS_MH_INSTRUMENT key
        string userId = await ProvisionUserAsync(SecurityKeys.YS_MH_INSTRUMENT);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act
        List<MentalHealthSummary> screens = await workflow.GetMentalHealthScreensAsync();

        // Assert — new patient has no screens
        Assert.That(screens, Is.Not.Null);
        Assert.That(screens, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task MentalHealth_ProviderKey_RejectsScreenAccess()
    {
        // Arrange — user with PROVIDER key but NOT YS_MH_INSTRUMENT
        string userId = await ProvisionUserAsync(SecurityKeys.PROVIDER);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act & Assert — PROVIDER alone is insufficient for MH methods
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.RecordMentalHealthScreenAsync(
                "GAD-7", DateTime.UtcNow,
                10m, "MODERATE", true,
                null, userId, "TEST,USER",
                null, null, null));

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await workflow.GetMentalHealthScreensAsync());
    }

    [Test]
    public async Task MentalHealth_XUPROG_BypassesMhCheck()
    {
        // Arrange — user with XUPROG (superuser) but NOT YS_MH_INSTRUMENT
        string userId = await ProvisionUserAsync(SecurityKeys.XUPROG);
        SetRequestContext(userId);
        IPatientWorkflowGrain workflow = NewWorkflow();

        // Act — XUPROG should bypass all security key checks
        string screenId = await workflow.RecordMentalHealthScreenAsync(
            "PHQ-9", DateTime.UtcNow,
            5m, "MILD", false,
            null, userId, "ADMIN,SUPER",
            null, null, "XUPROG bypass test");

        // Assert
        Assert.That(screenId, Is.Not.Null.And.Not.Empty);

        // Also verify GetMentalHealthScreensAsync works via XUPROG
        List<MentalHealthSummary> screens = await workflow.GetMentalHealthScreensAsync();
        Assert.That(screens, Has.Count.GreaterThanOrEqualTo(1));
    }
}

/// <summary>
/// Functional tests for 42 CFR Part 2 consent tracking on IPatientAccessControlGrain.
/// These test the grain directly (no AuthorizationCallFilter needed) using SharedCluster.
/// </summary>
[TestFixture]
public class Part2ConsentTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientAccessControlGrain NewPacGrain() =>
        _cluster.GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{Guid.NewGuid()}");

    // ─── 42 CFR Part 2 Consent Tracking ─────────────────────────────────

    [Test]
    public async Task Part2Consent_DefaultIsFalse()
    {
        // Arrange
        IPatientAccessControlGrain pac = NewPacGrain();

        // Act
        bool hasConsent = await pac.HasPart2ConsentAsync();

        // Assert — new patient has no Part 2 consent on file
        Assert.That(hasConsent, Is.False);
    }

    [Test]
    public async Task Part2Consent_SetConsent_PersistsState()
    {
        // Arrange
        IPatientAccessControlGrain pac = NewPacGrain();
        DateTime consentDate = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        await pac.SetPart2ConsentAsync(true, consentDate, "TPO");

        // Assert
        bool hasConsent = await pac.HasPart2ConsentAsync();
        Assert.That(hasConsent, Is.True);
    }

    [Test]
    public async Task Part2Consent_RevokeConsent_ClearsState()
    {
        // Arrange — set consent first
        IPatientAccessControlGrain pac = NewPacGrain();
        DateTime consentDate = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        await pac.SetPart2ConsentAsync(true, consentDate, "TPO");

        // Verify consent is set
        bool hasConsentBefore = await pac.HasPart2ConsentAsync();
        Assert.That(hasConsentBefore, Is.True);

        // Act — revoke consent
        await pac.SetPart2ConsentAsync(false, null, null);

        // Assert
        bool hasConsentAfter = await pac.HasPart2ConsentAsync();
        Assert.That(hasConsentAfter, Is.False);
    }

    [Test]
    public async Task Part2Consent_ConsentScope_Persists()
    {
        // Arrange
        IPatientAccessControlGrain pac = NewPacGrain();
        DateTime consentDate = new DateTime(2026, 2, 1, 8, 30, 0, DateTimeKind.Utc);

        // Act
        await pac.SetPart2ConsentAsync(true, consentDate, "TREATMENT_ONLY");

        // Assert — verify scope persists via GetAccessControlAsync
        PatientAccessControlState state = await pac.GetAccessControlAsync();
        Assert.That(state.HasPart2Consent, Is.True);
        Assert.That(state.Part2ConsentScope, Is.EqualTo("TREATMENT_ONLY"));
    }

    [Test]
    public async Task Part2Consent_ConsentDate_Persists()
    {
        // Arrange
        IPatientAccessControlGrain pac = NewPacGrain();
        DateTime consentDate = new DateTime(2026, 1, 20, 14, 0, 0, DateTimeKind.Utc);

        // Act
        await pac.SetPart2ConsentAsync(true, consentDate, "FULL");

        // Assert — verify date persists via GetAccessControlAsync
        PatientAccessControlState state = await pac.GetAccessControlAsync();
        Assert.That(state.HasPart2Consent, Is.True);
        Assert.That(state.Part2ConsentDate, Is.Not.Null);
        Assert.That(state.Part2ConsentDate, Is.EqualTo(consentDate));
        Assert.That(state.Part2ConsentScope, Is.EqualTo("FULL"));
    }
}
