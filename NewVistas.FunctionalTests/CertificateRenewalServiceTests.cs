// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Federation;
using NewVistas.SiloHost.Infrastructure.Federation;
using NewVistas.WebServer.Infrastructure.Federation;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Behavioural tests for <see cref="CertificateRenewalService"/>. Uses a
/// real in-memory hub-CA to mint test certs, a fake
/// <see cref="ICertificateAuthorityClient"/>, and a per-test temp directory
/// so file-swap mechanics are exercised without touching real disk paths.
/// </summary>
[TestFixture]
public class CertificateRenewalServiceTests
{
    private const string TestClusterId = "TEST-SPOKE";
    private const string TestPfxPassword = "";

    private string _tempDir = default!;
    private string _certPath = default!;

    private X509Certificate2 _caRoot = default!;
    private HubCertificateAuthority _ca = default!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"newvistas-renewal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _certPath = Path.Combine(_tempDir, "spoke.pfx");

        // Per-test in-memory CA — keeps tests independent.
        using var rsa = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=TEST-CA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature, true));
        _caRoot = rootRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        _ca = new HubCertificateAuthority(_caRoot);
    }

    [TearDown]
    public void Teardown()
    {
        _ca?.Dispose();
        _caRoot?.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>Builds an in-memory PFX with a leaf cert that expires <paramref name="daysFromNow"/> days out.</summary>
    private byte[] BuildLeafPfx(int daysFromNow)
    {
        // Generate spoke key + CSR, then run it through the CA with a custom
        // validity window. We can't pass the validity through IssueCertificate
        // exactly (it bakes -5min backdate), so we bypass and build directly.
        using var spokeKey = RSA.Create(2048);
        var spokeRequest = new CertificateRequest(
            $"CN={TestClusterId}", spokeKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        byte[] csrDer = spokeRequest.CreateSigningRequest();

        TimeSpan validity = daysFromNow > 0
            ? TimeSpan.FromDays(daysFromNow)
            : TimeSpan.FromMinutes(1);  // already-expired-ish: NotAfter just past
        using X509Certificate2 leaf = _ca.IssueCertificate(csrDer, validity);
        using X509Certificate2 leafWithKey = leaf.CopyWithPrivateKey(spokeKey);
        return leafWithKey.Export(X509ContentType.Pfx, TestPfxPassword);
    }

    private void WriteLeafPfx(int daysFromNow)
    {
        File.WriteAllBytes(_certPath, BuildLeafPfx(daysFromNow));
    }

    private CertificateRenewalService BuildService(
        ICertificateAuthorityClient caClient,
        int renewBeforeExpiryDays = 30)
    {
        var renewal = Options.Create(new RenewalOptions
        {
            Enabled = true,
            CheckIntervalHours = 6,
            RenewBeforeExpiryDays = renewBeforeExpiryDays,
            Url = "https://hub.test/csr/renew",
        });
        var http = Options.Create(new HttpFederationTransportOptions
        {
            ClientCertPath = _certPath,
            ClientCertPassword = TestPfxPassword,
        });
        var clusterIdentity = new StaticClusterIdentity(TestClusterId, "099");

        return new CertificateRenewalService(
            renewal, http, clusterIdentity, caClient,
            NullLogger<CertificateRenewalService>.Instance);
    }

    /// <summary>
    /// Real CA-backed client that signs whatever CSR comes in. Mirrors the
    /// HTTP path's behavior without an HTTP server.
    /// </summary>
    private sealed class StubCaClient : ICertificateAuthorityClient
    {
        private readonly HubCertificateAuthority _ca;
        public int CallCount { get; private set; }
        public string? LastCsrPem { get; private set; }

        public StubCaClient(HubCertificateAuthority ca) => _ca = ca;

        public Task<RenewalResponse> RenewAsync(string csrPem, CancellationToken cancellationToken)
        {
            CallCount++;
            LastCsrPem = csrPem;

            byte[] csrDer = PemToDer(csrPem, "CERTIFICATE REQUEST");
            using X509Certificate2 newLeaf = _ca.IssueCertificate(csrDer, TimeSpan.FromDays(365));
            using X509Certificate2 root = _ca.RootCertificate;
            return Task.FromResult(new RenewalResponse(
                CertPem: PemEncoding.WriteString("CERTIFICATE", newLeaf.Export(X509ContentType.Cert)),
                CaCertPem: PemEncoding.WriteString("CERTIFICATE", root.Export(X509ContentType.Cert))));
        }

        private static byte[] PemToDer(string pem, string expectedLabel)
        {
            ReadOnlySpan<char> span = pem.AsSpan();
            PemFields fields = PemEncoding.Find(span);
            return Convert.FromBase64String(span[fields.Base64Data].ToString());
        }
    }

    private sealed class ThrowingCaClient : ICertificateAuthorityClient
    {
        public int CallCount { get; private set; }
        public Task<RenewalResponse> RenewAsync(string csrPem, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new HttpRequestException("simulated hub down");
        }
    }

    private sealed class WrongCnCaClient : ICertificateAuthorityClient
    {
        private readonly HubCertificateAuthority _ca;
        public WrongCnCaClient(HubCertificateAuthority ca) => _ca = ca;

        public Task<RenewalResponse> RenewAsync(string csrPem, CancellationToken cancellationToken)
        {
            // Issue a cert for "WRONG-SPOKE" no matter what was asked for.
            using var key = RSA.Create(2048);
            var fakeRequest = new CertificateRequest("CN=WRONG-SPOKE", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            byte[] fakeCsr = fakeRequest.CreateSigningRequest();
            using X509Certificate2 leaf = _ca.IssueCertificate(fakeCsr, TimeSpan.FromDays(365));
            using X509Certificate2 root = _ca.RootCertificate;
            return Task.FromResult(new RenewalResponse(
                PemEncoding.WriteString("CERTIFICATE", leaf.Export(X509ContentType.Cert)),
                PemEncoding.WriteString("CERTIFICATE", root.Export(X509ContentType.Cert))));
        }
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Test]
    public async Task NoRenewalNeeded_LeavesFileUntouched()
    {
        WriteLeafPfx(daysFromNow: 90);
        DateTime originalWriteTime = File.GetLastWriteTimeUtc(_certPath);
        long originalLength = new FileInfo(_certPath).Length;

        var stub = new StubCaClient(_ca);
        CertificateRenewalService service = BuildService(stub, renewBeforeExpiryDays: 30);

        await Task.Delay(20);  // ensure any timestamp tick
        await service.TryRenewOnceAsync(CancellationToken.None);

        Assert.That(stub.CallCount, Is.EqualTo(0));
        Assert.That(File.GetLastWriteTimeUtc(_certPath), Is.EqualTo(originalWriteTime).Within(TimeSpan.FromMilliseconds(50)));
        Assert.That(new FileInfo(_certPath).Length, Is.EqualTo(originalLength));
    }

    [Test]
    public async Task RenewalDue_SwapsFileAndBacksUpPrevious()
    {
        WriteLeafPfx(daysFromNow: 10);  // within 30-day threshold
        byte[] originalBytes = File.ReadAllBytes(_certPath);

        var stub = new StubCaClient(_ca);
        CertificateRenewalService service = BuildService(stub, renewBeforeExpiryDays: 30);

        await service.TryRenewOnceAsync(CancellationToken.None);

        Assert.That(stub.CallCount, Is.EqualTo(1));
        Assert.That(File.Exists(_certPath), Is.True);
        Assert.That(File.Exists(_certPath + ".previous"), Is.True);
        Assert.That(File.Exists(_certPath + ".new"), Is.False, "Temp .new file should have been renamed away.");

        byte[] newBytes = File.ReadAllBytes(_certPath);
        Assert.That(newBytes, Is.Not.EqualTo(originalBytes));

        byte[] backupBytes = File.ReadAllBytes(_certPath + ".previous");
        Assert.That(backupBytes, Is.EqualTo(originalBytes));

        // New cert should have a NotAfter ~365 days out (the StubCaClient's validity).
        using X509Certificate2 newCert = X509CertificateLoader.LoadPkcs12FromFile(_certPath, TestPfxPassword);
        TimeSpan untilExpiry = newCert.NotAfter.ToUniversalTime() - DateTime.UtcNow;
        Assert.That(untilExpiry, Is.GreaterThan(TimeSpan.FromDays(360)));
    }

    [Test]
    public async Task RenewalDue_CaFails_LeavesFileUntouched()
    {
        WriteLeafPfx(daysFromNow: 10);
        byte[] originalBytes = File.ReadAllBytes(_certPath);

        var stub = new ThrowingCaClient();
        CertificateRenewalService service = BuildService(stub, renewBeforeExpiryDays: 30);

        await service.TryRenewOnceAsync(CancellationToken.None);

        Assert.That(stub.CallCount, Is.EqualTo(1));
        Assert.That(File.Exists(_certPath + ".previous"), Is.False);
        Assert.That(File.ReadAllBytes(_certPath), Is.EqualTo(originalBytes));
    }

    [Test]
    public async Task RenewalDue_HubReturnsWrongCn_RefusesToInstall()
    {
        WriteLeafPfx(daysFromNow: 10);
        byte[] originalBytes = File.ReadAllBytes(_certPath);

        var stub = new WrongCnCaClient(_ca);
        CertificateRenewalService service = BuildService(stub, renewBeforeExpiryDays: 30);

        await service.TryRenewOnceAsync(CancellationToken.None);

        // Defensive guard: hub returned a cert with the wrong CN, service refuses to swap.
        Assert.That(File.Exists(_certPath + ".previous"), Is.False);
        Assert.That(File.ReadAllBytes(_certPath), Is.EqualTo(originalBytes));
    }

    [Test]
    public async Task NoCertFile_LogsAndExitsCleanly()
    {
        // No file at _certPath.
        var stub = new StubCaClient(_ca);
        CertificateRenewalService service = BuildService(stub);

        Assert.That(
            async () => await service.TryRenewOnceAsync(CancellationToken.None),
            Throws.Nothing);
        Assert.That(stub.CallCount, Is.EqualTo(0));
    }

    [Test]
    public void AtomicSwap_RenamesNewOverLive_AndBacksUpPrevious()
    {
        // Direct test of the atomic-swap helper without going through the service.
        File.WriteAllBytes(_certPath, new byte[] { 0x01, 0x02 });
        byte[] newBytes = new byte[] { 0xAA, 0xBB, 0xCC };

        CertificateRenewalService.AtomicSwap(_certPath, newBytes);

        Assert.That(File.ReadAllBytes(_certPath), Is.EqualTo(newBytes));
        Assert.That(File.ReadAllBytes(_certPath + ".previous"), Is.EqualTo(new byte[] { 0x01, 0x02 }));
        Assert.That(File.Exists(_certPath + ".new"), Is.False);
    }
}
