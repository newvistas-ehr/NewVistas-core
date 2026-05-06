// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// One bootstrap-token grain per token. Grain key = the token string itself,
/// so a CSR endpoint that's handed a token can resolve the grain directly
/// without a lookup.
///
/// Lifecycle:
///   1. Hub admin calls <see cref="IssueAsync"/> via the admin endpoint.
///   2. Spoke posts a CSR with the token; CSR endpoint calls
///      <see cref="ConsumeAsync"/> to validate + mark used.
///   3. Token state remains for forensic queries (when, who, what cert).
/// </summary>
public interface IProvisioningTokenGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the token state. Idempotent — re-issuing the same grain key
    /// (which would only happen if an admin reused a token string by accident)
    /// throws.
    /// </summary>
    Task IssueAsync(string clusterId, DateTime expiresUtc);

    /// <summary>
    /// Validate + consume the token. Throws <see cref="InvalidOperationException"/>
    /// when the token isn't issued, has expired, was already consumed, or the
    /// supplied <paramref name="requestedClusterId"/> doesn't match the issued
    /// cluster id. On success, records the cert thumbprint.
    /// </summary>
    Task ConsumeAsync(string requestedClusterId, string issuedCertThumbprint);

    /// <summary>Read-only state accessor for diagnostics/admin views.</summary>
    Task<ProvisioningTokenState> GetStateAsync();
}
