// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Issues fresh Integration Control Numbers (ICNs) for patients registered at
/// the local cluster. Singleton — addressed by the constant key
/// <c>"ICN-ISSUER"</c>. ICN format is
/// <c>{3-digit cluster prefix}{7-digit local sequence}V{6-digit checksum}</c>;
/// the prefix is sourced from <see cref="Federation.IClusterIdentity.IcnPrefix"/>
/// at issuance time. Sequence is monotonic and persisted before the issued
/// ICN is returned to the caller, so a silo crash cannot cause a duplicate.
///
/// See <see href="../Docs/Architect-decisions/ADR-001-Patient-Identity-Strategy.md">ADR-001</see>
/// for the format rationale and the cluster-prefix allocation policy.
/// </summary>
public interface IIcnIssuerGrain : IGrainWithStringKey
{
    /// <summary>
    /// Issue the next ICN for this cluster. Persists the advanced sequence
    /// before returning, so the same ICN is never issued twice even on
    /// process restart.
    /// </summary>
    Task<string> IssueNextAsync();

    /// <summary>
    /// Diagnostic: the next sequence number that will be used. Does not
    /// advance the counter.
    /// </summary>
    Task<long> PeekNextSequenceAsync();
}
