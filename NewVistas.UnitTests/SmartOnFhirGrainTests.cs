// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for SMART on FHIR grains — §170.315(g)(10) compliance.
/// Tests client registration, OAuth 2.0 authorization flows, PKCE,
/// token introspection, revocation, and bulk data export.
/// </summary>
[TestFixture]
public class SmartOnFhirGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── SmartClientGrain ─────────────────────────────────────────────────────

    [Test]
    public async Task SmartClient_RegisterAndRetrieve()
    {
        string clientId = $"CLIENT-{Guid.NewGuid():N}";
        ISmartClientGrain grain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await grain.RegisterAsync(
            "Test App", new List<string> { "https://example.com/callback" },
            "public", null,
            new List<string> { "patient/Patient.read", "launch/patient" },
            null, null, null, null, "none");

        SmartClientState state = await grain.GetClientAsync();
        Assert.That(state.ClientName, Is.EqualTo("Test App"));
        Assert.That(state.ClientType, Is.EqualTo("public"));
        Assert.That(state.IsActive, Is.True);
        Assert.That(state.RedirectUris, Has.Count.EqualTo(1));
        Assert.That(state.GrantedScopes, Contains.Item("patient/Patient.read"));
    }

    [Test]
    public async Task SmartClient_ValidateRedirectUri_Registered_ReturnsTrue()
    {
        string clientId = $"CLIENT-{Guid.NewGuid():N}";
        ISmartClientGrain grain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await grain.RegisterAsync(
            "Redirect Test", new List<string> { "https://app.example.com/cb" },
            "public", null, new List<string>(), null, null, null, null, "none");

        Assert.That(await grain.ValidateRedirectUriAsync("https://app.example.com/cb"), Is.True);
        Assert.That(await grain.ValidateRedirectUriAsync("https://evil.com/cb"), Is.False);
    }

    [Test]
    public async Task SmartClient_ConfidentialClient_SecretValidation()
    {
        string clientId = $"CLIENT-{Guid.NewGuid():N}";
        ISmartClientGrain grain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await grain.RegisterAsync(
            "Confidential App", new List<string> { "https://server.example.com/cb" },
            "confidential-symmetric", "my-secret-123",
            new List<string> { "patient/Patient.read" },
            null, null, null, null, "client_secret_basic");

        Assert.That(await grain.ValidateSecretAsync("my-secret-123"), Is.True);
        Assert.That(await grain.ValidateSecretAsync("wrong-secret"), Is.False);
    }

    [Test]
    public async Task SmartClient_Deactivate_SetsInactive()
    {
        string clientId = $"CLIENT-{Guid.NewGuid():N}";
        ISmartClientGrain grain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await grain.RegisterAsync(
            "Deactivate Test", new List<string>(), "public", null,
            new List<string>(), null, null, null, null, "none");

        await grain.DeactivateAsync();
        SmartClientState state = await grain.GetClientAsync();
        Assert.That(state.IsActive, Is.False);

        await grain.ReactivateAsync();
        state = await grain.GetClientAsync();
        Assert.That(state.IsActive, Is.True);
    }

    [Test]
    public async Task SmartClient_ScopeCheck()
    {
        string clientId = $"CLIENT-{Guid.NewGuid():N}";
        ISmartClientGrain grain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await grain.RegisterAsync(
            "Scope Test", new List<string>(), "public", null,
            new List<string> { "patient/Patient.read", "patient/Condition.read" },
            null, null, null, null, "none");

        Assert.That(await grain.IsScopeGrantedAsync("patient/Patient.read"), Is.True);
        Assert.That(await grain.IsScopeGrantedAsync("patient/MedicationRequest.read"), Is.False);
    }

    // ─── SmartClientIndexGrain ────────────────────────────────────────────────

    [Test]
    public async Task SmartClientIndex_AddAndList()
    {
        string indexId = $"IDX-{Guid.NewGuid():N}";
        ISmartClientIndexGrain index = _cluster.GrainFactory.GetGrain<ISmartClientIndexGrain>(indexId);

        await index.AddClientAsync(new SmartClientSummary
        {
            ClientId = "C1", ClientName = "App 1", ClientType = "public", IsActive = true
        });
        await index.AddClientAsync(new SmartClientSummary
        {
            ClientId = "C2", ClientName = "App 2", ClientType = "confidential-symmetric", IsActive = false
        });

        List<SmartClientSummary> all = await index.GetAllClientsAsync();
        Assert.That(all, Has.Count.EqualTo(2));

        List<SmartClientSummary> active = await index.GetActiveClientsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ClientId, Is.EqualTo("C1"));
    }

    [Test]
    public async Task SmartClientIndex_RemoveClient()
    {
        string indexId = $"IDX-{Guid.NewGuid():N}";
        ISmartClientIndexGrain index = _cluster.GrainFactory.GetGrain<ISmartClientIndexGrain>(indexId);

        await index.AddClientAsync(new SmartClientSummary
        {
            ClientId = "C3", ClientName = "App 3", ClientType = "public", IsActive = true
        });
        await index.RemoveClientAsync("C3");

        List<SmartClientSummary> all = await index.GetAllClientsAsync();
        Assert.That(all, Is.Empty);
    }

    // ─── SmartAuthorizationGrain — Authorization Code Flow ────────────────────

    [Test]
    public async Task SmartAuth_CreateAuthorizationCode_ReturnsCode()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, "PATIENT-001", null);

        Assert.That(code, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task SmartAuth_ExchangeCode_ReturnsTokens()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, "PATIENT-002", null);

        SmartTokenResponse response = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", null);

        Assert.That(response.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(response.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(response.ExpiresIn, Is.EqualTo(3600));
        Assert.That(response.Scope, Does.Contain("patient/Patient.read"));
        Assert.That(response.PatientContext, Is.EqualTo("PATIENT-002"));
    }

    [Test]
    public async Task SmartAuth_ExchangeCode_WrongRedirectUri_Throws()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.ExchangeCodeAsync(code, "https://wrong.example.com/cb", null));
    }

    [Test]
    public async Task SmartAuth_CodeIsOneTimeUse()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        // First exchange succeeds
        await auth.ExchangeCodeAsync(code, "https://app.example.com/cb", null);

        // Second exchange with same code fails
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.ExchangeCodeAsync(code, "https://app.example.com/cb", null));
    }

    // ─── PKCE (RFC 7636) ─────────────────────────────────────────────────────

    [Test]
    public async Task SmartAuth_PKCE_ValidVerifier_Succeeds()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string codeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        string codeChallenge = SmartAuthorizationGrain.ComputeS256Challenge(codeVerifier);

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            codeChallenge, "S256", null, null);

        SmartTokenResponse response = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", codeVerifier);

        Assert.That(response.AccessToken, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task SmartAuth_PKCE_InvalidVerifier_Throws()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string codeVerifier = "correct-verifier-value-here-12345678901234567890";
        string codeChallenge = SmartAuthorizationGrain.ComputeS256Challenge(codeVerifier);

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            codeChallenge, "S256", null, null);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.ExchangeCodeAsync(code, "https://app.example.com/cb", "wrong-verifier"));
    }

    [Test]
    public async Task SmartAuth_PKCE_MissingVerifier_Throws()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string codeChallenge = SmartAuthorizationGrain.ComputeS256Challenge("some-verifier");

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            codeChallenge, "S256", null, null);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.ExchangeCodeAsync(code, "https://app.example.com/cb", null));
    }

    // ─── Refresh Tokens ──────────────────────────────────────────────────────

    [Test]
    public async Task SmartAuth_RefreshToken_ReturnsNewTokens()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, "PATIENT-003", null);

        SmartTokenResponse initial = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", null);

        SmartTokenResponse refreshed = await auth.RefreshTokenAsync(initial.RefreshToken!);

        Assert.That(refreshed.AccessToken, Is.Not.EqualTo(initial.AccessToken));
        Assert.That(refreshed.RefreshToken, Is.Not.EqualTo(initial.RefreshToken));
        Assert.That(refreshed.Scope, Does.Contain("patient/Patient.read"));
        Assert.That(refreshed.PatientContext, Is.EqualTo("PATIENT-003"));
    }

    [Test]
    public async Task SmartAuth_RefreshToken_OldTokenInvalidAfterRotation()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        SmartTokenResponse initial = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", null);

        // Refresh once — old refresh token should be revoked
        await auth.RefreshTokenAsync(initial.RefreshToken!);

        // Using the old refresh token should fail
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.RefreshTokenAsync(initial.RefreshToken!));
    }

    [Test]
    public async Task SmartAuth_RefreshToken_ThreeMonthValidity()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        await auth.ExchangeCodeAsync(code, "https://app.example.com/cb", null);

        SmartAuthorizationState state = await auth.GetStateAsync();
        SmartRefreshToken? activeToken = state.RefreshTokens.FirstOrDefault(t => !t.IsRevoked);

        Assert.That(activeToken, Is.Not.Null);
        // §170.315(g)(10) — refresh token must have ≥3 month validity
        TimeSpan validity = activeToken!.ExpiresDate - activeToken.IssuedDate;
        Assert.That(validity.TotalDays, Is.GreaterThanOrEqualTo(89)); // ~3 months
    }

    // ─── Token Introspection ─────────────────────────────────────────────────

    [Test]
    public async Task SmartAuth_IntrospectAccessToken_Active()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        SmartTokenResponse tokens = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", null);

        SmartTokenIntrospection result = await auth.IntrospectTokenAsync(tokens.AccessToken, true);

        Assert.That(result.Active, Is.True);
        Assert.That(result.Scope, Does.Contain("patient/Patient.read"));
        Assert.That(result.TokenType, Is.EqualTo("Bearer"));
    }

    [Test]
    public async Task SmartAuth_IntrospectInvalidToken_Inactive()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        SmartTokenIntrospection result = await auth.IntrospectTokenAsync("invalid-token", true);
        Assert.That(result.Active, Is.False);
    }

    // ─── Revocation ──────────────────────────────────────────────────────────

    [Test]
    public async Task SmartAuth_RevokeAll_InvalidatesTokens()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        SmartTokenResponse tokens = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", null);

        await auth.RevokeAllAsync();

        Assert.That(await auth.IsRevokedAsync(), Is.True);

        // Access token introspection should return inactive
        SmartTokenIntrospection result = await auth.IntrospectTokenAsync(tokens.AccessToken, true);
        Assert.That(result.Active, Is.False);

        // Refresh should fail
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.RefreshTokenAsync(tokens.RefreshToken!));
    }

    [Test]
    public async Task SmartAuth_RevokeAll_PreventsNewCodes()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        await auth.RevokeAllAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.CreateAuthorizationCodeAsync(
                "https://app.example.com/cb",
                new List<string> { "patient/Patient.read" },
                null, null, null, null));
    }

    [Test]
    public async Task SmartAuth_RevokeSpecificRefreshToken()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb",
            new List<string> { "patient/Patient.read" },
            null, null, null, null);

        SmartTokenResponse tokens = await auth.ExchangeCodeAsync(
            code, "https://app.example.com/cb", null);

        await auth.RevokeRefreshTokenAsync(tokens.RefreshToken!);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await auth.RefreshTokenAsync(tokens.RefreshToken!));
    }

    // ─── PKCE Helpers ─────────────────────────────────────────────────────────

    [Test]
    public void ComputeS256Challenge_Deterministic()
    {
        string verifier = "test-verifier-value";
        string challenge1 = SmartAuthorizationGrain.ComputeS256Challenge(verifier);
        string challenge2 = SmartAuthorizationGrain.ComputeS256Challenge(verifier);

        Assert.That(challenge1, Is.EqualTo(challenge2));
        Assert.That(challenge1, Is.Not.Empty);
    }

    [Test]
    public void ComputeS256Challenge_DifferentVerifiers_DifferentChallenges()
    {
        string c1 = SmartAuthorizationGrain.ComputeS256Challenge("verifier-1");
        string c2 = SmartAuthorizationGrain.ComputeS256Challenge("verifier-2");

        Assert.That(c1, Is.Not.EqualTo(c2));
    }

    [Test]
    public void GenerateSecureToken_UniqueEachTime()
    {
        string t1 = SmartAuthorizationGrain.GenerateSecureToken(32);
        string t2 = SmartAuthorizationGrain.GenerateSecureToken(32);

        Assert.That(t1, Is.Not.EqualTo(t2));
        Assert.That(t1.Length, Is.GreaterThan(0));
    }

    [Test]
    public void HashToken_Deterministic()
    {
        string token = "my-access-token";
        string h1 = SmartAuthorizationGrain.HashToken(token);
        string h2 = SmartAuthorizationGrain.HashToken(token);

        Assert.That(h1, Is.EqualTo(h2));
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private ISmartAuthorizationGrain GetAuthGrain()
    {
        string userId = $"USER-{Guid.NewGuid():N}";
        string clientId = $"CLIENT-{Guid.NewGuid():N}";
        return _cluster.GrainFactory.GetGrain<ISmartAuthorizationGrain>(
            $"SMART-AUTH:{userId}:{clientId}");
    }
}
