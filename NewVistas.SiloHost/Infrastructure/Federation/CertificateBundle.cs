// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Helpers for the renewal flow: build a CSR, build a PFX from a fresh
/// keypair + a returned cert PEM. Kept in one place so the renewal service
/// reads cleanly.
/// </summary>
internal static class CertificateBundle
{
    /// <summary>
    /// Generate a fresh RSA-2048 keypair and a PKCS#10 CSR with the given CN.
    /// Returns the CSR as PEM and the keypair as a byte[] holding the
    /// pkcs#8-encoded private key. Caller is responsible for combining the
    /// key with the issued cert into a PFX (see <see cref="BuildPfx"/>).
    /// </summary>
    public static (string CsrPem, byte[] PrivateKeyPkcs8Der) GenerateRenewalCsr(string commonName)
    {
        if (string.IsNullOrWhiteSpace(commonName))
            throw new ArgumentException("Common name is required.", nameof(commonName));

        // Caller can't reuse the RSA after this method (we Dispose it).
        // The pkcs#8 export captures the private key for later reattachment.
        using var rsa = RSA.Create(2048);
        byte[] pkcs8 = rsa.ExportPkcs8PrivateKey();

        var request = new CertificateRequest(
            $"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        byte[] csrDer = request.CreateSigningRequest();

        string csrPem = PemEncoding.WriteString("CERTIFICATE REQUEST", csrDer);
        return (csrPem, pkcs8);
    }

    /// <summary>
    /// Combine a freshly-issued cert (PEM) with the corresponding private key
    /// (pkcs#8 DER) into a PFX byte stream protected by <paramref name="password"/>
    /// (which may be empty). Suitable for writing to <c>ClientCertPath</c>.
    /// </summary>
    public static byte[] BuildPfx(string certPem, byte[] privateKeyPkcs8Der, string? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(certPem);
        ArgumentNullException.ThrowIfNull(privateKeyPkcs8Der);

        using X509Certificate2 leaf = X509CertificateLoader.LoadCertificate(
            PemToDer(certPem, "CERTIFICATE"));

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyPkcs8Der, out _);

        using X509Certificate2 leafWithKey = leaf.CopyWithPrivateKey(rsa);
        return leafWithKey.Export(X509ContentType.Pfx, password ?? string.Empty);
    }

    /// <summary>
    /// Decode a single-block PEM string into raw DER bytes. <paramref name="expectedLabel"/>
    /// is the expected armor (e.g. <c>"CERTIFICATE"</c>); mismatches throw.
    /// </summary>
    private static byte[] PemToDer(string pem, string expectedLabel)
    {
        ReadOnlySpan<char> span = pem.AsSpan();
        PemFields fields = PemEncoding.Find(span);
        ReadOnlySpan<char> actualLabel = span[fields.Label];
        if (!actualLabel.SequenceEqual(expectedLabel.AsSpan()))
        {
            throw new InvalidOperationException(
                $"Expected PEM label '{expectedLabel}', got '{actualLabel}'.");
        }
        return Convert.FromBase64String(span[fields.Base64Data].ToString());
    }
}
