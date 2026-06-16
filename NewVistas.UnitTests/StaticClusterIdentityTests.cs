// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Federation;

namespace NewVistas.UnitTests;

/// <summary>
/// Validates the constructor contract on <see cref="StaticClusterIdentity"/>:
/// the local cluster id and the 3-digit ICN prefix from
/// <c>ClusterPrefixAllocations.md</c> must both be present and well-formed.
/// </summary>
[TestFixture]
public class StaticClusterIdentityTests
{
    [Test]
    public void Constructor_AcceptsValidPrefix()
    {
        var id = new StaticClusterIdentity("SPOKE-BEDFORD", "518");
        Assert.That(id.LocalClusterId, Is.EqualTo("SPOKE-BEDFORD"));
        Assert.That(id.IcnPrefix, Is.EqualTo("518"));
    }

    [Test]
    public void Constructor_AllowsLeadingZeros()
    {
        var id = new StaticClusterIdentity("HUB-PRIMARY", "001");
        Assert.That(id.IcnPrefix, Is.EqualTo("001"));
    }

    [Test]
    public void Constructor_AllowsAllZeros()
    {
        var id = new StaticClusterIdentity("DEV-LOCAL", "000");
        Assert.That(id.IcnPrefix, Is.EqualTo("000"));
    }

    [Test]
    public void Constructor_RejectsEmptyClusterId()
    {
        Assert.That(
            () => new StaticClusterIdentity("", "001"),
            Throws.ArgumentException);
    }

    [Test]
    public void Constructor_RejectsWhitespaceClusterId()
    {
        Assert.That(
            () => new StaticClusterIdentity("   ", "001"),
            Throws.ArgumentException);
    }

    [Test]
    public void Constructor_RejectsEmptyPrefix()
    {
        Assert.That(
            () => new StaticClusterIdentity("HUB", ""),
            Throws.ArgumentException);
    }

    [Test]
    public void Constructor_RejectsNonNumericPrefix()
    {
        Assert.That(
            () => new StaticClusterIdentity("HUB", "abc"),
            Throws.ArgumentException);
    }

    [Test]
    public void Constructor_RejectsMixedAlphanumericPrefix()
    {
        Assert.That(
            () => new StaticClusterIdentity("HUB", "5A8"),
            Throws.ArgumentException);
    }

    [TestCase("1")]
    [TestCase("12")]
    [TestCase("1234")]
    [TestCase("12345")]
    public void Constructor_RejectsWrongLengthPrefix(string badPrefix)
    {
        Assert.That(
            () => new StaticClusterIdentity("HUB", badPrefix),
            Throws.ArgumentException);
    }

    [Test]
    public void Constructor_RejectsPrefixWithLeadingSpace()
    {
        Assert.That(
            () => new StaticClusterIdentity("HUB", " 18"),
            Throws.ArgumentException);
    }
}
