// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Spool-directory layout for sneakernet federation. Bound from
/// <c>Federation:FileBundle</c>. A clinic configured for offline operation
/// uses these paths to drop outbound bundles (one batch per file) and to
/// pick up inbound bundles delivered by USB / satellite uplink / similar.
/// </summary>
public sealed class FileBundleOptions
{
    public const string SectionName = "Federation:FileBundle";

    /// <summary>Where the drainer writes ready-to-ship bundles. Created if missing.</summary>
    public string? OutboundDirectory { get; set; }

    /// <summary>Where the inbound service watches for arrivals. Created if missing.</summary>
    public string? InboundDirectory { get; set; }

    /// <summary>Where the inbound service moves successfully-applied bundles. Created if missing.</summary>
    public string? ProcessedDirectory { get; set; }

    /// <summary>How often the inbound service scans for new bundles. Default: 60 seconds.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;
}
