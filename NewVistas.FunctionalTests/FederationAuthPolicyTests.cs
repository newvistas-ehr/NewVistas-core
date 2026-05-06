// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using NewVistas.WebServer.Controllers;
using NewVistas.WebServer.Infrastructure.Federation;
using Orleans.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Behavioural tests for the federation inbound auth pieces:
///   - <see cref="FederationPeerHandler"/> in both allow-all and enforce modes
///   - <see cref="FederationController.Inbound"/> cluster-id match check
///
/// These are unit tests in functional-tests' clothing — they live here only
/// because <c>NewVistas.WebServer</c> isn't referenced from
/// <c>NewVistas.UnitTests</c>. No real TLS handshake is involved; the cert
/// validation pipeline is well-covered by Microsoft's own tests for the
/// Certificate auth package.
/// </summary>
[TestFixture]
public class FederationAuthPolicyTests
{
    private static FederationPeerHandler NewHandler(IRevocationCache? cache = null) =>
        new(cache ?? new NoOpRevocationCache(), NullLogger<FederationPeerHandler>.Instance);

    private static AuthorizationHandlerContext MakeContext(
        FederationPeerRequirement requirement,
        ClaimsPrincipal? user = null)
    {
        user ??= new ClaimsPrincipal(new ClaimsIdentity());  // unauthenticated
        return new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
    }

