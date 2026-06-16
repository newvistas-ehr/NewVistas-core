// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Tests for the dashboard's grain-side data sources:
///   - <see cref="IProvisioningTokenIndexGrain"/> — append, mark consumed, list.
///   - <see cref="IFederationStatsGrain"/> — wraps <see cref="IOutboxStatistics"/>.
///
/// The grain factory is taken from <see cref="SharedCluster"/>; the
/// <see cref="IOutboxStatistics"/> registered there is the no-op (no real
/// outbox in tests), so the stats-grain tests assert the no-op shape. SQL
/// implementation is tested via the existing outbox tests.
/// </summary>
[TestFixture]
public class FederationDashboardTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IProvisioningTokenIndexGrain Index() =>
        _cluster.GrainFactory.GetGrain<IProvisioningTokenIndexGrain>(IProvisioningTokenIndexGrain.GlobalKey);

    private IFederationStatsGrain Stats() =>
        _cluster.GrainFactory.GetGrain<IFederationStatsGrain>(IFederationStatsGrain.GlobalKey);

    /// <summary>Each test uses unique tokens so SharedCluster persistence doesn't bleed.</summary>
    private static string FreshToken() => $"tok-{Guid.NewGuid():N}";

    // ── Provisioning token index ─────────────────────────────────────────────

    [Test]
    public async Task TokenIndex_AddThenList_ContainsEntry()
    {
        string token = FreshToken();
        DateTime issuedUtc = DateTime.UtcNow;
        DateTime expiresUtc = issuedUtc.AddHours(24);

        await Index().AddAsync(token, "TEST-CLUSTER", issuedUtc, expiresUtc);

        IReadOnlyList<ProvisioningTokenSummary> all = await Index().GetAllAsync();
        ProvisioningTokenSummary? entry = all.FirstOrDefault(t => t.Token == token);

        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ClusterId, Is.EqualTo("TEST-CLUSTER"));
        Assert.That(entry.ExpiresUtc, Is.EqualTo(expiresUtc));
        Assert.That(entry.ConsumedUtc, Is.Null);
    }

    [Test]
    public async Task TokenIndex_AddIsIdempotent_ByTokenString()
    {
        string token = FreshToken();
        DateTime first = DateTime.UtcNow;

        await Index().AddAsync(token, "CLUSTER-A", first, first.AddHours(1));
        await Task.Delay(10);
        await Index().AddAsync(token, "CLUSTER-B", first.AddSeconds(5), first.AddHours(2));

        IReadOnlyList<ProvisioningTokenSummary> matching =
            (await Index().GetAllAsync()).Where(t => t.Token == token).ToList();

        Assert.That(matching, Has.Count.EqualTo(1));
        Assert.That(matching[0].ClusterId, Is.EqualTo("CLUSTER-A"),
            "Re-add should preserve the original entry, not overwrite it.");
    }

    [Test]
    public async Task TokenIndex_MarkConsumed_UpdatesEntry()
    {
        string token = FreshToken();
        DateTime issuedUtc = DateTime.UtcNow;

        await Index().AddAsync(token, "CLUSTER-A", issuedUtc, issuedUtc.AddHours(24));
        DateTime consumedAt = DateTime.UtcNow;
        await Index().MarkConsumedAsync(token, consumedAt, "ABCDEF1234567890");

        ProvisioningTokenSummary entry = (await Index().GetAllAsync())
            .Single(t => t.Token == token);

        Assert.That(entry.ConsumedUtc, Is.EqualTo(consumedAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(entry.ConsumedByThumbprint, Is.EqualTo("ABCDEF1234567890"));
    }

    [Test]
    public async Task TokenIndex_MarkConsumed_UnknownToken_IsNoOp()
    {
        string unknownToken = FreshToken();

        Assert.That(
            async () => await Index().MarkConsumedAsync(unknownToken, DateTime.UtcNow, "thumb"),
            Throws.Nothing);

        Assert.That((await Index().GetAllAsync()).Any(t => t.Token == unknownToken), Is.False);
    }

    [Test]
    public async Task TokenIndex_GetAll_OrdersByIssuedDescending()
    {
        // Three tokens, ascending issued times; expect listing in descending order.
        string token1 = FreshToken();
        string token2 = FreshToken();
        string token3 = FreshToken();

        DateTime t0 = DateTime.UtcNow.AddMinutes(-30);
        await Index().AddAsync(token1, "C", t0, t0.AddHours(1));
        await Index().AddAsync(token2, "C", t0.AddMinutes(10), t0.AddHours(2));
        await Index().AddAsync(token3, "C", t0.AddMinutes(20), t0.AddHours(3));

        List<ProvisioningTokenSummary> our = (await Index().GetAllAsync())
            .Where(t => t.Token == token1 || t.Token == token2 || t.Token == token3)
            .ToList();

        Assert.That(our.Select(t => t.Token).ToArray(),
            Is.EqualTo(new[] { token3, token2, token1 }));
    }

    // ── Federation stats grain ───────────────────────────────────────────────

    [Test]
    public async Task StatsGrain_NoOutboxConfigured_ReturnsNotAvailable()
    {
        // SharedCluster registers the NoOp implementation since there's no
        // SQL outbox in the test environment. Assert the grain surfaces
        // the not-available shape that the controller turns into 404.
        OutboxStats stats = await Stats().GetOutboxStatsAsync();

        Assert.That(stats.Available, Is.False);
        Assert.That(stats.Pending, Is.EqualTo(0));
        Assert.That(stats.Sent, Is.EqualTo(0));
        Assert.That(stats.OldestPendingUtc, Is.Null);
        Assert.That(stats.LastSentUtc, Is.Null);
    }
}
