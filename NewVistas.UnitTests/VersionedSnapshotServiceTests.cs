// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests for the two silo-local snapshot holders that back the StatelessWorker
/// readers (drug-interaction checker and patient search). Both implement a
/// version-monotonic Swap so concurrent reader refreshes arriving out of order
/// can never regress the cache to older data. Pure in-memory services — no cluster.
/// </summary>
[TestFixture]
public class VersionedSnapshotServiceTests
{
    // ─── DrugInteractionCacheService ─────────────────────────────────────────

    private static CachedInteractionSnapshot DiSnapshot(long version) =>
        new(version, new Dictionary<string, DrugInteractionPair>
        {
            [$"v{version}"] = new DrugInteractionPair { IngredientIen1 = "A", IngredientIen2 = "B" }
        });

    [Test]
    public void DrugCache_InitiallyEmpty()
    {
        var svc = new DrugInteractionCacheService();

        Assert.That(svc.IsPopulated, Is.False);
        Assert.That(svc.GetSnapshot(), Is.Null);
    }

    [Test]
    public void DrugCache_Swap_InstallsSnapshot()
    {
        var svc = new DrugInteractionCacheService();

        svc.Swap(DiSnapshot(1));

        Assert.That(svc.IsPopulated, Is.True);
        Assert.That(svc.GetSnapshot()!.Version, Is.EqualTo(1));
    }

    [Test]
    public void DrugCache_Swap_HigherVersionReplaces()
    {
        var svc = new DrugInteractionCacheService();
        svc.Swap(DiSnapshot(1));

        svc.Swap(DiSnapshot(5));

        Assert.That(svc.GetSnapshot()!.Version, Is.EqualTo(5));
    }

    [Test]
    public void DrugCache_Swap_LowerVersionIgnored()
    {
        var svc = new DrugInteractionCacheService();
        svc.Swap(DiSnapshot(5));

        svc.Swap(DiSnapshot(2)); // a stale pull arriving late

        Assert.That(svc.GetSnapshot()!.Version, Is.EqualTo(5),
            "A lower-versioned snapshot must not regress the cache.");
    }

    [Test]
    public void DrugCache_Swap_EqualVersionIgnored()
    {
        var svc = new DrugInteractionCacheService();
        CachedInteractionSnapshot first = DiSnapshot(3);
        svc.Swap(first);

        svc.Swap(DiSnapshot(3));

        Assert.That(svc.GetSnapshot(), Is.SameAs(first),
            "An equal-versioned swap is a no-op (keeps the existing instance).");
    }

    // ─── PatientIndexSnapshotService ─────────────────────────────────────────

    private static PatientIndexReadSnapshot IndexSnapshot(long version) =>
        new(version, new Dictionary<string, PatientIndexEntry>
        {
            [$"P{version}"] = new PatientIndexEntry { PatientId = $"P{version}", Name = "X,Y" }
        });

    [Test]
    public void IndexCache_InitiallyEmpty()
        => Assert.That(new PatientIndexSnapshotService().TryGet(), Is.Null);

    [Test]
    public void IndexCache_Swap_InstallsSnapshot()
    {
        var svc = new PatientIndexSnapshotService();

        svc.Swap(IndexSnapshot(1));

        Assert.That(svc.TryGet()!.Version, Is.EqualTo(1));
    }

    [Test]
    public void IndexCache_Swap_HigherVersionReplaces()
    {
        var svc = new PatientIndexSnapshotService();
        svc.Swap(IndexSnapshot(1));

        svc.Swap(IndexSnapshot(9));

        Assert.That(svc.TryGet()!.Version, Is.EqualTo(9));
    }

    [Test]
    public void IndexCache_Swap_LowerVersionIgnored()
    {
        var svc = new PatientIndexSnapshotService();
        svc.Swap(IndexSnapshot(9));

        svc.Swap(IndexSnapshot(4)); // a stale pull arriving late

        Assert.That(svc.TryGet()!.Version, Is.EqualTo(9),
            "A lower-versioned snapshot must not regress the cache.");
    }
}
