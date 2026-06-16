// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a registered SMART on FHIR application (OAuth 2.0 client).
/// §170.315(g)(10) requires third-party app registration with the authorization server.
/// Registration must be publicly documented without preconditions.
///
/// Grain Key: "SMART-CLIENT:{client_id}"
/// </summary>
[GenerateSerializer]
public class SmartClientState
{
    /// <summary>OAuth 2.0 client_id — unique application identifier.</summary>
    [Id(0)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Human-readable application name.</summary>
    [Id(1)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Allowed redirect URIs for the authorization code flow.</summary>
    [Id(2)]
    public List<string> RedirectUris { get; set; } = new();

    /// <summary>
    /// Client type per SMART App Launch 2.0.0:
    /// "confidential-symmetric" — server-side app with client_secret
    /// "confidential-asymmetric" — server-side app with private_key_jwt
    /// "public" — native/SPA app, no secret (PKCE required)
    /// </summary>
    [Id(3)]
    public string ClientType { get; set; } = "public";

    /// <summary>Hashed client secret (for confidential-symmetric clients only).</summary>
    [Id(4)]
    public string? ClientSecretHash { get; set; }

    /// <summary>Granted FHIR scopes (e.g., "patient/Patient.read", "launch/patient").</summary>
    [Id(5)]
    public List<string> GrantedScopes { get; set; } = new();

    /// <summary>SMART launch URL (for EHR-launch apps).</summary>
    [Id(6)]
    public string? LaunchUrl { get; set; }

    /// <summary>Application logo URI for consent screen.</summary>
    [Id(7)]
    public string? LogoUri { get; set; }

    /// <summary>Whether this client is active (can request tokens).</summary>
    [Id(8)]
    public bool IsActive { get; set; } = true;

    /// <summary>When the client was registered.</summary>
    [Id(9)]
    public DateTime RegisteredDate { get; set; } = DateTime.UtcNow;

    [Id(10)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>JWKS URI for confidential-asymmetric clients (public key verification).</summary>
    [Id(11)]
    public string? JwksUri { get; set; }

    /// <summary>Contacts for the application developer.</summary>
    [Id(12)]
    public List<string> Contacts { get; set; } = new();

    /// <summary>Token endpoint auth method: "client_secret_basic", "client_secret_post", "private_key_jwt", "none".</summary>
    [Id(13)]
    public string TokenEndpointAuthMethod { get; set; } = "none";
}

/// <summary>
/// State for SMART OAuth 2.0 authorization — tracks authorization codes, tokens, and revocations
/// for a specific user-client pair.
///
/// Grain Key: "SMART-AUTH:{userId}:{clientId}"
/// </summary>
[GenerateSerializer]
public class SmartAuthorizationState
{
    /// <summary>User who granted authorization.</summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>SMART client that received authorization.</summary>
    [Id(1)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Scopes approved by the user.</summary>
    [Id(2)]
    public List<string> ApprovedScopes { get; set; } = new();

    /// <summary>
    /// Pending authorization codes — short-lived (5 min), single-use.
    /// Key: authorization code, Value: code metadata.
    /// </summary>
    [Id(3)]
    public List<SmartAuthorizationCode> PendingCodes { get; set; } = new();

    /// <summary>Active refresh tokens. §170.315(g)(10) requires ≥3 month validity.</summary>
    [Id(4)]
    public List<SmartRefreshToken> RefreshTokens { get; set; } = new();

    /// <summary>Active access tokens (for introspection).</summary>
    [Id(5)]
    public List<SmartAccessToken> AccessTokens { get; set; } = new();

    /// <summary>Whether the user has revoked this client's access entirely.</summary>
    [Id(6)]
    public bool IsRevoked { get; set; }

    /// <summary>When the user revoked access (must take effect within 1 hour per §170.315(g)(10)).</summary>
    [Id(7)]
    public DateTime? RevokedDate { get; set; }

    /// <summary>Patient context (patient/*.read scopes are bound to this patient).</summary>
    [Id(8)]
    public string? PatientContext { get; set; }

    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Short-lived authorization code for the OAuth 2.0 authorization code flow.
/// </summary>
[GenerateSerializer]
public class SmartAuthorizationCode
{
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    [Id(1)]
    public string RedirectUri { get; set; } = string.Empty;

    [Id(2)]
    public List<string> Scopes { get; set; } = new();

    /// <summary>PKCE code_challenge (SHA-256 hash of code_verifier).</summary>
    [Id(3)]
    public string? CodeChallenge { get; set; }

    [Id(4)]
    public string? CodeChallengeMethod { get; set; }

    [Id(5)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>5-minute expiration per SMART spec.</summary>
    [Id(6)]
    public DateTime ExpiresDate { get; set; } = DateTime.UtcNow.AddMinutes(5);

    [Id(7)]
    public bool IsUsed { get; set; }

    /// <summary>Patient context selected during authorization.</summary>
    [Id(8)]
    public string? PatientContext { get; set; }

    /// <summary>SMART launch context (for EHR launch flow).</summary>
    [Id(9)]
    public string? LaunchContext { get; set; }
}

/// <summary>
/// OAuth 2.0 refresh token — §170.315(g)(10) requires ≥3 month validity for confidential clients.
/// </summary>
[GenerateSerializer]
public class SmartRefreshToken
{
    [Id(0)]
    public string TokenHash { get; set; } = string.Empty;

    [Id(1)]
    public List<string> Scopes { get; set; } = new();

    [Id(2)]
    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    /// <summary>≥3 months for confidential clients per §170.315(g)(10).</summary>
    [Id(3)]
    public DateTime ExpiresDate { get; set; } = DateTime.UtcNow.AddMonths(3);

    [Id(4)]
    public bool IsRevoked { get; set; }

    [Id(5)]
    public string? PatientContext { get; set; }
}

/// <summary>
/// OAuth 2.0 access token metadata — stored for token introspection per SMART 2.0.0.
/// </summary>
[GenerateSerializer]
public class SmartAccessToken
{
    [Id(0)]
    public string TokenHash { get; set; } = string.Empty;

    [Id(1)]
    public List<string> Scopes { get; set; } = new();

    [Id(2)]
    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    [Id(3)]
    public DateTime ExpiresDate { get; set; } = DateTime.UtcNow.AddMinutes(60);

    [Id(4)]
    public bool IsRevoked { get; set; }

    [Id(5)]
    public string? PatientContext { get; set; }

    [Id(6)]
    public string? UserId { get; set; }

    [Id(7)]
    public string? ClientId { get; set; }
}

/// <summary>
/// State for a FHIR Bulk Data Export job — async group-export per §170.215(d).
///
/// Grain Key: "BULK-EXPORT:{jobId}"
/// </summary>
[GenerateSerializer]
public class BulkDataExportState
{
    [Id(0)]
    public string JobId { get; set; } = string.Empty;

    /// <summary>Group ID being exported (patient list/panel).</summary>
    [Id(1)]
    public string GroupId { get; set; } = string.Empty;

    /// <summary>FHIR resource types requested (e.g., "Patient,Condition,Observation").</summary>
    [Id(2)]
    public List<string> ResourceTypes { get; set; } = new();

    /// <summary>Export status: "pending", "in-progress", "completed", "error".</summary>
    [Id(3)]
    public string Status { get; set; } = "pending";

    /// <summary>When the export was requested.</summary>
    [Id(4)]
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    /// <summary>When the export completed (or errored).</summary>
    [Id(5)]
    public DateTime? CompletedDate { get; set; }

    /// <summary>Completed NDJSON output file references (resourceType → content).</summary>
    [Id(6)]
    public List<BulkExportOutputFile> OutputFiles { get; set; } = new();

    /// <summary>Error message if the export failed.</summary>
    [Id(7)]
    public string? ErrorMessage { get; set; }

    /// <summary>Since parameter — only export resources modified after this date.</summary>
    [Id(8)]
    public DateTime? Since { get; set; }

    /// <summary>Patient IDs in the group being exported.</summary>
    [Id(9)]
    public List<string> PatientIds { get; set; } = new();

    /// <summary>Number of resources processed so far.</summary>
    [Id(10)]
    public int ProcessedCount { get; set; }

    /// <summary>User/system that initiated the export.</summary>
    [Id(11)]
    public string? RequestedBy { get; set; }
}

/// <summary>
/// A single NDJSON output file from a bulk export job.
/// </summary>
[GenerateSerializer]
public class BulkExportOutputFile
{
    [Id(0)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>URL to download the NDJSON file.</summary>
    [Id(1)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Number of resources in this file.</summary>
    [Id(2)]
    public int Count { get; set; }
}

/// <summary>
/// Index entry for SMART client registration listing.
/// </summary>
[GenerateSerializer]
public class SmartClientSummary
{
    [Id(0)]
    public string ClientId { get; set; } = string.Empty;

    [Id(1)]
    public string ClientName { get; set; } = string.Empty;

    [Id(2)]
    public string ClientType { get; set; } = string.Empty;

    [Id(3)]
    public bool IsActive { get; set; }

    [Id(4)]
    public DateTime RegisteredDate { get; set; }
}

/// <summary>
/// Index state for listing all registered SMART clients.
///
/// Grain Key: "SMART-CLIENT-INDEX"
/// </summary>
[GenerateSerializer]
public class SmartClientIndexState
{
    [Id(0)]
    public List<SmartClientSummary> Clients { get; set; } = new();
}
