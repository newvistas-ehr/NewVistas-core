// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure.Federation;
using Orleans;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Read-only admin endpoints powering the federation security dashboard.
/// All <see cref="HttpGetAttribute"/>; all gated by the existing
/// <c>Administrator</c> role. Mutating operations stay on
/// <see cref="HubCaController"/>.
///
/// <para>
/// Each endpoint is independently feature-gated. A spoke cluster (no
/// hub-CA) returns 404 for the hub-only panels (revocations, tokens) and
/// 200 for outbox stats; a deployment without an outbox returns 404 for
/// outbox stats. The dashboard page treats 404 as "panel not applicable
/// here" and renders a disabled state rather than an error.
/// </para>
/// </summary>
[ApiController]
[Route("api/federation/admin")]
[Produces("application/json")]
[Authorize(Roles = "Administrator")]
public sealed class FederationAdminController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly HubCaOptions _hubCaOptions;
    private readonly ILogger<FederationAdminController> _logger;

    public FederationAdminController(
        IGrainFactory grainFactory,
        IOptions<HubCaOptions> hubCaOptions,
        ILogger<FederationAdminController> logger)
    {
        _grainFactory = grainFactory;
        _hubCaOptions = hubCaOptions.Value;
        _logger = logger;
    }

    private bool HubCaEnabled => _hubCaOptions.Enabled;

    /// <summary>
    /// List every revoked cert with metadata. Hub-only.
    /// </summary>
    [HttpGet("revocations")]
    [ProducesResponseType(typeof(RevocationListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevocationListResponse>> GetRevocations()
    {
        if (!HubCaEnabled) return NotFound();

        IRevocationRegistryGrain grain =
            _grainFactory.GetGrain<IRevocationRegistryGrain>(IRevocationRegistryGrain.GlobalKey);
        IReadOnlyList<RevocationRecord> records = await grain.GetAllAsync();

        // Order newest first for dashboard display.
        var sorted = records.OrderByDescending(r => r.RevokedUtc).ToList();
        return Ok(new RevocationListResponse(sorted));
    }

    /// <summary>
    /// List every provisioning token issued. Hub-only. Token strings are
    /// truncated to a recognizable prefix; full tokens never leave the
    /// per-token grain.
    /// </summary>
    [HttpGet("provisioning-tokens")]
    [ProducesResponseType(typeof(ProvisioningTokenListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProvisioningTokenListResponse>> GetProvisioningTokens()
    {
        if (!HubCaEnabled) return NotFound();

        IProvisioningTokenIndexGrain grain =
            _grainFactory.GetGrain<IProvisioningTokenIndexGrain>(IProvisioningTokenIndexGrain.GlobalKey);
        IReadOnlyList<ProvisioningTokenSummary> records = await grain.GetAllAsync();

        DateTime now = DateTime.UtcNow;
        var view = records.Select(r => new ProvisioningTokenView(
            TokenPrefix: TruncateToken(r.Token),
            ClusterId: r.ClusterId,
            IssuedUtc: r.IssuedUtc,
            ExpiresUtc: r.ExpiresUtc,
            ConsumedUtc: r.ConsumedUtc,
            ConsumedByThumbprint: r.ConsumedByThumbprint,
            Status: ClassifyToken(r, now))).ToList();

        return Ok(new ProvisioningTokenListResponse(view));
    }

    /// <summary>
    /// Outbox health stats — pending count, oldest pending, last sent.
    /// Returns 404 when the deployment has no outbox.
    /// </summary>
    [HttpGet("outbox-stats")]
    [ProducesResponseType(typeof(OutboxStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OutboxStats>> GetOutboxStats()
    {
        IFederationStatsGrain grain =
            _grainFactory.GetGrain<IFederationStatsGrain>(IFederationStatsGrain.GlobalKey);
        OutboxStats stats = await grain.GetOutboxStatsAsync();

        if (!stats.Available) return NotFound();
        return Ok(stats);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string TruncateToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        const int prefixLen = 8;
        return token.Length <= prefixLen
            ? token
            : token[..prefixLen] + "…";  // ellipsis
    }

    private static string ClassifyToken(ProvisioningTokenSummary t, DateTime nowUtc)
    {
        if (t.ConsumedUtc is not null) return "consumed";
        if (nowUtc >= t.ExpiresUtc) return "expired";
        return "pending";
    }
}

// ── Response DTOs ────────────────────────────────────────────────────────────

public sealed record RevocationListResponse(IReadOnlyList<RevocationRecord> Records);

public sealed record ProvisioningTokenListResponse(IReadOnlyList<ProvisioningTokenView> Tokens);

public sealed record ProvisioningTokenView(
    string TokenPrefix,
    string ClusterId,
    DateTime IssuedUtc,
    DateTime ExpiresUtc,
    DateTime? ConsumedUtc,
    string? ConsumedByThumbprint,
    string Status);
