// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Cryptography.X509Certificates;

namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Signs spoke-cluster certs against the hub's long-lived root cert. One
/// instance per WebServer process; loads root material once at startup.
///
/// Stateless w.r.t. caller — no state about who's been issued what; the
/// provisioning-token grains hold that bookkeeping.
/// </summary>
public interface IHubCertificateAuthority
{
    /// <summary>
    /// Signs a CSR submitted by a spoke. The CSR's CN must already match the
    /// authorized cluster id (the controller validates that before calling
    /// this method).
    /// </summary>
    /// <returns>The signed leaf cert.</returns>
    X509Certificate2 IssueCertificate(byte[] csrDer, TimeSpan validity);

    /// <summary>The hub-CA root certificate, exposed so the controller can return it alongside leaf certs.</summary>
    X509Certificate2 RootCertificate { get; }
}
