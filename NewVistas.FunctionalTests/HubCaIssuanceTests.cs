// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure.Federation;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the hub-CA: in-memory test root, real CSR generated
/// in-test, real signing through <see cref="HubCertificateAuthority"/>, real
/// chain validation. Plus token-grain lifecycle exercised against the
/// <c>SharedCluster</c>.
/// </summary>
[TestFixture]
public class HubCaIssuanceTests
{
    private TestCluster _cluster = default!;
    private X509Certificate2 _testRoot = default!;
    private HubCertificateAuthority _ca = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;

        // Generate an ephemeral test root cert + key in memory. CreateSelfSigned
        // on a CertificateRequest with BasicConstraints(CA=true) gives us a
        // working CA cert without touching the filesystem.
        using var rsa = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=TEST-FEDERATION-CA",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
            critical: true));

        _testRoot = rootRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        _ca = new HubCertificateAuthority(_testRoot);
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _ca?.Dispose();
        _testRoot?.Dispose();
    }

    private static byte[] BuildCsrDer(string commonName, int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest(
            $"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSigningRequest();
    }

    // ── HubCertificateAuthority signing ──────────────────────────────────────

    [Test]
    public void IssueCertificate_ReturnsCertChainedToRoot()
    {
        byte[] csr = BuildCsrDer("KIBALE-UGANDA");

        using X509Certificate2 leaf = _ca.IssueCertificate(csr, TimeSpan.FromDays(365));

        Assert.That(leaf.SubjectName.Name, Does.Contain("CN=KIBALE-UGANDA"));
        Assert.That(leaf.IssuerName.Name, Is.EqualTo(_testRoot.SubjectName.Name));

        // Build a chain rooted at the test root and verify.
        using var chain = new X509Chain
        {
            ChainPolicy = new X509ChainPolicy
            {
                RevocationMode = X509RevocationMode.NoCheck,
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                CustomTrustStore = { _testRoot },
            }
        };
        bool valid = chain.Build(leaf);
        Assert.That(valid, Is.True, $"Chain failed: {string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation))}");
    }

    [Test]
    public void IssueCertificate_AppliesClientAuthEku()
    {
        byte[] csr = BuildCsrDer("TEST-SPOKE");

        using X509Certificate2 leaf = _ca.IssueCertificate(csr, TimeSpan.FromDays(365));

        var eku = leaf.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .Single();
        // ClientAuthentication OID
        Assert.That(eku.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.2"), Is.True);
    }

    [Test]
    public void IssueCertificate_RejectsTooWeakKey()
    {
        byte[] csr = BuildCsrDer("TEST-SPOKE", keySize: 1024);

        Assert.That(
            () => _ca.IssueCertificate(csr, TimeSpan.FromDays(365)),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("RSA-2048"));
    }

    [Test]
    public void IssueCertificate_AppliesValidityWindow()
    {
        byte[] csr = BuildCsrDer("TEST-SPOKE");

        using X509Certificate2 leaf = _ca.IssueCertificate(csr, TimeSpan.FromDays(30));

        DateTime expectedExpiry = DateTime.UtcNow.AddDays(30);
        Assert.That(leaf.NotAfter.ToUniversalTime(),
            Is.EqualTo(expectedExpiry).Within(TimeSpan.FromMinutes(2)));
    }

    // ── Provisioning token grain ─────────────────────────────────────────────

    private IProvisioningTokenGrain TokenGrain(string token) =>
        _cluster.GrainFactory.GetGrain<IProvisioningTokenGrain>(token);

    [Test]
    public async Task TokenGrain_IssueThenConsume_Succeeds()
    {
        string token = $"tok-{Guid.NewGuid():N}";
        IProvisioningTokenGrain grain = TokenGrain(token);

        await grain.IssueAsync("KIBALE-UGANDA", DateTime.UtcNow.AddHours(1));
        await grain.ConsumeAsync("KIBALE-UGANDA", "DEAD0BEEF1234567");

        ProvisioningTokenState state = await grain.GetStateAsync();
        Assert.That(state.IsIssued, Is.True);
        Assert.That(state.ConsumedUtc, Is.Not.Null);
        Assert.That(state.ConsumedByThumbprint, Is.EqualTo("DEAD0BEEF1234567"));
    }

    [Test]
    public async Task TokenGrain_DoubleIssue_Throws()
    {
        string token = $"tok-{Guid.NewGuid():N}";
        IProvisioningTokenGrain grain = TokenGrain(token);

        await grain.IssueAsync("PEER-A", DateTime.UtcNow.AddHours(1));

        Assert.That(
            async () => await grain.IssueAsync("PEER-B", DateTime.UtcNow.AddHours(1)),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task TokenGrain_DoubleConsume_Throws()
    {
        string token = $"tok-{Guid.NewGuid():N}";
        IProvisioningTokenGrain grain = TokenGrain(token);

        await grain.IssueAsync("PEER-A", DateTime.UtcNow.AddHours(1));
        await grain.ConsumeAsync("PEER-A", "thumbprint1");

        Assert.That(
            async () => await grain.ConsumeAsync("PEER-A", "thumbprint2"),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("already been consumed"));
    }

    [Test]
    public async Task TokenGrain_ConsumeBeforeIssue_Throws()
    {
        string token = $"tok-{Guid.NewGuid():N}";
        IProvisioningTokenGrain grain = TokenGrain(token);

        Assert.That(
            async () => await grain.ConsumeAsync("PEER-A", "thumb"),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("Unknown token"));
    }

    [Test]
    public async Task TokenGrain_ConsumeAfterExpiry_Throws()
    {
        string token = $"tok-{Guid.NewGuid():N}";
        IProvisioningTokenGrain grain = TokenGrain(token);

        await grain.IssueAsync("PEER-A", DateTime.UtcNow.AddSeconds(-1));  // already expired

        Assert.That(
            async () => await grain.ConsumeAsync("PEER-A", "thumb"),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("expired"));
    }

    [Test]
    public async Task TokenGrain_ConsumeWithMismatchedClusterId_Throws()
    {
        string token = $"tok-{Guid.NewGuid():N}";
        IProvisioningTokenGrain grain = TokenGrain(token);

        await grain.IssueAsync("PEER-A", DateTime.UtcNow.AddHours(1));

        Assert.That(
            async () => await grain.ConsumeAsync("PEER-B", "thumb"),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("bound to cluster"));
    }
}
