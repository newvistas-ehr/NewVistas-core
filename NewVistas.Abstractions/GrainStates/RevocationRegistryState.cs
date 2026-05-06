// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Per-revocation forensic record. Once added, it stays — operators can
/// see when a cert was revoked, by whom, and why, even after the underlying
/// cert has expired.
/// </summary>
[GenerateSerializer]
public sealed record RevocationRecord
{
    /// <summary>
    /// SHA-1 thumbprint of the revoked cert. Always uppercase hex with no
    /// separators (matches <c>X509Certificate2.Thumbprint</c>).
    /// </summary>
    [Id(0)] public string Thumbprint { get; init; } = string.Empty;

    /// <summary>Cluster id this cert was issued to. For audit display only.</summary>
    [Id(1)] public string ClusterId { get; init; } = string.Empty;

    /// <summary>Operator-supplied free text — why this cert was revoked.</summary>
    [Id(2)] public string Reason { get; init; } = string.Empty;

    /// <summary>UTC time of revocation.</summary>
    [Id(3)] public DateTime RevokedUtc { get; init; }

    /// <summary>User id of the admin who revoked. Captured from the auth context.</summary>
    [Id(4)] public string RevokedByUserId { get; init; } = string.Empty;
}

/// <summary>
/// State for the singleton <see cref="GrainInterfaces.IRevocationRegistryGrain"/>.
/// Holds every revocation in one list — append-only, indexed by thumbprint.
/// Volume is small (a federation of dozens of spokes generates a handful of
/// revocations per year), so no per-thumbprint grain fan-out.
/// </summary>
[GenerateSerializer]
public class RevocationRegistryState
{
    [Id(0)] public List<RevocationRecord> Records { get; set; } = new();
}
