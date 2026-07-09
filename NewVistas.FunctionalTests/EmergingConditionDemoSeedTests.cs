// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging.Abstractions;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Runs the real <see cref="EmergingConditionSeed"/> end to end and asserts the demo's headline
/// invariant: nine members are pre-confirmed with the threshold-10 alert armed, so the tenth confirm
/// (done live in the demo) fires the alert exactly once. NonParallelizable — it seeds ~43 patients
/// and asserts on a fixed proto id.
/// </summary>
[TestFixture, NonParallelizable]
public class EmergingConditionDemoSeedTests
{
    private TestCluster _cluster = null!;
    private const string ProtoId = "outbreak-2019-resp";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    [Test]
    public async Task Seed_ProducesNineConfirmed_TenthConfirmFiresAlert()
    {
        await EmergingConditionSeed.SeedAsync(_cluster.GrainFactory, NullLogger.Instance);

        IProtoConditionGrain proto = _cluster.GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{ProtoId}");
        ProtoConditionState state = await proto.GetAsync();

        Assert.That(state.Status, Is.EqualTo(ProtoConditionStatus.Active));
        Assert.That(state.Members.Count(m => m.Status == ProtoMemberStatus.Confirmed), Is.EqualTo(9));
        Assert.That(state.Members.Count(m => m.Status == ProtoMemberStatus.Candidate), Is.EqualTo(4));
        Assert.That(state.AlertRule!.Threshold, Is.EqualTo(10));
        Assert.That(state.AlertRule.TimesFired, Is.EqualTo(0), "alert must not have fired at 9 confirmed");

        // The live demo action: confirm the 10th member (P9210) → threshold reached → alert fires once.
        await proto.ConfirmMemberAsync("P9210", "QM3");

        ProtoConditionState after = await proto.GetAsync();
        Assert.That(after.Members.Count(m => m.Status == ProtoMemberStatus.Confirmed), Is.EqualTo(10));
        Assert.That(after.AlertRule!.TimesFired, Is.EqualTo(1));
        Assert.That(after.AlertRule.LastFiredCount, Is.EqualTo(10));
    }
}
