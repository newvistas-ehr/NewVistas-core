// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Hub-CA configuration. Bound from <c>Federation:HubCa</c>. When
/// <see cref="Enabled"/> is false (the default), the hub-CA service is not
/// registered and the <c>HubCaController</c> endpoints are unreachable.
/// </summary>
public sealed class HubCaOptions
{
    public const string SectionName = "Federation:HubCa";

    /// <summary>
    /// Master switch. When true, the hub-CA service is registered and the
    /// CSR + admin endpoints accept requests. When false, the controller
    /// returns 404 (its endpoints are not registered with the routing table).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Path to the hub-CA root certificate (PEM).</summary>
    public string? RootCertPath { get; set; }

    /// <summary>Path to the hub-CA private key (PEM, PKCS#8 or PKCS#1).</summary>
    public string? RootKeyPath { get; set; }

    /// <summary>Optional password protecting the private key file.</summary>
    public string? RootKeyPassword { get; set; }

    /// <summary>Validity of certs issued to spokes. Default 365 days.</summary>
    public int IssuedCertValidityDays { get; set; } = 365;

    /// <summary>Default expiry window of newly-issued provisioning tokens. Default 24 hours.</summary>
    public int ProvisioningTokenValidityHours { get; set; } = 24;
}
