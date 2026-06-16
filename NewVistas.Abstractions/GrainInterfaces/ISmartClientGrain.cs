// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// SMART on FHIR Client Registration Grain — manages a registered third-party application.
/// §170.315(g)(10) requires allowing third-party app registration with the authorization server.
///
/// Grain Key: "SMART-CLIENT:{client_id}"
/// </summary>
public interface ISmartClientGrain : IGrainWithStringKey
{
    /// <summary>Register or update a SMART client application.</summary>
    Task RegisterAsync(
        string clientName,
        List<string> redirectUris,
        string clientType,
        string? clientSecret,
        List<string> grantedScopes,
        string? launchUrl,
        string? logoUri,
        string? jwksUri,
        List<string>? contacts,
        string tokenEndpointAuthMethod);

    /// <summary>Get the full client registration state.</summary>
    Task<SmartClientState> GetClientAsync();

    /// <summary>Validate that a redirect URI is registered for this client.</summary>
    Task<bool> ValidateRedirectUriAsync(string redirectUri);

    /// <summary>Validate client credentials (for confidential clients).</summary>
    Task<bool> ValidateSecretAsync(string clientSecret);

    /// <summary>Check if a scope is granted to this client.</summary>
    Task<bool> IsScopeGrantedAsync(string scope);

    /// <summary>Deactivate the client (soft-delete).</summary>
    Task DeactivateAsync();

    /// <summary>Reactivate a deactivated client.</summary>
    Task ReactivateAsync();
}

/// <summary>
/// Index grain for listing all registered SMART clients.
///
/// Grain Key: "SMART-CLIENT-INDEX"
/// </summary>
public interface ISmartClientIndexGrain : IGrainWithStringKey
{
    Task AddClientAsync(SmartClientSummary summary);
    Task RemoveClientAsync(string clientId);
    Task<List<SmartClientSummary>> GetAllClientsAsync();
    Task<List<SmartClientSummary>> GetActiveClientsAsync();
}

/// <summary>
/// SMART OAuth 2.0 Authorization Grain — manages authorization codes, tokens,
/// and revocation for a specific user-client pair.
///
/// §170.315(g)(10) OAuth 2.0 requirements:
///   - Authorization code flow with PKCE
///   - Refresh tokens with ≥3 month validity (confidential clients)
///   - Token introspection (SMART 2.0.0)
///   - Patient authorization revocation within 1 hour
///
/// Grain Key: "SMART-AUTH:{userId}:{clientId}"
/// </summary>
public interface ISmartAuthorizationGrain : IGrainWithStringKey
{
    /// <summary>Create an authorization code for the code flow. Returns the code string.</summary>
    Task<string> CreateAuthorizationCodeAsync(
        string redirectUri,
        List<string> scopes,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? patientContext,
        string? launchContext);

    /// <summary>
    /// Exchange an authorization code for tokens.
    /// Returns (accessToken, refreshToken, expiresIn, scopes, patientContext) or throws if invalid.
    /// </summary>
    Task<SmartTokenResponse> ExchangeCodeAsync(string code, string redirectUri, string? codeVerifier);

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// §170.315(g)(10) requires issuing a new refresh token with ≥3 month validity.
    /// </summary>
    Task<SmartTokenResponse> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Introspect a token — returns whether it's active and its metadata.
    /// Required by SMART App Launch 2.0.0 §170.215(c)(2).
    /// </summary>
    Task<SmartTokenIntrospection> IntrospectTokenAsync(string token, bool isAccessToken);

    /// <summary>
    /// Revoke all tokens for this user-client pair.
    /// §170.315(g)(10) requires revocation within 1 hour of patient request.
    /// </summary>
    Task RevokeAllAsync();

    /// <summary>Revoke a specific refresh token.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken);

    /// <summary>Check if this user-client authorization has been revoked.</summary>
    Task<bool> IsRevokedAsync();

    /// <summary>Get the full authorization state.</summary>
    Task<SmartAuthorizationState> GetStateAsync();
}

/// <summary>
/// Token response from code exchange or refresh operations.
/// </summary>
[GenerateSerializer]
public class SmartTokenResponse
{
    [Id(0)]
    public string AccessToken { get; set; } = string.Empty;

    [Id(1)]
    public string? RefreshToken { get; set; }

    [Id(2)]
    public int ExpiresIn { get; set; } = 3600;

    [Id(3)]
    public string TokenType { get; set; } = "Bearer";

    [Id(4)]
    public string Scope { get; set; } = string.Empty;

    [Id(5)]
    public string? PatientContext { get; set; }
}

/// <summary>
/// Token introspection response per RFC 7662 / SMART 2.0.0.
/// </summary>
[GenerateSerializer]
public class SmartTokenIntrospection
{
    [Id(0)]
    public bool Active { get; set; }

    [Id(1)]
    public string? Scope { get; set; }

    [Id(2)]
    public string? ClientId { get; set; }

    [Id(3)]
    public string? Sub { get; set; }

    [Id(4)]
    public long? Exp { get; set; }

    [Id(5)]
    public long? Iat { get; set; }

    [Id(6)]
    public string? TokenType { get; set; }
}

/// <summary>
/// FHIR Bulk Data Export Grain — manages async group-export jobs.
/// §170.215(d)(1) — FHIR Bulk Data Access (Flat FHIR) v1.0.0.
///
/// Grain Key: "BULK-EXPORT:{jobId}"
/// </summary>
public interface IBulkDataExportGrain : IGrainWithStringKey
{
    /// <summary>Kick off a bulk export job for a group of patients.</summary>
    Task StartExportAsync(
        string groupId,
        List<string> patientIds,
        List<string>? resourceTypes,
        DateTime? since,
        string? requestedBy);

    /// <summary>Get current job status.</summary>
    Task<BulkDataExportState> GetStatusAsync();

    /// <summary>Cancel a running export.</summary>
    Task CancelAsync();
}
