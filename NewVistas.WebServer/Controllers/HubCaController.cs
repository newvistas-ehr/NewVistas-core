// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure.Federation;
using Orleans;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Hub-CA endpoints for spoke onboarding. Hosted only on clusters configured
/// as the federation hub (<c>Federation:HubCa:Enabled=true</c>); on
/// non-hub deployments every endpoint here returns 404.
///
/// Two endpoints, two auth schemes:
/// <list type="bullet">
///   <item><description>
///     <c>POST admin/provisioning-token</c> — JWT, Administrator role.
///     Generates a one-time bootstrap token bound to a target cluster id.
///   </description></item>
///   <item><description>
///     <c>POST csr</c> — bootstrap token (Bearer header). Spoke posts a
///     PKCS#10 CSR; hub validates token + CSR, signs the cert, returns it
///     plus the hub-CA's own root cert for trust-anchor distribution.
///   </description></item>
/// </list>
/// </summary>
[ApiController]
[Route("api/federation")]
[Produces("application/json")]
public sealed class HubCaController : ControllerBase
{
    private readonly IHubCertificateAuthority? _ca;
    private readonly IGrainFactory _grainFactory;
    private readonly HubCaOptions _options;
    private readonly ILogger<HubCaController> _logger;

    public HubCaController(
        IServiceProvider services,
        IGrainFactory grainFactory,
        IOptions<HubCaOptions> options,
        ILogger<HubCaController> logger)
    {
        // Optional service: present only when the hub-CA is enabled. We
        // resolve it via IServiceProvider so the controller can construct
        // on non-hub deployments and 404 cleanly.
        _ca = services.GetService<IHubCertificateAuthority>();
        _grainFactory = grainFactory;
        _options = options.Value;
        _logger = logger;
    }

    private bool HubCaEnabled => _options.Enabled && _ca is not null;

