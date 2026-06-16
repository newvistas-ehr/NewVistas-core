// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// SMART on FHIR 2.0.0 Authorization Server — OAuth 2.0 endpoints.
///
/// §170.315(g)(10) — Standardized API for patient and population services.
/// §170.215(c)(2) — SMART App Launch IG Release 2.0.0.
///
/// Implements:
///   - .well-known/smart-configuration (SMART discovery)
///   - /authorize (authorization code flow with PKCE)
///   - /token (code exchange and refresh)
///   - /introspect (token introspection per RFC 7662)
///   - /revoke (token revocation per RFC 7009)
///   - /register (dynamic client registration)
///
/// Required capability sets:
///   - "Patient Access for Standalone Apps"
///   - "Clinician Access for EHR Launch"
/// </summary>
[ApiController]
[Route("api/smart")]
public class SmartAuthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SmartAuthController> _logger;

    public SmartAuthController(IGrainFactory grainFactory, ILogger<SmartAuthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── SMART Discovery ──────────────────────────────────────────────────────

    /// <summary>
    /// SMART on FHIR Well-Known Configuration.
    /// Returns the authorization server's capabilities per SMART App Launch 2.0.0.
    /// This endpoint must be publicly accessible without authentication.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/api/fhir/.well-known/smart-configuration")]
    [Produces("application/json")]
    public IActionResult GetSmartConfiguration()
    {
        string baseUrl = $"{Request.Scheme}://{Request.Host}";

        var config = new
        {
            issuer = baseUrl,
            authorization_endpoint = $"{baseUrl}/api/smart/authorize",
            token_endpoint = $"{baseUrl}/api/smart/token",
            introspection_endpoint = $"{baseUrl}/api/smart/introspect",
            revocation_endpoint = $"{baseUrl}/api/smart/revoke",
            registration_endpoint = $"{baseUrl}/api/smart/register",
            management_endpoint = $"{baseUrl}/api/smart/clients",

            // SMART 2.0.0 required scopes
            scopes_supported = new[]
            {
                "openid", "fhirUser", "profile",
                "launch", "launch/patient",
                "patient/Patient.read",
                "patient/Condition.read",
                "patient/AllergyIntolerance.read",
                "patient/Observation.read",
                "patient/MedicationRequest.read",
                "patient/DiagnosticReport.read",
                "patient/Encounter.read",
                "patient/Appointment.read",
                "patient/DocumentReference.read",
                "patient/Immunization.read",
                "patient/Procedure.read",
                "patient/CarePlan.read",
                "patient/CareTeam.read",
                "patient/Goal.read",
                "patient/Coverage.read",
                "user/Patient.read",
                "user/Condition.read",
                "user/AllergyIntolerance.read",
                "user/Observation.read",
                "user/MedicationRequest.read",
                "user/DiagnosticReport.read",
                "user/Encounter.read",
                "user/Appointment.read",
                "system/Patient.read",
                "system/Condition.read",
                "system/AllergyIntolerance.read",
                "system/Observation.read",
                "system/*.read"
            },

            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },

            // SMART 2.0.0 capability sets
            capabilities = new[]
            {
                "launch-ehr",
                "launch-standalone",
                "client-public",
                "client-confidential-symmetric",
                "client-confidential-asymmetric",
                "context-ehr-patient",
                "context-standalone-patient",
                "permission-offline",
                "permission-patient",
                "permission-user",
                "sso-openid-connect"
            },

            token_endpoint_auth_methods_supported = new[]
            {
                "client_secret_basic",
                "client_secret_post",
                "private_key_jwt",
                "none"
            },

            code_challenge_methods_supported = new[] { "S256" },

            // SMART 2.0.0 required fields
            token_endpoint_auth_signing_alg_values_supported = new[] { "RS256", "ES256" }
        };

        return Ok(config);
    }

    // ─── Authorization Endpoint ───────────────────────────────────────────────

    /// <summary>
    /// OAuth 2.0 Authorization Endpoint — initiates the authorization code flow.
    /// Supports PKCE (S256) as required by SMART 2.0.0 for public clients.
    /// </summary>
    [Authorize]
    [HttpGet("authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string scope,
        [FromQuery] string? state,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method,
        [FromQuery] string? launch,
        [FromQuery] string? aud)
    {
        try
        {
            if (response_type != "code")
                return BadRequest(new { error = "unsupported_response_type" });

            // Validate client
            ISmartClientGrain clientGrain = _grainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{client_id}");
            SmartClientState client = await clientGrain.GetClientAsync();

            if (string.IsNullOrEmpty(client.ClientName) || !client.IsActive)
                return BadRequest(new { error = "invalid_client", error_description = "Unknown or inactive client." });

            if (!await clientGrain.ValidateRedirectUriAsync(redirect_uri))
                return BadRequest(new { error = "invalid_request", error_description = "Redirect URI not registered." });

            // Public clients MUST use PKCE
            if (client.ClientType == "public" && string.IsNullOrEmpty(code_challenge))
                return BadRequest(new { error = "invalid_request", error_description = "PKCE required for public clients." });

            if (!string.IsNullOrEmpty(code_challenge) && code_challenge_method != "S256")
                return BadRequest(new { error = "invalid_request", error_description = "Only S256 code_challenge_method is supported." });

            // Parse scopes and validate
            List<string> requestedScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Get user from JWT
            string userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? "anonymous";

            // Determine patient context (from launch param or scope)
            string? patientContext = null;
            if (!string.IsNullOrEmpty(launch))
                patientContext = launch; // EHR launch provides patient context

            // Create authorization code
            ISmartAuthorizationGrain authGrain = _grainFactory.GetGrain<ISmartAuthorizationGrain>(
                $"SMART-AUTH:{userId}:{client_id}");

            string code = await authGrain.CreateAuthorizationCodeAsync(
                redirect_uri, requestedScopes, code_challenge, code_challenge_method,
                patientContext, launch);

            // Redirect back to client with code
            string redirectUrl = $"{redirect_uri}?code={Uri.EscapeDataString(code)}";
            if (!string.IsNullOrEmpty(state))
                redirectUrl += $"&state={Uri.EscapeDataString(state)}";

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMART authorize error for client {ClientId}", client_id);
            return BadRequest(new { error = "server_error", error_description = "Authorization failed." });
        }
    }

    // ─── Token Endpoint ───────────────────────────────────────────────────────

    /// <summary>
    /// OAuth 2.0 Token Endpoint — exchanges authorization codes for tokens
    /// and handles refresh token grants.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token([FromForm] SmartTokenRequest request)
    {
        try
        {
            if (request.grant_type == "authorization_code")
                return await HandleAuthorizationCodeGrant(request);

            if (request.grant_type == "refresh_token")
                return await HandleRefreshTokenGrant(request);

            return BadRequest(new { error = "unsupported_grant_type" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("SMART token error: {Message}", ex.Message);
            return BadRequest(new { error = "invalid_grant", error_description = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMART token endpoint error");
            return StatusCode(500, new { error = "server_error" });
        }
    }

    private async Task<IActionResult> HandleAuthorizationCodeGrant(SmartTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.code) || string.IsNullOrEmpty(request.redirect_uri))
            return BadRequest(new { error = "invalid_request", error_description = "code and redirect_uri required." });

        // Validate client credentials for confidential clients
        if (!string.IsNullOrEmpty(request.client_secret))
        {
            ISmartClientGrain clientGrain = _grainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{request.client_id}");
            if (!await clientGrain.ValidateSecretAsync(request.client_secret))
                return Unauthorized(new { error = "invalid_client" });
        }

        // Find the authorization grain — we need the userId from the code
        // For simplicity, the client_id is in the request; we scan for matching codes
        // In production, the code would encode the auth grain key
        ISmartAuthorizationGrain authGrain = await FindAuthGrainByCodeAsync(request.client_id, request.code);

        SmartTokenResponse tokenResponse = await authGrain.ExchangeCodeAsync(
            request.code, request.redirect_uri, request.code_verifier);

        var response = new
        {
            access_token = tokenResponse.AccessToken,
            token_type = tokenResponse.TokenType,
            expires_in = tokenResponse.ExpiresIn,
            scope = tokenResponse.Scope,
            refresh_token = tokenResponse.RefreshToken,
            patient = tokenResponse.PatientContext
        };

        return Ok(response);
    }

    private async Task<IActionResult> HandleRefreshTokenGrant(SmartTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.refresh_token))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token required." });

        // Validate client for confidential clients
        if (!string.IsNullOrEmpty(request.client_secret))
        {
            ISmartClientGrain clientGrain = _grainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{request.client_id}");
            if (!await clientGrain.ValidateSecretAsync(request.client_secret))
                return Unauthorized(new { error = "invalid_client" });
        }

        ISmartAuthorizationGrain authGrain = await FindAuthGrainByRefreshTokenAsync(
            request.client_id, request.refresh_token);

        SmartTokenResponse tokenResponse = await authGrain.RefreshTokenAsync(request.refresh_token);

        return Ok(new
        {
            access_token = tokenResponse.AccessToken,
            token_type = tokenResponse.TokenType,
            expires_in = tokenResponse.ExpiresIn,
            scope = tokenResponse.Scope,
            refresh_token = tokenResponse.RefreshToken,
            patient = tokenResponse.PatientContext
        });
    }

    // ─── Token Introspection (SMART 2.0.0 / RFC 7662) ────────────────────────

    /// <summary>
    /// Token Introspection Endpoint — validates tokens per SMART App Launch 2.0.0.
    /// §170.215(c)(2) requires token introspection support.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("introspect")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Introspect([FromForm] string token, [FromForm] string? token_type_hint)
    {
        try
        {
            bool isAccessToken = token_type_hint != "refresh_token";

            // Try to find the authorization that issued this token
            // In production, tokens would encode the auth grain key
            SmartTokenIntrospection result = await FindAndIntrospectTokenAsync(token, isAccessToken);

            return Ok(new
            {
                active = result.Active,
                scope = result.Scope,
                client_id = result.ClientId,
                sub = result.Sub,
                exp = result.Exp,
                iat = result.Iat,
                token_type = result.TokenType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token introspection error");
            return Ok(new { active = false });
        }
    }

    // ─── Token Revocation (RFC 7009) ──────────────────────────────────────────

    /// <summary>
    /// Token Revocation Endpoint — §170.315(g)(10) requires revocation within 1 hour.
    /// </summary>
    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromForm] string client_id)
    {
        try
        {
            string userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? string.Empty;

            ISmartAuthorizationGrain authGrain = _grainFactory.GetGrain<ISmartAuthorizationGrain>(
                $"SMART-AUTH:{userId}:{client_id}");

            await authGrain.RevokeAllAsync();

            _logger.LogInformation("SMART authorization revoked for user {UserId}, client {ClientId}",
                userId, client_id);

            return Ok(new { message = "Authorization revoked." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token revocation error");
            return StatusCode(500, new { error = "server_error" });
        }
    }

    // ─── Client Registration ──────────────────────────────────────────────────

    /// <summary>
    /// Dynamic Client Registration — allows third-party apps to register.
    /// §170.315(g)(10) requires app registration without preconditions.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterClient([FromBody] SmartClientRegistrationRequest request)
    {
        try
        {
            string clientId = Guid.NewGuid().ToString("N")[..16];

            // Determine auth method and generate secret for confidential clients
            string? clientSecret = null;
            string tokenEndpointAuthMethod = request.TokenEndpointAuthMethod ?? "none";

            if (request.ClientType?.StartsWith("confidential") == true &&
                tokenEndpointAuthMethod is "client_secret_basic" or "client_secret_post")
            {
                clientSecret = Guid.NewGuid().ToString("N");
            }

            ISmartClientGrain clientGrain = _grainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");
            await clientGrain.RegisterAsync(
                clientName: request.ClientName,
                redirectUris: request.RedirectUris ?? new(),
                clientType: request.ClientType ?? "public",
                clientSecret: clientSecret,
                grantedScopes: request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new(),
                launchUrl: request.LaunchUrl,
                logoUri: request.LogoUri,
                jwksUri: request.JwksUri,
                contacts: request.Contacts,
                tokenEndpointAuthMethod: tokenEndpointAuthMethod);

            // Add to index
            ISmartClientIndexGrain indexGrain = _grainFactory.GetGrain<ISmartClientIndexGrain>("SMART-CLIENT-INDEX");
            await indexGrain.AddClientAsync(new SmartClientSummary
            {
                ClientId = clientId,
                ClientName = request.ClientName,
                ClientType = request.ClientType ?? "public",
                IsActive = true,
                RegisteredDate = DateTime.UtcNow
            });

            _logger.LogInformation("SMART client registered: {ClientId} ({ClientName})", clientId, request.ClientName);

            var response = new
            {
                client_id = clientId,
                client_name = request.ClientName,
                client_secret = clientSecret, // only returned once for confidential clients
                redirect_uris = request.RedirectUris,
                client_type = request.ClientType ?? "public",
                token_endpoint_auth_method = tokenEndpointAuthMethod,
                scope = request.Scope,
                registration_date = DateTime.UtcNow.ToString("O")
            };

            return Created($"/api/smart/clients/{clientId}", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMART client registration error");
            return StatusCode(500, new { error = "server_error" });
        }
    }

    /// <summary>List registered SMART clients.</summary>
    [Authorize(Roles = "Administrator")]
    [HttpGet("clients")]
    public async Task<IActionResult> ListClients()
    {
        try
        {
            ISmartClientIndexGrain indexGrain = _grainFactory.GetGrain<ISmartClientIndexGrain>("SMART-CLIENT-INDEX");
            List<SmartClientSummary> clients = await indexGrain.GetAllClientsAsync();
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing SMART clients");
            return StatusCode(500, new { error = "server_error" });
        }
    }

    /// <summary>Get a specific client's registration details.</summary>
    [Authorize(Roles = "Administrator")]
    [HttpGet("clients/{clientId}")]
    public async Task<IActionResult> GetClient(string clientId)
    {
        try
        {
            ISmartClientGrain clientGrain = _grainFactory.GetGrain<ISmartClientGrain>($"SMART-CLIENT:{clientId}");
            SmartClientState client = await clientGrain.GetClientAsync();

            if (string.IsNullOrEmpty(client.ClientName))
                return NotFound(new { error = "Client not found." });

            return Ok(new
            {
                client_id = client.ClientId,
                client_name = client.ClientName,
                redirect_uris = client.RedirectUris,
                client_type = client.ClientType,
                granted_scopes = client.GrantedScopes,
                is_active = client.IsActive,
                registered_date = client.RegisteredDate,
                token_endpoint_auth_method = client.TokenEndpointAuthMethod
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SMART client {ClientId}", clientId);
            return StatusCode(500, new { error = "server_error" });
        }
    }

    // ─── Bulk Data Export ─────────────────────────────────────────────────────

    /// <summary>
    /// FHIR Bulk Data Export — kick off group-export per §170.215(d).
    /// POST /api/fhir/Group/{groupId}/$export
    /// </summary>
    [Authorize]
    [HttpPost("/api/fhir/Group/{groupId}/$export")]
    public async Task<IActionResult> StartBulkExport(
        string groupId,
        [FromQuery(Name = "_type")] string? resourceTypes,
        [FromQuery(Name = "_since")] DateTime? since)
    {
        try
        {
            // In production, the group would resolve to a patient panel/list
            // For now, the groupId is a comma-separated list of patient IDs or a panel name
            List<string> patientIds = groupId.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            string jobId = $"BULK-EXPORT:{Guid.NewGuid():N}";
            List<string>? types = resourceTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            string userId = User.FindFirst("sub")?.Value ?? "system";

            IBulkDataExportGrain exportGrain = _grainFactory.GetGrain<IBulkDataExportGrain>(jobId);
            await exportGrain.StartExportAsync(groupId, patientIds, types, since, userId);

            // Return 202 Accepted with Content-Location header per Bulk Data spec
            string statusUrl = $"{Request.Scheme}://{Request.Host}/api/fhir/bulk-export/{Uri.EscapeDataString(jobId)}";
            Response.Headers["Content-Location"] = statusUrl;
            return Accepted(statusUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk export error for group {GroupId}", groupId);
            return StatusCode(500, new { error = "server_error" });
        }
    }

    /// <summary>
    /// Bulk Data Export status — GET /api/fhir/bulk-export/{jobId}
    /// Returns 202 if still processing, 200 with output file manifest if complete.
    /// </summary>
    [Authorize]
    [HttpGet("/api/fhir/bulk-export/{jobId}")]
    public async Task<IActionResult> GetBulkExportStatus(string jobId)
    {
        try
        {
            IBulkDataExportGrain exportGrain = _grainFactory.GetGrain<IBulkDataExportGrain>(jobId);
            BulkDataExportState status = await exportGrain.GetStatusAsync();

            if (string.IsNullOrEmpty(status.GroupId))
                return NotFound(new { error = "Export job not found." });

            if (status.Status == "in-progress" || status.Status == "pending")
            {
                Response.Headers["X-Progress"] = $"{status.ProcessedCount} patients processed";
                Response.Headers["Retry-After"] = "10";
                return StatusCode(202);
            }

            if (status.Status == "error")
                return StatusCode(500, new { error = status.ErrorMessage });

            // Completed — return manifest per Bulk Data spec
            return Ok(new
            {
                transactionTime = status.CompletedDate?.ToString("O"),
                request = $"/api/fhir/Group/{status.GroupId}/$export",
                requiresAccessToken = true,
                output = status.OutputFiles.Select(f => new
                {
                    type = f.ResourceType,
                    url = f.Url,
                    count = f.Count
                }),
                error = Array.Empty<object>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk export status error for {JobId}", jobId);
            return StatusCode(500, new { error = "server_error" });
        }
    }

    /// <summary>Cancel a bulk export job.</summary>
    [Authorize]
    [HttpDelete("/api/fhir/bulk-export/{jobId}")]
    public async Task<IActionResult> CancelBulkExport(string jobId)
    {
        try
        {
            IBulkDataExportGrain exportGrain = _grainFactory.GetGrain<IBulkDataExportGrain>(jobId);
            await exportGrain.CancelAsync();
            return StatusCode(202);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk export cancel error for {JobId}", jobId);
            return StatusCode(500, new { error = "server_error" });
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Find the authorization grain that contains a given authorization code.
    /// In production, codes would encode the grain key for O(1) lookup.
    /// For this implementation, we use a convention where the client stores
    /// a mapping from codes to user IDs.
    /// </summary>
    private Task<ISmartAuthorizationGrain> FindAuthGrainByCodeAsync(string clientId, string code)
    {
        // For the demo/test implementation, we decode the userId from the JWT
        // that was used during the authorize step. The client_id narrows the search.
        // Since codes are created per-user, the token request must include client auth
        // or the code itself implicitly identifies the user.

        // Simplified: use the code as a lookup in a well-known auth grain.
        // The authorize endpoint embeds userId in the grain key, so the token endpoint
        // needs the userId. In SMART, the code exchange typically happens server-to-server
        // after the redirect, so we store a code→userId mapping.
        // For now, we use the authenticated user or fall back to checking all known authorizations.

        string? userId = User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(
                _grainFactory.GetGrain<ISmartAuthorizationGrain>($"SMART-AUTH:{userId}:{clientId}"));
        }

        // Fallback: the token endpoint is AllowAnonymous, so check via client_id
        // In production, use a code-to-grain index
        throw new InvalidOperationException("Unable to identify user for authorization code exchange.");
    }

    private Task<ISmartAuthorizationGrain> FindAuthGrainByRefreshTokenAsync(string clientId, string refreshToken)
    {
        string? userId = User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(
                _grainFactory.GetGrain<ISmartAuthorizationGrain>($"SMART-AUTH:{userId}:{clientId}"));
        }

        throw new InvalidOperationException("Unable to identify user for token refresh.");
    }

    private async Task<SmartTokenIntrospection> FindAndIntrospectTokenAsync(string token, bool isAccessToken)
    {
        // In production, tokens would embed the auth grain key for O(1) lookup.
        // For this implementation, the introspect caller must authenticate.
        string? userId = User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return new SmartTokenIntrospection { Active = false };

        // Check all known client authorizations for this user
        ISmartClientIndexGrain indexGrain = _grainFactory.GetGrain<ISmartClientIndexGrain>("SMART-CLIENT-INDEX");
        List<SmartClientSummary> clients = await indexGrain.GetAllClientsAsync();

        foreach (SmartClientSummary client in clients)
        {
            ISmartAuthorizationGrain authGrain = _grainFactory.GetGrain<ISmartAuthorizationGrain>(
                $"SMART-AUTH:{userId}:{client.ClientId}");

            SmartTokenIntrospection result = await authGrain.IntrospectTokenAsync(token, isAccessToken);
            if (result.Active)
                return result;
        }

        return new SmartTokenIntrospection { Active = false };
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public class SmartTokenRequest
{
    public string grant_type { get; set; } = string.Empty;
    public string? code { get; set; }
    public string? redirect_uri { get; set; }
    public string client_id { get; set; } = string.Empty;
    public string? client_secret { get; set; }
    public string? code_verifier { get; set; }
    public string? refresh_token { get; set; }
    public string? scope { get; set; }
}

public record SmartClientRegistrationRequest
{
    public string ClientName { get; init; } = string.Empty;
    public List<string>? RedirectUris { get; init; }
    public string? ClientType { get; init; }
    public string? Scope { get; init; }
    public string? LaunchUrl { get; init; }
    public string? LogoUri { get; init; }
    public string? JwksUri { get; init; }
    public List<string>? Contacts { get; init; }
    public string? TokenEndpointAuthMethod { get; init; }
}
