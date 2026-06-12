// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the version/snapshot/delta protocol on PatientIndexGrain that
/// backs the PatientSearchGrain StatelessWorker readers. The subtle invariants
/// (monotonic version, delta vs. full-snapshot fallback when a reader has fallen
/// behind the in-memory change ring) are exercised here directly — the
/// functional MultiSiloIndexReaderTests only cover them end-to-end.
/// </summary>
[TestFixture]
public class PatientIndexVersioningTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    // A fresh, isolated index per test (constant-key singleton in production,
    // but the test harness lets us key it per case so versions start at 0).
    private IPatientIndexGrain Index() =>
        _cluster.GrainFactory.GetGrain<IPatientIndexGrain>($"PIDX-VER:{Guid.NewGuid()}");

    private static PatientIndexEntry Entry(string id, string name) =>
        new() { PatientId = id, Name = name, Sex = "M", IsActive = true };

    [Test]
    public async Task Version_StartsAtZero()
        => Assert.That(await Index().GetVersionAsync(), Is.EqualTo(0));

    [Test]
    public async Task AddOrUpdate_IncrementsVersion()
    {
        IPatientIndexGrain index = Index();

        await index.AddOrUpdateAsync(Entry("P1", "A,A"));
        Assert.That(await index.GetVersionAsync(), Is.EqualTo(1));

        await index.AddOrUpdateAsync(Entry("P2", "B,B"));
        Assert.That(await index.GetVersionAsync(), Is.EqualTo(2));

        // Re-upserting the same patient still bumps the version (the entry changed).
        await index.AddOrUpdateAsync(Entry("P1", "A,RENAMED"));
        Assert.That(await index.GetVersionAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task Snapshot_ReturnsAllEntriesAtCurrentVersion()
    {
        IPatientIndexGrain index = Index();
        await index.AddOrUpdateAsync(Entry("P1", "A,A"));
        await index.AddOrUpdateAsync(Entry("P2", "B,B"));

        PatientIndexSnapshot snapshot = await index.GetSnapshotAsync();

        Assert.That(snapshot.Version, Is.EqualTo(2));
        Assert.That(snapshot.Entries.Select(e => e.PatientId), Is.EquivalentTo(new[] { "P1", "P2" }));
    }

    [Test]
    public async Task ChangesSince_CurrentVersion_EmptyDelta()
    {
        IPatientIndexGrain index = Index();
        await index.AddOrUpdateAsync(Entry("P1", "A,A"));
        long current = await index.GetVersionAsync();

        PatientIndexDelta delta = await index.GetChangesSinceAsync(current);

        Assert.That(delta.SnapshotRequired, Is.False);
        Assert.That(delta.Changes, Is.Empty);
        Assert.That(delta.Version, Is.EqualTo(current));
    }

    [Test]
    public async Task ChangesSince_OlderVersion_ReturnsOnlySubsequentChanges()
    {
        IPatientIndexGrain index = Index();
        await index.AddOrUpdateAsync(Entry("P1", "A,A")); // version 1
        await index.AddOrUpdateAsync(Entry("P2", "B,B")); // version 2
        await index.AddOrUpdateAsync(Entry("P3", "C,C")); // version 3

        PatientIndexDelta delta = await index.GetChangesSinceAsync(1);

        Assert.That(delta.SnapshotRequired, Is.False);
        Assert.That(delta.Version, Is.EqualTo(3));
        Assert.That(delta.Changes.Select(c => c.Entry.PatientId), Is.EqualTo(new[] { "P2", "P3" }));
        Assert.That(delta.Changes.All(c => c.Version > 1), Is.True);
    }

    [Test]
    public async Task ChangesSince_AheadOfCurrentVersion_RequiresSnapshot()
    {
        IPatientIndexGrain index = Index();
        await index.AddOrUpdateAsync(Entry("P1", "A,A"));

        // A reader claiming a version newer than the index has (e.g. talking to
        // a different index instance) must be told to re-pull from scratch.
        PatientIndexDelta delta = await index.GetChangesSinceAsync(99);

        Assert.That(delta.SnapshotRequired, Is.True);
    }

    [Test]
    public async Task ChangesSince_BeyondChangeRing_RequiresSnapshot()
    {
        // The in-memory change ring caps at 1000 entries. A reader that has
        // fallen further behind than the ring retains must take a full
        // snapshot rather than silently applying a partial delta and missing
        // patients. Push past the ring so version 1 is evicted.
        IPatientIndexGrain index = Index();
        for (int i = 0; i < 1001; i++)
            await index.AddOrUpdateAsync(Entry($"P{i}", $"NAME,{i}"));

        PatientIndexDelta delta = await index.GetChangesSinceAsync(0);

        Assert.That(delta.SnapshotRequired, Is.True,
            "A reader behind the change ring must be forced to a full snapshot.");

        // A reader still within the ring window gets a normal delta.
        PatientIndexDelta recent = await index.GetChangesSinceAsync(1000);
        Assert.That(recent.SnapshotRequired, Is.False);
        Assert.That(recent.Changes, Has.Count.EqualTo(1));
    }
}
