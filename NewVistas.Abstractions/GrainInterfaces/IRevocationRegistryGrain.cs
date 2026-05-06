// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain holding the federation cert revocation list. One grain
/// per cluster, keyed by the constant <see cref="GlobalKey"/>. Append-only —
/// a future un-revoke operation would be a separate plan.
///
/// Hub deployments register a periodic refresh service that pulls the list
/// into <c>IRevocationCache</c> on the WebServer; the inbound auth handler
/// consults the cache.
/// </summary>
public interface IRevocationRegistryGrain : IGrainWithStringKey
{
    /// <summary>The single grain key callers should use.</summary>
    public const string GlobalKey = "GLOBAL";

    /// <summary>
    /// Append a revocation. Idempotent on <paramref name="thumbprint"/> —
    /// re-revoking the same thumbprint is a no-op (the original record's
    /// timestamp + reason are preserved).
    /// </summary>
    Task RevokeAsync(string thumbprint, string clusterId, string reason, string revokedByUserId);

    /// <summary>True when <paramref name="thumbprint"/> appears in the list.</summary>
    Task<bool> IsRevokedAsync(string thumbprint);

    /// <summary>Snapshot of every revocation, ordered by revocation time.</summary>
    Task<IReadOnlyList<RevocationRecord>> GetAllAsync();
}
