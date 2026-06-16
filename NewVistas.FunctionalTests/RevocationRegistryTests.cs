// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging.Abstractions;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure.Federation;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Tests for the revocation registry grain and the in-memory cache that
/// the auth handler reads from. Uses <see cref="SharedCluster"/> for a
/// real grain factory; the cache test exercises the grain → cache →
/// IsRevoked path end-to-end.
/// </summary>
[TestFixture]
public class RevocationRegistryTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IRevocationRegistryGrain Registry() =>
        _cluster.GrainFactory.GetGrain<IRevocationRegistryGrain>(IRevocationRegistryGrain.GlobalKey);

    /// <summary>
    /// Each test uses a unique thumbprint so the SharedCluster's persistent
    /// state across tests doesn't cause cross-test contamination.
    /// </summary>
    private static string FreshThumbprint() =>
        Guid.NewGuid().ToString("N").ToUpperInvariant() + "AAAAAAAA";

    [Test]
    public async Task Revoke_AddsToList_AndIsRevokedReturnsTrue()
    {
        string thumbprint = FreshThumbprint();
        IRevocationRegistryGrain registry = Registry();

        await registry.RevokeAsync(thumbprint, "TEST-CLUSTER", "key compromise", "admin-1");

        Assert.That(await registry.IsRevokedAsync(thumbprint), Is.True);
        IReadOnlyList<RevocationRecord> all = await registry.GetAllAsync();
        Assert.That(all.Any(r => r.Thumbprint == thumbprint), Is.True);
        RevocationRecord rec = all.Single(r => r.Thumbprint == thumbprint);
        Assert.That(rec.ClusterId, Is.EqualTo("TEST-CLUSTER"));
        Assert.That(rec.Reason, Is.EqualTo("key compromise"));
        Assert.That(rec.RevokedByUserId, Is.EqualTo("admin-1"));
    }

    [Test]
    public async Task Revoke_IsIdempotent_PreservesOriginalRecord()
    {
        string thumbprint = FreshThumbprint();
        IRevocationRegistryGrain registry = Registry();

        await registry.RevokeAsync(thumbprint, "CLUSTER-A", "first reason", "admin-1");
        DateTime firstTimestamp = (await registry.GetAllAsync())
            .Single(r => r.Thumbprint == thumbprint).RevokedUtc;

        await Task.Delay(10);  // ensure clock moves forward
        await registry.RevokeAsync(thumbprint, "CLUSTER-B", "second reason", "admin-2");

        // Second call must NOT clobber the original — re-revoke is a no-op.
        IReadOnlyList<RevocationRecord> matching = (await registry.GetAllAsync())
            .Where(r => r.Thumbprint == thumbprint).ToList();
        Assert.That(matching, Has.Count.EqualTo(1));
        Assert.That(matching[0].Reason, Is.EqualTo("first reason"));
        Assert.That(matching[0].RevokedUtc, Is.EqualTo(firstTimestamp));
    }

    [Test]
    public async Task IsRevokedAsync_UnknownThumbprint_ReturnsFalse()
    {
        Assert.That(await Registry().IsRevokedAsync(FreshThumbprint()), Is.False);
    }

    [Test]
    public async Task Revoke_NormalizesThumbprintFormat()
    {
        string raw = FreshThumbprint();
        // Caller passes lowercase + colons (a common copy-paste shape).
        string asPassed = string.Join(":", Enumerable.Range(0, raw.Length / 2)
            .Select(i => raw.Substring(i * 2, 2))).ToLowerInvariant();

        await Registry().RevokeAsync(asPassed, "CLUSTER", "test", "admin");

        // Lookup with the normalized form should succeed.
        Assert.That(await Registry().IsRevokedAsync(raw), Is.True);
    }

    // ── Cache → grain integration ────────────────────────────────────────────

    [Test]
    public async Task Cache_RefreshFromGrain_PicksUpNewRevocations()
    {
        string thumbprint = FreshThumbprint();
        var cache = new InMemoryRevocationCache(
            _cluster.GrainFactory, NullLogger<InMemoryRevocationCache>.Instance);

        // Cold cache: not revoked.
        await cache.RefreshAsync(CancellationToken.None);
        Assert.That(cache.IsRevoked(thumbprint), Is.False);

        // Revoke via the grain, refresh, observe the flip.
        await Registry().RevokeAsync(thumbprint, "CLUSTER", "cache test", "admin");
        await cache.RefreshAsync(CancellationToken.None);
        Assert.That(cache.IsRevoked(thumbprint), Is.True);
    }
}
