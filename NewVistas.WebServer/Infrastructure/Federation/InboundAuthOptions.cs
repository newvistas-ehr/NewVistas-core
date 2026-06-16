// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Inbound-side mTLS configuration for the federation endpoint. Bound from
/// the <c>Federation:Inbound</c> configuration section.
///
/// When <see cref="TrustedCaPath"/> is unset (or empty), the
/// <c>FederationPeer</c> authorization policy operates in allow-all mode —
/// the controller behaves exactly as it did pre-auth. When set, the policy
/// enforces a valid client cert chained to that CA, with the cert's CN
/// listed in <see cref="AllowedClusterIds"/>.
/// </summary>
public sealed class InboundAuthOptions
{
    public const string SectionName = "Federation:Inbound";

    /// <summary>
    /// Path to the trust anchor (PEM) used for chain validation. When null
    /// or empty, mTLS is disabled and the controller is open. Set this on
    /// <c>RemoteOnline</c> deployments.
    /// </summary>
    public string? TrustedCaPath { get; set; }

    /// <summary>
    /// Cluster ids permitted to call the inbound endpoint. The
    /// authenticated principal's <c>Identity.Name</c> (set from the client
    /// cert's CN) must appear in this list.
    /// </summary>
    public List<string> AllowedClusterIds { get; set; } = new();

    /// <summary>
    /// When true (default), the controller additionally requires the
    /// authenticated cluster id to match <c>InboundFederationBatch.FromClusterId</c>.
    /// Defends against a peer with a valid cert posting events as another cluster.
    /// </summary>
    public bool RequireMatchingClusterId { get; set; } = true;
}
