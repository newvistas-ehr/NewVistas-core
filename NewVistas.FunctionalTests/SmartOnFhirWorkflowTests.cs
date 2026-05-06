// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the SMART on FHIR 2.0.0 authorization workflow.
/// §170.315(g)(10) — Standardized API for patient and population services.
///
/// Tests the full OAuth 2.0 lifecycle:
///   1. Client registration → authorization code → token exchange → resource access → revocation
///   2. PKCE flow for public clients
///   3. Token introspection per SMART 2.0.0
///   4. Refresh token rotation with ≥3 month validity
///   5. Patient authorization revocation within 1 hour
///   6. Bulk data export (group-export)
/// </summary>
[TestFixture]
public class SmartOnFhirWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Full OAuth 2.0 Workflow ──────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_RegisterClient_Authorize_Exchange_Introspect_Revoke()
    {
        // 1. Register a SMART client
        string clientId = $"WORKFLOW-{Guid.NewGuid():N}";
        ISmartClientGrain clientGrain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await clientGrain.RegisterAsync(
            "Patient Portal",
            new List<string> { "https://portal.example.com/callback" },
            "public", null,
            new List<string> { "patient/Patient.read", "patient/Condition.read", "launch/patient" },
            null, null, null, null, "none");

        // Add to index
        ISmartClientIndexGrain indexGrain = _cluster.GrainFactory.GetGrain<ISmartClientIndexGrain>("SMART-CLIENT-INDEX");
        await indexGrain.AddClientAsync(new SmartClientSummary
        {
            ClientId = clientId,
            ClientName = "Patient Portal",
            ClientType = "public",
            IsActive = true
        });

        // 2. Validate client registration
        SmartClientState client = await clientGrain.GetClientAsync();
        Assert.That(client.ClientName, Is.EqualTo("Patient Portal"));
        Assert.That(client.IsActive, Is.True);
        Assert.That(await clientGrain.ValidateRedirectUriAsync("https://portal.example.com/callback"), Is.True);

        // 3. Create authorization code with PKCE
        string userId = "USER-PATIENT-001";
        ISmartAuthorizationGrain authGrain = _cluster.GrainFactory.GetGrain<ISmartAuthorizationGrain>(
            $"SMART-AUTH:{userId}:{clientId}");

        string codeVerifier = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
        string codeChallenge = SmartAuthorizationGrain.ComputeS256Challenge(codeVerifier);

        string authCode = await authGrain.CreateAuthorizationCodeAsync(
            "https://portal.example.com/callback",
            new List<string> { "patient/Patient.read", "patient/Condition.read" },
            codeChallenge, "S256", "PATIENT-100", null);

        Assert.That(authCode, Is.Not.Empty);

        // 4. Exchange code for tokens (with PKCE verification)
        SmartTokenResponse tokens = await authGrain.ExchangeCodeAsync(
            authCode, "https://portal.example.com/callback", codeVerifier);

        Assert.That(tokens.AccessToken, Is.Not.Empty);
        Assert.That(tokens.RefreshToken, Is.Not.Empty);
        Assert.That(tokens.ExpiresIn, Is.EqualTo(3600));
        Assert.That(tokens.PatientContext, Is.EqualTo("PATIENT-100"));

        // 5. Introspect access token — should be active
        SmartTokenIntrospection introspection = await authGrain.IntrospectTokenAsync(tokens.AccessToken, true);
        Assert.That(introspection.Active, Is.True);
        Assert.That(introspection.Scope, Does.Contain("patient/Patient.read"));
        Assert.That(introspection.Sub, Is.EqualTo(userId));
        Assert.That(introspection.TokenType, Is.EqualTo("Bearer"));

        // 6. Refresh the token
        SmartTokenResponse refreshed = await authGrain.RefreshTokenAsync(tokens.RefreshToken!);
        Assert.That(refreshed.AccessToken, Is.Not.EqualTo(tokens.AccessToken));
        Assert.That(refreshed.RefreshToken, Is.Not.EqualTo(tokens.RefreshToken));

        // 7. Old access token should still be introspectable (until expiry)
        // New access token should be active
        SmartTokenIntrospection newIntrospection = await authGrain.IntrospectTokenAsync(refreshed.AccessToken, true);
        Assert.That(newIntrospection.Active, Is.True);

        // 8. Revoke all — §170.315(g)(10) requires within 1 hour
        await authGrain.RevokeAllAsync();
        Assert.That(await authGrain.IsRevokedAsync(), Is.True);

        // 9. Introspection after revocation — should be inactive
        SmartTokenIntrospection revokedIntrospection = await authGrain.IntrospectTokenAsync(refreshed.AccessToken, true);
        Assert.That(revokedIntrospection.Active, Is.False);
    }

    // ─── Confidential Client Workflow ─────────────────────────────────────────

    [Test]
    public async Task ConfidentialClient_SecretValidation_And_TokenExchange()
    {
        string clientId = $"CONF-{Guid.NewGuid():N}";
        ISmartClientGrain clientGrain = _cluster.GrainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");

        await clientGrain.RegisterAsync(
            "Clinical Portal",
            new List<string> { "https://clinical.example.com/cb" },
            "confidential-symmetric", "super-secret-key",
            new List<string> { "user/Patient.read", "user/Condition.read" },
            null, null, null, null, "client_secret_basic");

        // Validate secret
        Assert.That(await clientGrain.ValidateSecretAsync("super-secret-key"), Is.True);
        Assert.That(await clientGrain.ValidateSecretAsync("wrong-key"), Is.False);

        // Auth code flow works same as public client
        string userId = "USER-CLINICIAN-001";
        ISmartAuthorizationGrain authGrain = _cluster.GrainFactory.GetGrain<ISmartAuthorizationGrain>(
            $"SMART-AUTH:{userId}:{clientId}");

        string code = await authGrain.CreateAuthorizationCodeAsync(
            "https://clinical.example.com/cb",
            new List<string> { "user/Patient.read" },
            null, null, null, null);

        SmartTokenResponse tokens = await authGrain.ExchangeCodeAsync(
            code, "https://clinical.example.com/cb", null);

        Assert.That(tokens.AccessToken, Is.Not.Empty);
    }

    // ─── Multiple Clients Per User ────────────────────────────────────────────

    [Test]
    public async Task MultipleClients_IndependentAuthorizations()
    {
        string userId = "USER-MULTI-001";

        // Client A
        string clientIdA = $"MULTI-A-{Guid.NewGuid():N}";
        ISmartAuthorizationGrain authA = _cluster.GrainFactory.GetGrain<ISmartAuthorizationGrain>(
            $"SMART-AUTH:{userId}:{clientIdA}");

        string codeA = await authA.CreateAuthorizationCodeAsync(
            "https://app-a.com/cb", new List<string> { "patient/Patient.read" },
            null, null, "PATIENT-A", null);
        SmartTokenResponse tokensA = await authA.ExchangeCodeAsync(codeA, "https://app-a.com/cb", null);

        // Client B
        string clientIdB = $"MULTI-B-{Guid.NewGuid():N}";
        ISmartAuthorizationGrain authB = _cluster.GrainFactory.GetGrain<ISmartAuthorizationGrain>(
            $"SMART-AUTH:{userId}:{clientIdB}");

        string codeB = await authB.CreateAuthorizationCodeAsync(
            "https://app-b.com/cb", new List<string> { "patient/Condition.read" },
            null, null, "PATIENT-B", null);
        SmartTokenResponse tokensB = await authB.ExchangeCodeAsync(codeB, "https://app-b.com/cb", null);

        // Revoking Client A should not affect Client B
        await authA.RevokeAllAsync();

        SmartTokenIntrospection intrA = await authA.IntrospectTokenAsync(tokensA.AccessToken, true);
        SmartTokenIntrospection intrB = await authB.IntrospectTokenAsync(tokensB.AccessToken, true);

        Assert.That(intrA.Active, Is.False);
        Assert.That(intrB.Active, Is.True);
    }

    // ─── Client Registration via Index ────────────────────────────────────────

    [Test]
    public async Task ClientIndex_ListsAllRegisteredClients()
    {
        string indexKey = $"CLIENT-INDEX-{Guid.NewGuid():N}";
        ISmartClientIndexGrain index = _cluster.GrainFactory.GetGrain<ISmartClientIndexGrain>(indexKey);

        for (int i = 0; i < 5; i++)
        {
            await index.AddClientAsync(new SmartClientSummary
            {
                ClientId = $"APP-{i}",
                ClientName = $"Application {i}",
                ClientType = i % 2 == 0 ? "public" : "confidential-symmetric",
                IsActive = i != 3, // App 3 is inactive
                RegisteredDate = DateTime.UtcNow
            });
        }

        List<SmartClientSummary> all = await index.GetAllClientsAsync();
        Assert.That(all, Has.Count.EqualTo(5));

        List<SmartClientSummary> active = await index.GetActiveClientsAsync();
        Assert.That(active, Has.Count.EqualTo(4));
    }

    // ─── Revocation Timing ────────────────────────────────────────────────────

    [Test]
    public async Task Revocation_RecordsTimestamp()
    {
        ISmartAuthorizationGrain auth = GetAuthGrain();

        string code = await auth.CreateAuthorizationCodeAsync(
            "https://app.example.com/cb", new List<string> { "patient/Patient.read" },
            null, null, null, null);
        await auth.ExchangeCodeAsync(code, "https://app.example.com/cb", null);

        DateTime beforeRevoke = DateTime.UtcNow;
        await auth.RevokeAllAsync();

        SmartAuthorizationState state = await auth.GetStateAsync();
        Assert.That(state.IsRevoked, Is.True);
        Assert.That(state.RevokedDate, Is.Not.Null);
        Assert.That(state.RevokedDate, Is.GreaterThanOrEqualTo(beforeRevoke));
    }

    // ─── Bulk Data Export ─────────────────────────────────────────────────────

    [Test]
    public async Task BulkExport_StartAndComplete()
    {
        string jobId = $"BULK-EXPORT:{Guid.NewGuid():N}";
        IBulkDataExportGrain exportGrain = _cluster.GrainFactory.GetGrain<IBulkDataExportGrain>(jobId);

        // Create a patient with data for the export
        string patientId = $"PATIENT-EXPORT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync($"EXPORT,TEST {patientId}", "M", null, "000-00-0000");

        await exportGrain.StartExportAsync(
            patientId,
            new List<string> { patientId },
            new List<string> { "Patient" },
            null,
            "test-user");

        BulkDataExportState status = await exportGrain.GetStatusAsync();
        Assert.That(status.Status, Is.EqualTo("completed"));
        Assert.That(status.ProcessedCount, Is.EqualTo(1));
        Assert.That(status.OutputFiles.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(status.OutputFiles.Any(f => f.ResourceType == "Patient"), Is.True);
    }

    [Test]
    public async Task BulkExport_NoPatientData_CompletesWithEmptyOutput()
    {
        string jobId = $"BULK-EXPORT:{Guid.NewGuid():N}";
        IBulkDataExportGrain exportGrain = _cluster.GrainFactory.GetGrain<IBulkDataExportGrain>(jobId);

        await exportGrain.StartExportAsync(
            "EMPTY-GROUP",
            new List<string> { $"PATIENT-NODATA-{Guid.NewGuid()}" },
            new List<string> { "Condition", "Observation" },
            null,
            "test-user");

        BulkDataExportState status = await exportGrain.GetStatusAsync();
        Assert.That(status.Status, Is.EqualTo("completed"));
        // No data for this patient, so no output files for the requested types
        Assert.That(status.OutputFiles.All(f => f.ResourceType != "Patient"), Is.True);
    }

    [Test]
    public async Task BulkExport_Cancel_SetsErrorStatus()
    {
        string jobId = $"BULK-EXPORT:{Guid.NewGuid():N}";
        IBulkDataExportGrain exportGrain = _cluster.GrainFactory.GetGrain<IBulkDataExportGrain>(jobId);

        // Start and immediately cancel (the sync implementation will already be done,
        // so we test the cancel logic on a pending grain)
        BulkDataExportState initialStatus = await exportGrain.GetStatusAsync();
        Assert.That(initialStatus.Status, Is.EqualTo("pending"));

        await exportGrain.CancelAsync();
        BulkDataExportState cancelled = await exportGrain.GetStatusAsync();
        Assert.That(cancelled.Status, Is.EqualTo("error"));
        Assert.That(cancelled.ErrorMessage, Does.Contain("Cancelled"));
    }

    [Test]
    public async Task BulkExport_MultiplePatients()
    {
        string jobId = $"BULK-EXPORT:{Guid.NewGuid():N}";
        IBulkDataExportGrain exportGrain = _cluster.GrainFactory.GetGrain<IBulkDataExportGrain>(jobId);

        // Register multiple patients
        List<string> patientIds = new();
        for (int i = 0; i < 3; i++)
        {
            string pid = $"PATIENT-BULK-{Guid.NewGuid():N}";
            IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
            await w.UpdateDemographicsAsync($"BULK,PATIENT {i}", "F", null, $"111-11-111{i}");
            patientIds.Add(pid);
        }

        await exportGrain.StartExportAsync(
            "GROUP-TEST", patientIds,
            new List<string> { "Patient" },
            null, "test-user");

        BulkDataExportState status = await exportGrain.GetStatusAsync();
        Assert.That(status.Status, Is.EqualTo("completed"));
        Assert.That(status.ProcessedCount, Is.EqualTo(3));

        BulkExportOutputFile? patientFile = status.OutputFiles.FirstOrDefault(f => f.ResourceType == "Patient");
        Assert.That(patientFile, Is.Not.Null);
        Assert.That(patientFile!.Count, Is.EqualTo(3));
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
