// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Spoke-side abstraction over the hub's <c>POST /api/federation/csr/renew</c>
/// endpoint. The HTTP implementation reuses the same named <c>HttpClient</c>
/// as the outbound federation transport, so it inherits the mTLS handler
/// (authenticating with the spoke's current cert).
/// </summary>
public interface ICertificateAuthorityClient
{
    /// <summary>
    /// POST a CSR for renewal; returns the newly-issued cert (PEM) and the
    /// hub-CA's own root cert (PEM) for trust-anchor confirmation.
    /// Throws on transport / 4xx / 5xx errors.
    /// </summary>
    Task<RenewalResponse> RenewAsync(string csrPem, CancellationToken cancellationToken);
}

/// <summary>Response shape — mirrors the controller's <c>IssueCertificateResponse</c>.</summary>
public sealed record RenewalResponse(string CertPem, string CaCertPem);
