// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;

namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Wires up the <c>FederationPeer</c> authorization policy that the
/// inbound federation controller depends on.
///
/// <para>
/// Behaviour is configuration-driven:
/// <list type="bullet">
///   <item><description>
///     When <see cref="InboundAuthOptions.TrustedCaPath"/> is null/empty,
///     the policy is registered in <b>allow-all mode</b> — the controller
///     accepts requests from any caller (including unauthenticated). A
///     warning is logged at startup so misconfiguration is loud.
///   </description></item>
///   <item><description>
///     When set, mTLS is required: client certificates are validated
///     against the trust anchor, and the policy further requires the
///     cert's CN to be listed in <see cref="InboundAuthOptions.AllowedClusterIds"/>.
///   </description></item>
/// </list>
/// </para>
/// </summary>
public static class FederationAuthExtensions
{
    public const string PolicyName = "FederationPeer";

    /// <summary>Claim type used to surface the authenticated cluster id (cert CN).</summary>
    public const string ClusterIdClaimType = "FederationClusterId";

    /// <summary>Claim type used to surface the authenticated cert thumbprint (for revocation checks).</summary>
    public const string ThumbprintClaimType = "FederationCertThumbprint";

    public static IServiceCollection AddFederationInboundAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(InboundAuthOptions.SectionName)
            .Get<InboundAuthOptions>() ?? new InboundAuthOptions();

        services.Configure<InboundAuthOptions>(
            configuration.GetSection(InboundAuthOptions.SectionName));

        bool authEnabled = !string.IsNullOrWhiteSpace(options.TrustedCaPath);

        if (authEnabled)
        {
            X509Certificate2 trustAnchor = LoadTrustAnchor(options.TrustedCaPath!);

            services.AddAuthentication()
                .AddCertificate(CertificateAuthenticationDefaults.AuthenticationScheme, certOptions =>
                {
                    certOptions.AllowedCertificateTypes = CertificateTypes.All;
                    // Self-signed CA is the common deployment shape (private CA
                    // run by the federation hub). The chain validator below
                    // accepts only chains rooted at the configured trust anchor.
                    certOptions.RevocationMode = X509RevocationMode.NoCheck;
                    certOptions.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;
                    certOptions.CustomTrustStore = new X509Certificate2Collection(trustAnchor);

                    certOptions.Events = new CertificateAuthenticationEvents
                    {
                        OnCertificateValidated = ctx =>
                        {
                            // Cluster id == client cert CN.
                            string? cn = ctx.ClientCertificate.GetNameInfo(X509NameType.SimpleName, false);
                            if (string.IsNullOrEmpty(cn))
                            {
                                ctx.Fail("Client certificate has no CN.");
                                return Task.CompletedTask;
                            }

                            var claims = new[]
                            {
                                new Claim(ClaimTypes.Name, cn, ClaimValueTypes.String, ctx.Options.ClaimsIssuer),
                                new Claim(ClusterIdClaimType, cn, ClaimValueTypes.String, ctx.Options.ClaimsIssuer),
                                new Claim(ThumbprintClaimType, ctx.ClientCertificate.Thumbprint, ClaimValueTypes.String, ctx.Options.ClaimsIssuer),
                            };
                            ctx.Principal = new ClaimsPrincipal(
                                new ClaimsIdentity(claims, ctx.Scheme.Name));
                            ctx.Success();
                            return Task.CompletedTask;
                        },
                    };
                });
        }

        services.AddAuthorization(authz =>
        {
            authz.AddPolicy(PolicyName, policy =>
            {
                if (authEnabled)
                {
                    policy.AuthenticationSchemes.Add(CertificateAuthenticationDefaults.AuthenticationScheme);
                    policy.Requirements.Add(new FederationPeerRequirement(options.AllowedClusterIds));
                }
                else
                {
                    // Allow-all: a single requirement that always succeeds.
                    policy.Requirements.Add(new FederationPeerRequirement(allowAll: true));
                }
            });
        });

        services.AddSingleton<IAuthorizationHandler, FederationPeerHandler>();

        return services;
    }

    private static X509Certificate2 LoadTrustAnchor(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Federation trust anchor file not found at '{path}'. " +
                "Set Federation:Inbound:TrustedCaPath to a valid PEM file or unset it to disable mTLS.",
                path);
        }
        // X509CertificateLoader.LoadCertificateFromFile handles both PEM and DER on .NET 9+.
        return X509CertificateLoader.LoadCertificateFromFile(path);
    }
}

/// <summary>
/// Requirement attached to the <c>FederationPeer</c> policy. In allow-all
/// mode the <see cref="FederationPeerHandler"/> always succeeds; otherwise
/// it checks that the authenticated principal's name is in the configured
/// allow-list.
/// </summary>
public sealed class FederationPeerRequirement : IAuthorizationRequirement
{
    public FederationPeerRequirement(bool allowAll)
    {
        AllowAll = allowAll;
        AllowedClusterIds = Array.Empty<string>();
    }

    public FederationPeerRequirement(IReadOnlyList<string> allowedClusterIds)
    {
        AllowAll = false;
        AllowedClusterIds = allowedClusterIds;
    }

    public bool AllowAll { get; }
    public IReadOnlyList<string> AllowedClusterIds { get; }
}

public sealed class FederationPeerHandler : AuthorizationHandler<FederationPeerRequirement>
{
    private readonly IRevocationCache _revocationCache;
    private readonly ILogger<FederationPeerHandler> _logger;

    public FederationPeerHandler(
        IRevocationCache revocationCache,
        ILogger<FederationPeerHandler> logger)
    {
        _revocationCache = revocationCache;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FederationPeerRequirement requirement)
    {
        if (requirement.AllowAll)
        {
            // Open mode — controller is unauthenticated. Startup logged this once.
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Federation inbound rejected: no authenticated principal.");
            return Task.CompletedTask;
        }

        string? clusterId = context.User.Identity.Name;
        if (string.IsNullOrEmpty(clusterId))
        {
            _logger.LogWarning("Federation inbound rejected: principal has no name claim.");
            return Task.CompletedTask;
        }

        // Revocation check before the allow-list check: a revoked cert
        // shouldn't even reach the allow-list comparison. Non-hub deployments
        // get a NoOpRevocationCache that always returns false here.
        string? thumbprint = context.User.FindFirst(FederationAuthExtensions.ThumbprintClaimType)?.Value;
        if (!string.IsNullOrEmpty(thumbprint) && _revocationCache.IsRevoked(thumbprint))
        {
            _logger.LogWarning(
                "Federation inbound rejected: cert {Thumbprint} for cluster '{ClusterId}' is revoked.",
                thumbprint, clusterId);
            return Task.CompletedTask;
        }

        if (!requirement.AllowedClusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Federation inbound rejected: cluster '{ClusterId}' is not in the allow-list.",
                clusterId);
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
