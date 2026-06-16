// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Fast, in-memory check for "is this cert thumbprint revoked?". Consulted
/// by <c>FederationPeerHandler</c> on every inbound auth check, so reads
/// must be lock-free.
///
/// On hub deployments the cache is refreshed from the registry grain by
/// <see cref="RevocationRefreshService"/>. On non-hub deployments a no-op
/// implementation is registered (always returns false), which keeps the
/// auth handler simple — it always has the dependency, never branches on
/// null.
/// </summary>
public interface IRevocationCache
{
    /// <summary>Lock-free check. Thumbprint should be uppercase hex (matches <c>X509Certificate2.Thumbprint</c>).</summary>
    bool IsRevoked(string thumbprint);

    /// <summary>Pulls a fresh snapshot from the source of truth. Called by the refresh service.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}
