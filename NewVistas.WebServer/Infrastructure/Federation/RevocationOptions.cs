// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Tunables for the revocation cache + refresh service. Bound from
/// <c>Federation:Revocation</c>. Active only when hub-CA is enabled
/// (revocation runs alongside the rest of the hub bookkeeping).
/// </summary>
public sealed class RevocationOptions
{
    public const string SectionName = "Federation:Revocation";

    /// <summary>How often the in-memory cache pulls a fresh snapshot from the registry grain. Default: 5 minutes.</summary>
    public int RefreshIntervalMinutes { get; set; } = 5;
}
