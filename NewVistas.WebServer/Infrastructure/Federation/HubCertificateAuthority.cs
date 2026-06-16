// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// File-backed implementation of <see cref="IHubCertificateAuthority"/>.
/// Loads the root cert + key once at construction and reuses them for every
/// signing operation.
///
/// Key material is held in memory for the WebServer's lifetime — the
/// process is the security boundary. Future plans can introduce an
/// <c>IHubCaKeyProvider</c> abstraction that fetches keys from Azure Key
/// Vault or an HSM; the rest of this class is unaffected.
/// </summary>
public sealed class HubCertificateAuthority : IHubCertificateAuthority, IDisposable
{
    private const int MinRsaKeySizeBits = 2048;
    private const int MinEcdsaKeySizeBits = 256;

    private readonly X509Certificate2 _rootWithPrivateKey;

    public HubCertificateAuthority(HubCaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootCertPath))
            throw new InvalidOperationException("Federation:HubCa:RootCertPath is required when HubCa is enabled.");
        if (string.IsNullOrWhiteSpace(options.RootKeyPath))
            throw new InvalidOperationException("Federation:HubCa:RootKeyPath is required when HubCa is enabled.");

        _rootWithPrivateKey = LoadRootCertWithKey(options.RootCertPath, options.RootKeyPath, options.RootKeyPassword);

        if (!_rootWithPrivateKey.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "Hub-CA root cert has no private key after loading. Check RootKeyPath and that the key matches the cert.");
        }
    }

    /// <summary>
    /// Constructs a CA from an in-memory cert that already carries its
    /// private key. Tests use this to skip the file I/O; production goes
    /// through the options-based constructor.
    /// </summary>
    public HubCertificateAuthority(X509Certificate2 rootWithPrivateKey)
    {
        ArgumentNullException.ThrowIfNull(rootWithPrivateKey);
        if (!rootWithPrivateKey.HasPrivateKey)
            throw new ArgumentException("Hub-CA root cert must include the private key.", nameof(rootWithPrivateKey));
        _rootWithPrivateKey = rootWithPrivateKey;
    }

    /// <summary>The root cert without the private key (safe to publish to spokes).</summary>
    public X509Certificate2 RootCertificate =>
        X509CertificateLoader.LoadCertificate(_rootWithPrivateKey.Export(X509ContentType.Cert));

    public X509Certificate2 IssueCertificate(byte[] csrDer, TimeSpan validity)
    {
        ArgumentNullException.ThrowIfNull(csrDer);

        // CertificateRequest.LoadSigningRequest verifies the CSR's self-signature
        // as part of loading; a tampered CSR throws here. The signature-padding
        // parameter is needed for the subsequent .Create(issuer, ...) call to
        // know how to sign the new cert with the issuer's RSA key.
        CertificateRequest request = CertificateRequest.LoadSigningRequest(
            csrDer,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.Default,
            signerSignaturePadding: RSASignaturePadding.Pkcs1);

        ValidatePublicKeyStrength(request);

        // Add extensions appropriate for a federation client cert. We don't
        // copy extensions from the CSR (operators can't request arbitrary
        // capabilities by stuffing them in the CSR).
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        // ClientAuthentication EKU — leaf is for outbound federation TLS.
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") },
            critical: true));

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5); // small clock-skew allowance
        DateTimeOffset notAfter = DateTimeOffset.UtcNow.Add(validity);

        // 64-bit random serial — enough to avoid collisions for a single hub.
        byte[] serial = new byte[8];
        RandomNumberGenerator.Fill(serial);

        return request.Create(_rootWithPrivateKey, notBefore, notAfter, serial);
    }

    public void Dispose() => _rootWithPrivateKey.Dispose();

    /// <summary>
    /// Public so test setup can mint a deterministic root cert + key in
    /// memory. Production callers go through the constructor.
    /// </summary>
    public static X509Certificate2 LoadRootCertWithKey(string certPath, string keyPath, string? keyPassword)
    {
        X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile(certPath);

        string keyPem = File.ReadAllText(keyPath);

        // Try RSA first; fall back to ECDsa.
        try
        {
            using var rsa = RSA.Create();
            if (string.IsNullOrEmpty(keyPassword))
                rsa.ImportFromPem(keyPem);
            else
                rsa.ImportFromEncryptedPem(keyPem, keyPassword);
            return cert.CopyWithPrivateKey(rsa);
        }
        catch (ArgumentException)
        {
            // Not an RSA key — try ECDsa.
            using var ecdsa = ECDsa.Create();
            if (string.IsNullOrEmpty(keyPassword))
                ecdsa.ImportFromPem(keyPem);
            else
                ecdsa.ImportFromEncryptedPem(keyPem, keyPassword);
            return cert.CopyWithPrivateKey(ecdsa);
        }
    }

    private static void ValidatePublicKeyStrength(CertificateRequest request)
    {
        PublicKey pk = request.PublicKey;

        using RSA? rsa = pk.GetRSAPublicKey();
        if (rsa is not null)
        {
            if (rsa.KeySize < MinRsaKeySizeBits)
            {
                throw new InvalidOperationException(
                    $"CSR public key is RSA-{rsa.KeySize}; minimum is RSA-{MinRsaKeySizeBits}.");
            }
            return;
        }

        using ECDsa? ecdsa = pk.GetECDsaPublicKey();
        if (ecdsa is not null)
        {
            if (ecdsa.KeySize < MinEcdsaKeySizeBits)
            {
                throw new InvalidOperationException(
                    $"CSR public key is ECDsa-{ecdsa.KeySize}; minimum is ECDsa-{MinEcdsaKeySizeBits} (P-256).");
            }
            return;
        }

        throw new InvalidOperationException("CSR public key is neither RSA nor ECDsa; only those algorithms are supported.");
    }
}
