// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Behavioural tests for <see cref="IIcnIssuerGrain"/>. Uses its own
/// <see cref="TestCluster"/> so it can register a known
/// <see cref="IClusterIdentity"/> with a deterministic prefix.
/// </summary>
[TestFixture, NonParallelizable]
public class IcnIssuerGrainTests
{
    /// <summary>3-digit cluster prefix the test silo will issue ICNs under.</summary>
    public const string TestPrefix = "518";

    private static readonly Regex IcnPattern =
        new(@"^[0-9]{10}V[0-9]{6}$", RegexOptions.Compiled);

    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<IcnIssuerSiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    private IIcnIssuerGrain Issuer() =>
        _cluster.GrainFactory.GetGrain<IIcnIssuerGrain>("ICN-ISSUER");

    [Test]
    public async Task IssueNextAsync_FirstCall_MatchesIcnFormat()
    {
        string icn = await Issuer().IssueNextAsync();
        Assert.That(IcnPattern.IsMatch(icn), Is.True,
            $"ICN '{icn}' does not match the format {{10digit}}V{{6digit}}.");
    }

    [Test]
    public async Task IssueNextAsync_PrefixComesFromClusterIdentity()
    {
        string icn = await Issuer().IssueNextAsync();
        Assert.That(icn.StartsWith(TestPrefix), Is.True,
            $"ICN '{icn}' should start with cluster prefix '{TestPrefix}'.");
    }

    [Test]
    public async Task IssueNextAsync_SequentialCallsReturnDistinctValues()
    {
        string a = await Issuer().IssueNextAsync();
        string b = await Issuer().IssueNextAsync();
        Assert.That(b, Is.Not.EqualTo(a));
    }

    [Test]
    public async Task IssueNextAsync_SequentialCallsAreMonotonic()
    {
        string a = await Issuer().IssueNextAsync();
        string b = await Issuer().IssueNextAsync();
        // Sequence portion is positions 3..9 (7 digits).
        long seqA = long.Parse(a.Substring(3, 7));
        long seqB = long.Parse(b.Substring(3, 7));
        Assert.That(seqB, Is.GreaterThan(seqA));
    }

    [Test]
    public async Task PeekNextSequenceAsync_AfterIssue_ReturnsAdvancedValue()
    {
        long before = await Issuer().PeekNextSequenceAsync();
        await Issuer().IssueNextAsync();
        long after = await Issuer().PeekNextSequenceAsync();
        Assert.That(after, Is.EqualTo(before + 1));
    }

    [Test]
    public async Task IssueNextAsync_ChecksumIsDeterministicForSamePrefixAndSequence()
    {
        // Burn the issuer up to a known sequence, then capture; we cannot
        // reset the issuer between tests within a OneTime fixture, so this
        // test simply asserts that the checksum field is always the same
        // for a given prefix+sequence by recomputing it externally.
        string icn = await Issuer().IssueNextAsync();
        string prefixAndSeq = icn.Substring(0, 10);
        string emittedChecksum = icn.Substring(11);
        string expected = NewVistas.Abstractions.Helpers.IcnChecksumCalculator
            .Compute(prefixAndSeq);
        Assert.That(emittedChecksum, Is.EqualTo(expected));
    }

    // ── Configuration plumbing ───────────────────────────────────────────────

    private sealed class IcnIssuerSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("icnIssuerStore");
            siloBuilder.Services.AddSingleton<IClusterIdentity>(
                new StaticClusterIdentity("TEST-ISSUER-CLUSTER", TestPrefix));
        }
    }
}
