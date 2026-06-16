// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Identifies the local cluster — the site/hospital this silo represents in a
/// federation. Stamped onto fresh clinical event envelopes at append time so
/// replicated chains carry source attribution end-to-end.
///
/// One implementation registered as a singleton per site profile. Profiles
/// typically supply a constant value via <see cref="StaticClusterIdentity"/>,
/// reading the configured value (or a profile-specific fallback) at startup.
/// </summary>
public interface IClusterIdentity
{
    /// <summary>
    /// The identifier this cluster reports as the source of any clinical event
    /// it writes. Examples: <c>"VAMC-BOSTON"</c>, <c>"KIBALE-UGANDA"</c>,
    /// <c>"DEV-LOCAL"</c>. Stable for the lifetime of the silo.
    /// </summary>
    string LocalClusterId { get; }

    /// <summary>
    /// The 3-digit numeric prefix for ICNs issued by this cluster. Combined with
    /// a 7-digit local sequence and a 6-digit checksum, this forms a 17-character
    /// ICN in the same shape as VA AITC-issued ICNs (e.g., <c>"5180003421V045712"</c>).
    /// Allocated globally per
    /// <see href="../Docs/Architect-decisions/ClusterPrefixAllocations.md">ClusterPrefixAllocations.md</see>;
    /// never reused even after a cluster is decommissioned.
    /// </summary>
    string IcnPrefix { get; }
}