    private static ClaimsPrincipal AuthenticatedAs(string clusterId, string? thumbprint = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, clusterId) };
        if (!string.IsNullOrEmpty(thumbprint))
        {
            claims.Add(new Claim(FederationAuthExtensions.ThumbprintClaimType, thumbprint));
        }
        var identity = new ClaimsIdentity(claims, authenticationType: "Certificate");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Test double: cache that reports a fixed set of thumbprints as revoked.</summary>
    private sealed class StubRevocationCache : IRevocationCache
    {
        private readonly HashSet<string> _revoked;
        public StubRevocationCache(params string[] revokedThumbprints) =>
            _revoked = new HashSet<string>(revokedThumbprints, StringComparer.OrdinalIgnoreCase);

        public bool IsRevoked(string thumbprint) => _revoked.Contains(thumbprint);
        public Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
    }

    // ── Policy: allow-all mode ───────────────────────────────────────────────

    [Test]
    public async Task Policy_AllowAll_SucceedsWithUnauthenticatedCaller()
    {
        var requirement = new FederationPeerRequirement(allowAll: true);
        AuthorizationHandlerContext ctx = MakeContext(requirement);

        await NewHandler().HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.True);
    }

    [Test]
    public async Task Policy_AllowAll_SucceedsWithAuthenticatedCaller()
    {
        var requirement = new FederationPeerRequirement(allowAll: true);
        AuthorizationHandlerContext ctx = MakeContext(requirement, AuthenticatedAs("ANY-CLUSTER"));

        await NewHandler().HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.True);
    }

    // ── Policy: enforce mode ─────────────────────────────────────────────────

    [Test]
    public async Task Policy_Enforced_FailsWhenUnauthenticated()
    {
        var requirement = new FederationPeerRequirement(new[] { "PEER-A", "PEER-B" });
        AuthorizationHandlerContext ctx = MakeContext(requirement);

        await NewHandler().HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.False);
    }

    [Test]
    public async Task Policy_Enforced_FailsWhenClusterIdNotInAllowList()
    {
        var requirement = new FederationPeerRequirement(new[] { "PEER-A", "PEER-B" });
        AuthorizationHandlerContext ctx = MakeContext(requirement, AuthenticatedAs("UNKNOWN-PEER"));

        await NewHandler().HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.False);
    }

    [Test]
    public async Task Policy_Enforced_SucceedsWhenClusterIdInAllowList()
    {
        var requirement = new FederationPeerRequirement(new[] { "PEER-A", "PEER-B" });
        AuthorizationHandlerContext ctx = MakeContext(requirement, AuthenticatedAs("PEER-A"));

        await NewHandler().HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.True);
    }

    [Test]
    public async Task Policy_Enforced_AllowListIsCaseInsensitive()
    {
        var requirement = new FederationPeerRequirement(new[] { "Peer-A" });
        AuthorizationHandlerContext ctx = MakeContext(requirement, AuthenticatedAs("PEER-A"));

        await NewHandler().HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.True);
    }

    // ── Policy: revocation ───────────────────────────────────────────────────

    [Test]
    public async Task Policy_Enforced_FailsWhenCertIsRevoked()
    {
        const string revokedThumbprint = "ABCDEF1234567890ABCDEF1234567890ABCDEF12";
        var requirement = new FederationPeerRequirement(new[] { "PEER-A" });
        AuthorizationHandlerContext ctx = MakeContext(
            requirement, AuthenticatedAs("PEER-A", thumbprint: revokedThumbprint));

        await NewHandler(new StubRevocationCache(revokedThumbprint)).HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.False,
            "Revoked cert should fail auth even when the cluster id is in the allow-list.");
    }

    [Test]
    public async Task Policy_Enforced_SucceedsWhenCertNotRevoked()
    {
        const string unrelatedRevokedThumbprint = "0000000000000000000000000000000000000000";
        const string okThumbprint = "ABCDEF1234567890ABCDEF1234567890ABCDEF12";
        var requirement = new FederationPeerRequirement(new[] { "PEER-A" });
        AuthorizationHandlerContext ctx = MakeContext(
            requirement, AuthenticatedAs("PEER-A", thumbprint: okThumbprint));

        await NewHandler(new StubRevocationCache(unrelatedRevokedThumbprint)).HandleAsync(ctx);

        Assert.That(ctx.HasSucceeded, Is.True);
    }

    // ── Controller: cluster-id match check ───────────────────────────────────

    private static FederationController BuildController(
        ClaimsPrincipal user,
        IFederationInboundApplier? applier = null,
        InboundAuthOptions? authOptions = null)
    {
        var services = new ServiceCollection();
        services.AddSerializer();
        ServiceProvider sp = services.BuildServiceProvider();
        Serializer<EventEnvelope> serializer = sp.GetRequiredService<Serializer<EventEnvelope>>();

        var controller = new FederationController(
            applier ?? new StubApplier(),
            serializer,
            Options.Create(authOptions ?? new InboundAuthOptions { RequireMatchingClusterId = true }),
            NullLogger<FederationController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    [Test]
    public async Task Controller_AuthenticatedClusterMismatch_Returns403()
    {
        FederationController controller = BuildController(AuthenticatedAs("KIBALE-UGANDA"));

        ActionResult<InboundApplyResult> response = await controller.Inbound(
            new InboundFederationBatch
            {
                FromClusterId = "VAMC-BOSTON",  // lying about origin
                EnvelopeBlobs = new List<byte[]>()
            },
            CancellationToken.None);

        Assert.That(response.Result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task Controller_AuthenticatedClusterMatches_ReachesApplier()
    {
        var applier = new StubApplier();
        FederationController controller = BuildController(AuthenticatedAs("KIBALE-UGANDA"), applier);

        ActionResult<InboundApplyResult> response = await controller.Inbound(
            new InboundFederationBatch
            {
                FromClusterId = "KIBALE-UGANDA",  // matches authenticated identity
                EnvelopeBlobs = new List<byte[]>()
            },
            CancellationToken.None);

        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That(applier.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Controller_AllowAllMode_NoIdentity_SkipsClusterCheck()
    {
        // Allow-all mode at the policy layer means the action sees no
        // authenticated principal. The cluster-id check is skipped (no
        // identity to compare against) and the request proceeds.
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
        var applier = new StubApplier();
        FederationController controller = BuildController(unauthenticated, applier);

        ActionResult<InboundApplyResult> response = await controller.Inbound(
            new InboundFederationBatch
            {
                FromClusterId = "ANY-PEER",
                EnvelopeBlobs = new List<byte[]>()
            },
            CancellationToken.None);

        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That(applier.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Controller_RequireMatchingClusterIdFalse_SkipsClusterCheck()
    {
        var applier = new StubApplier();
        FederationController controller = BuildController(
            AuthenticatedAs("KIBALE-UGANDA"),
            applier,
            new InboundAuthOptions { RequireMatchingClusterId = false });

        // Mismatch — but the option is off, so the controller proceeds.
        ActionResult<InboundApplyResult> response = await controller.Inbound(
            new InboundFederationBatch
            {
                FromClusterId = "VAMC-BOSTON",
                EnvelopeBlobs = new List<byte[]>()
            },
            CancellationToken.None);

        Assert.That(response.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That(applier.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Controller_EmptyFromClusterId_Returns400()
    {
        FederationController controller = BuildController(AuthenticatedAs("KIBALE-UGANDA"));

        ActionResult<InboundApplyResult> response = await controller.Inbound(
            new InboundFederationBatch
            {
                FromClusterId = "",
                EnvelopeBlobs = new List<byte[]>()
            },
            CancellationToken.None);

        Assert.That(response.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    private sealed class StubApplier : IFederationInboundApplier
    {
        public List<(IReadOnlyList<EventEnvelope> envelopes, string fromClusterId)> Calls { get; } = new();

        public Task<InboundApplyResult> ApplyBatchAsync(
            IReadOnlyList<EventEnvelope> envelopes, string fromClusterId, CancellationToken cancellationToken)
        {
            Calls.Add((envelopes, fromClusterId));
            return Task.FromResult(new InboundApplyResult(envelopes.Count, envelopes.Count, 0));
        }
    }
}
