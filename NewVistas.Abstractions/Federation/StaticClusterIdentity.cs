// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// <see cref="IClusterIdentity"/> backed by constants supplied at construction.
/// Each site profile registers one of these with the cluster's configured
/// identity (or a profile-specific fallback for smoke tests).
/// </summary>
public sealed class StaticClusterIdentity : IClusterIdentity
{
    private static readonly Regex IcnPrefixPattern = new("^[0-9]{3}$", RegexOptions.Compiled);

    public StaticClusterIdentity(string localClusterId, string icnPrefix)
    {
        if (string.IsNullOrWhiteSpace(localClusterId))
            throw new ArgumentException("Cluster identity cannot be empty.", nameof(localClusterId));
        if (string.IsNullOrWhiteSpace(icnPrefix))
            throw new ArgumentException("ICN prefix cannot be empty.", nameof(icnPrefix));
        if (!IcnPrefixPattern.IsMatch(icnPrefix))
            throw new ArgumentException(
                $"ICN prefix must be exactly 3 numeric digits (got '{icnPrefix}').",
                nameof(icnPrefix));

        LocalClusterId = localClusterId;
        IcnPrefix = icnPrefix;
    }

    public string LocalClusterId { get; }

    public string IcnPrefix { get; }
}
