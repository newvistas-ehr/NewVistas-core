// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Spoke-side cert auto-renewal config. Bound from <c>Federation:Renewal</c>.
/// When <see cref="Enabled"/> is false (the default), the renewal service
/// is not registered.
///
/// The renewal service watches the cert at
/// <c>Federation:Http:ClientCertPath</c>; when its <c>NotAfter</c> is within
/// <see cref="RenewBeforeExpiryDays"/>, it generates a CSR, posts it to
/// <see cref="Url"/> using the current cert as mTLS auth, receives a fresh
/// cert, and atomically swaps the file.
/// </summary>
public sealed class RenewalOptions
{
    public const string SectionName = "Federation:Renewal";

    /// <summary>Enable the auto-renewal hosted service. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Hub URL accepting renewal CSRs (e.g. <c>https://hub.example.com/api/federation/csr/renew</c>).</summary>
    public string? Url { get; set; }

    /// <summary>How often to check the current cert's expiry. Default: 6 hours.</summary>
    public int CheckIntervalHours { get; set; } = 6;

    /// <summary>Renew when the current cert's NotAfter is within this many days. Default: 30.</summary>
    public int RenewBeforeExpiryDays { get; set; } = 30;
}
