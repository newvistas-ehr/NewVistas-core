// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain holding the index of every provisioning token ever
/// issued, so the security dashboard can list them. The per-token
/// <see cref="IProvisioningTokenGrain"/> remains the source of truth for
/// the bootstrap flow itself; this index is best-effort and exists for
/// enumeration.
///
/// One grain per cluster, keyed by <see cref="GlobalKey"/>.
/// </summary>
public interface IProvisioningTokenIndexGrain : IGrainWithStringKey
{
    public const string GlobalKey = "GLOBAL";

    /// <summary>
    /// Append a newly-issued token. Idempotent on the token string —
    /// re-adding the same token preserves the original entry.
    /// </summary>
    Task AddAsync(string token, string clusterId, DateTime issuedUtc, DateTime expiresUtc);

    /// <summary>
    /// Mark a token consumed in the index. Looks up by token string;
    /// no-op if the token isn't in the index (e.g., issued before the
    /// index was wired).
    /// </summary>
    Task MarkConsumedAsync(string token, DateTime consumedUtc, string consumedByThumbprint);

    /// <summary>
    /// All tokens in the index, ordered by <see cref="ProvisioningTokenSummary.IssuedUtc"/>
    /// descending (newest first).
    /// </summary>
    Task<IReadOnlyList<ProvisioningTokenSummary>> GetAllAsync();
}