    /// <summary>
    /// Generate a one-time bootstrap token that authorizes one CSR for the
    /// specified cluster id. Operator copies this token to the spoke
    /// out-of-band; spoke uses it once on <c>POST csr</c>.
    /// </summary>
    [HttpPost("admin/provisioning-token")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(IssueProvisioningTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IssueProvisioningTokenResponse>> IssueProvisioningToken(
        [FromBody] IssueProvisioningTokenRequest request)
    {
        if (!HubCaEnabled) return NotFound();
        if (request is null || string.IsNullOrWhiteSpace(request.ClusterId))
            return BadRequest("ClusterId is required.");

        int validityHours = request.ValidityHours ?? _options.ProvisioningTokenValidityHours;
        if (validityHours <= 0 || validityHours > 168)  // cap at 7 days
            return BadRequest("ValidityHours must be between 1 and 168.");

        string token = GenerateToken();
        DateTime issuedUtc = DateTime.UtcNow;
        DateTime expiresUtc = issuedUtc.AddHours(validityHours);

        IProvisioningTokenGrain grain = _grainFactory.GetGrain<IProvisioningTokenGrain>(token);
        await grain.IssueAsync(request.ClusterId, expiresUtc);

        // Best-effort: append to the dashboard index. Failure here doesn't
        // fail the issuance — the per-token grain above is the source of
        // truth for the bootstrap flow. The dashboard might miss this entry
        // if the index update fails, but the token still works.
        try
        {
            IProvisioningTokenIndexGrain index =
                _grainFactory.GetGrain<IProvisioningTokenIndexGrain>(IProvisioningTokenIndexGrain.GlobalKey);
            await index.AddAsync(token, request.ClusterId, issuedUtc, expiresUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Token issued but index update failed; dashboard will miss this entry until reconciled.");
        }

        _logger.LogInformation(
            "Provisioning token issued for cluster {ClusterId}, expires {ExpiresUtc:O}",
            request.ClusterId, expiresUtc);

        return Ok(new IssueProvisioningTokenResponse(token, expiresUtc));
    }

    /// <summary>
    /// Sign a CSR for a spoke. Bearer auth: the bootstrap token from the
    /// admin endpoint goes in <c>Authorization: Bearer ...</c>. Token and CSR
    /// must agree on the cluster id (the CSR's CN must match the token's
    /// bound cluster).
    /// </summary>
    [HttpPost("csr")]
    [AllowAnonymous]  // bootstrap-token auth, validated inline below
    [ProducesResponseType(typeof(IssueCertificateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IssueCertificateResponse>> IssueCertificate(
        [FromBody] IssueCertificateRequest request)
    {
        if (!HubCaEnabled) return NotFound();
        if (request is null || string.IsNullOrWhiteSpace(request.CsrPem))
            return BadRequest("CsrPem is required.");

        string? token = ExtractBearerToken(Request.Headers["Authorization"]);
        if (string.IsNullOrEmpty(token))
            return Unauthorized("Bootstrap token required.");

        // Parse the CSR first so we can extract the CN before consuming the
        // token. Failed CSR parsing means a bad request, not a token issue.
        byte[] csrDer;
        string? csrCommonName;
        try
        {
            csrDer = LoadCsrDer(request.CsrPem);
            CertificateRequest parsed = CertificateRequest.LoadSigningRequest(
                csrDer, HashAlgorithmName.SHA256, CertificateRequestLoadOptions.Default);
            csrCommonName = GetCommonName(parsed.SubjectName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inbound CSR failed to parse.");
            return BadRequest($"Invalid CSR: {ex.Message}");
        }

        if (string.IsNullOrEmpty(csrCommonName))
            return BadRequest("CSR subject must include a Common Name (CN=clusterId).");

        // Consume the token. The grain validates that the requested cluster
        // id matches the token's bound cluster — handles typos and tampered
        // CSRs in one place.
        IProvisioningTokenGrain grain = _grainFactory.GetGrain<IProvisioningTokenGrain>(token);

        X509Certificate2 leaf;
        try
        {
            leaf = _ca!.IssueCertificate(csrDer, TimeSpan.FromDays(_options.IssuedCertValidityDays));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub-CA failed to issue cert for {ClusterId}.", csrCommonName);
            return BadRequest($"Cert issuance failed: {ex.Message}");
        }

        try
        {
            await grain.ConsumeAsync(csrCommonName, leaf.Thumbprint);
        }
        catch (InvalidOperationException ex)
        {
            // Token was bad after all (expired, consumed, mismatch). Discard the cert.
            leaf.Dispose();
            _logger.LogWarning("Provisioning token rejected for cluster {ClusterId}: {Reason}",
                csrCommonName, ex.Message);
            return Unauthorized(ex.Message);
        }

        // Best-effort: reflect the consumption in the dashboard index.
        try
        {
            IProvisioningTokenIndexGrain index =
                _grainFactory.GetGrain<IProvisioningTokenIndexGrain>(IProvisioningTokenIndexGrain.GlobalKey);
            await index.MarkConsumedAsync(token, DateTime.UtcNow, leaf.Thumbprint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cert issued but index consume-update failed; dashboard may show this token as still pending.");
        }

        var response = new IssueCertificateResponse(
            CertPem: ExportCertPem(leaf),
            CaCertPem: ExportCertPem(_ca!.RootCertificate));
        leaf.Dispose();

        _logger.LogInformation(
            "Hub-CA issued cert {Thumbprint} for cluster {ClusterId} (token consumed).",
            response.CertPem.Length > 0 ? "(redacted)" : "(empty)", csrCommonName);

        return Ok(response);
    }

    /// <summary>
    /// Renew an existing spoke cert. The caller authenticates via mTLS (its
    /// current cert), and the new cert is issued for the same cluster id —
    /// the authenticated identity's <c>Name</c> claim. No bootstrap token
    /// involved; the existing cert <i>is</i> the auth.
    /// </summary>
    [HttpPost("csr/renew")]
    [Authorize(Policy = FederationAuthExtensions.PolicyName,
               AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(IssueCertificateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IssueCertificateResponse> RenewCertificate(
        [FromBody] IssueCertificateRequest request)
    {
        if (!HubCaEnabled) return NotFound();
        if (request is null || string.IsNullOrWhiteSpace(request.CsrPem))
            return BadRequest("CsrPem is required.");

        string? authenticatedClusterId = User.Identity?.Name;
        if (string.IsNullOrEmpty(authenticatedClusterId))
            return Forbid();

        byte[] csrDer;
        string? csrCommonName;
        try
        {
            csrDer = LoadCsrDer(request.CsrPem);
            CertificateRequest parsed = CertificateRequest.LoadSigningRequest(
                csrDer, HashAlgorithmName.SHA256, CertificateRequestLoadOptions.Default);
            csrCommonName = GetCommonName(parsed.SubjectName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Renewal CSR failed to parse for {ClusterId}.", authenticatedClusterId);
            return BadRequest($"Invalid CSR: {ex.Message}");
        }

        if (string.IsNullOrEmpty(csrCommonName))
            return BadRequest("CSR subject must include a Common Name (CN=clusterId).");

        if (!string.Equals(csrCommonName, authenticatedClusterId, StringComparison.OrdinalIgnoreCase))
        {
            // A spoke with a valid KIBALE-UGANDA cert can renew only KIBALE-UGANDA.
            _logger.LogWarning(
                "Renewal rejected: authenticated cluster '{Authenticated}' does not match CSR CN '{Requested}'.",
                authenticatedClusterId, csrCommonName);
            return Forbid();
        }

        X509Certificate2 leaf;
        try
        {
            leaf = _ca!.IssueCertificate(csrDer, TimeSpan.FromDays(_options.IssuedCertValidityDays));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub-CA failed to renew cert for {ClusterId}.", authenticatedClusterId);
            return BadRequest($"Cert issuance failed: {ex.Message}");
        }

        var response = new IssueCertificateResponse(
            CertPem: ExportCertPem(leaf),
            CaCertPem: ExportCertPem(_ca!.RootCertificate));
        leaf.Dispose();

        _logger.LogInformation(
            "Hub-CA renewed cert for cluster {ClusterId} (mTLS-authenticated).",
            authenticatedClusterId);

        return Ok(response);
    }

    /// <summary>
    /// Revoke a federation cert by thumbprint. Idempotent — re-revoking the
    /// same thumbprint preserves the original record. The local revocation
    /// cache is refreshed immediately so subsequent inbound requests see the
    /// new revocation without waiting for the next periodic refresh.
    /// </summary>
    [HttpPost("admin/revoke")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeCertificate(
        [FromBody] RevokeCertificateRequest request,
        [FromServices] IRevocationCache revocationCache,
        CancellationToken cancellationToken)
    {
        if (!HubCaEnabled) return NotFound();
        if (request is null || string.IsNullOrWhiteSpace(request.Thumbprint))
            return BadRequest("Thumbprint is required.");

        // Normalize for the format check: SHA-1 = 40 hex chars, SHA-256 = 64.
        string normalized = request.Thumbprint
            .Replace(" ", string.Empty)
            .Replace(":", string.Empty)
            .ToUpperInvariant();
        if (!IsHex(normalized) || (normalized.Length != 40 && normalized.Length != 64))
        {
            return BadRequest("Thumbprint must be 40 or 64 hex characters (SHA-1 or SHA-256).");
        }

        string adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name
            ?? "unknown";

        IRevocationRegistryGrain registry =
            _grainFactory.GetGrain<IRevocationRegistryGrain>(IRevocationRegistryGrain.GlobalKey);
        await registry.RevokeAsync(normalized, request.ClusterId ?? string.Empty, request.Reason ?? string.Empty, adminUserId);

        // Local cache refresh — make hub-local revocations effectively-instant
        // rather than waiting for the periodic refresh.
        try
        {
            await revocationCache.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Revocation succeeded but local cache refresh failed; periodic refresh will pick it up.");
        }

        _logger.LogInformation(
            "Cert revoked: thumbprint={Thumbprint} cluster={ClusterId} by={Admin} reason={Reason}",
            normalized, request.ClusterId, adminUserId, request.Reason);

        return Ok();
    }

    private static bool IsHex(string s)
    {
        foreach (char c in s)
        {
            if (!Uri.IsHexDigit(c)) return false;
        }
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateToken()
    {
        // 32 bytes of randomness → 43 base64url chars. Ample for a one-time
        // secret. Using base64url so it fits in HTTP headers without escaping.
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string? ExtractBearerToken(IEnumerable<string?> authHeaders)
    {
        foreach (string? header in authHeaders)
        {
            if (string.IsNullOrEmpty(header)) continue;
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return header["Bearer ".Length..].Trim();
            }
        }
        return null;
    }

    private static byte[] LoadCsrDer(string csrPem)
    {
        // Strip the PEM armor and decode base64. PemEncoding is the documented
        // primitive for this on .NET 9+.
        ReadOnlySpan<char> span = csrPem.AsSpan();
        PemFields fields = PemEncoding.Find(span);
        ReadOnlySpan<char> base64 = span[fields.Base64Data];
        return Convert.FromBase64String(base64.ToString());
    }

    private static string? GetCommonName(X500DistinguishedName subject)
    {
        // Subject.Name format: "CN=KIBALE-UGANDA, OU=..., ...". Regex is
        // adequate for the simple subjects we generate; for more elaborate
        // names use X500DistinguishedName.EnumerateRelativeDistinguishedNames()
        // in .NET 9+.
        Match match = Regex.Match(subject.Name, @"CN=(?<cn>[^,]+)");
        return match.Success ? match.Groups["cn"].Value.Trim() : null;
    }

    private static string ExportCertPem(X509Certificate2 cert)
    {
        byte[] der = cert.Export(X509ContentType.Cert);
        return PemEncoding.WriteString("CERTIFICATE", der);
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record IssueProvisioningTokenRequest(string ClusterId, int? ValidityHours);
public sealed record IssueProvisioningTokenResponse(string Token, DateTime ExpiresUtc);

public sealed record IssueCertificateRequest(string CsrPem);
public sealed record IssueCertificateResponse(string CertPem, string CaCertPem);

public sealed record RevokeCertificateRequest(string Thumbprint, string? ClusterId, string? Reason);
