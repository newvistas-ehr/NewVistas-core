// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a federation provisioning token. The token is the grain key;
/// this state carries the cluster id it's bound to, expiry, and consumed-or-not.
/// One token, one cluster, one cert issuance.
/// </summary>
[GenerateSerializer]
public class ProvisioningTokenState
{
    /// <summary>Cluster id this token authorizes a cert for. Set at issuance, never changed.</summary>
    [Id(0)] public string ClusterId { get; set; } = string.Empty;

    /// <summary>UTC time the token was issued.</summary>
    [Id(1)] public DateTime IssuedUtc { get; set; }

    /// <summary>UTC time after which the token is no longer valid.</summary>
    [Id(2)] public DateTime ExpiresUtc { get; set; }

    /// <summary>UTC time the token was consumed. Null if still pending.</summary>
    [Id(3)] public DateTime? ConsumedUtc { get; set; }

    /// <summary>Thumbprint of the cert issued when the token was consumed. Forensic record.</summary>
    [Id(4)] public string? ConsumedByThumbprint { get; set; }

    /// <summary>True if this token has been issued (state is initialized).</summary>
    [Id(5)] public bool IsIssued { get; set; }
}
