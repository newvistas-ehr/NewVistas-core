// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Configuration for <see cref="HttpFederationTransport"/>. Bound from the
/// <c>Federation:Http</c> configuration section.
/// </summary>
public class HttpFederationTransportOptions
{
    public const string SectionName = "Federation:Http";

    /// <summary>
    /// Full URL of the upstream cluster's inbound endpoint, e.g.
    /// <c>https://hub.va.gov/api/federation/inbound</c>. When null/empty, the
    /// profile falls back to <c>LoggingFederationTransport</c>.
    /// </summary>
    public string? InboundUrl { get; set; }

    /// <summary>Per-request timeout in seconds. Default: 60.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Path to a client certificate (PFX) used to authenticate this cluster
    /// to the upstream. When null/empty, requests are sent un-authenticated
    /// (suitable for same-machine smoke tests and the pre-mTLS rollout
    /// window). Production <c>RemoteOnline</c> deployments must set this.
    /// </summary>
    public string? ClientCertPath { get; set; }

    /// <summary>Password for the PFX file at <see cref="ClientCertPath"/>, if any.</summary>
    public string? ClientCertPassword { get; set; }
}
