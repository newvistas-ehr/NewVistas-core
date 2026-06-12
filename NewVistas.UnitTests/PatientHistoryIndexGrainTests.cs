// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the generic per-patient, per-domain full-history index that
/// holds the IDs trimmed out of PatientState's capped recent window. The
/// paging order (dated entries newest-first, then undated migrated entries in
/// reverse insertion order) and idempotent upsert are the load-bearing details.
/// </summary>
[TestFixture]
public class PatientHistoryIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientHistoryIndexGrain History() =>
        _cluster.GrainFactory.GetGrain<IPatientHistoryIndexGrain>(
            $"PATIENT-{Guid.NewGuid()}:{PatientHistoryDomains.Consult}");

    private static HistoryRef Ref(string id, DateTime? date = null) =>
        new() { ItemId = id, Date = date };

    [Test]
    public async Task AddEntry_GetAllIds_PreservesInsertionOrder()
    {
        IPatientHistoryIndexGrain history = History();
        await history.AddEntryAsync(Ref("A"));
        await history.AddEntryAsync(Ref("B"));
        await history.AddEntryAsync(Ref("C"));

        Assert.That(await history.GetAllIdsAsync(), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(await history.GetCountAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task AddEntry_DuplicateId_IsIdempotent()
    {
        IPatientHistoryIndexGrain history = History();
        await history.AddEntryAsync(Ref("A"));
        await history.AddEntryAsync(Ref("A"));
        await history.AddEntryAsync(Ref("A"));

        Assert.That(await history.GetCountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task AddEntry_ReAddWithDate_RefreshesNullDate()
    {
        IPatientHistoryIndexGrain history = History();
        DateTime when = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Migrated first (no date), then a live dated write for the same item.
        await history.AddEntryAsync(Ref("A", date: null));
        await history.AddEntryAsync(Ref("B", date: null));
        await history.AddEntryAsync(Ref("A", date: when));

        Assert.That(await history.GetCountAsync(), Is.EqualTo(2));

        // A now has a date, so it sorts ahead of the still-undated B.
        Assert.That(await history.GetPageAsync(0, 10), Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task AddRange_DedupsById()
    {
        IPatientHistoryIndexGrain history = History();

        await history.AddRangeAsync([Ref("A"), Ref("B"), Ref("C")]);
        await history.AddRangeAsync([Ref("B"), Ref("C"), Ref("D")]); // overlap

        Assert.That(await history.GetCountAsync(), Is.EqualTo(4));
        Assert.That(await history.GetAllIdsAsync(), Is.EqualTo(new[] { "A", "B", "C", "D" }));
    }

    [Test]
    public async Task GetPage_DatedNewestFirstThenUndatedReverseInsertion()
    {
        IPatientHistoryIndexGrain history = History();
        DateTime day1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime day5 = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        // Insertion order interleaves dated and undated entries.
        await history.AddEntryAsync(Ref("U1", date: null));
        await history.AddEntryAsync(Ref("D_OLD", date: day1));
        await history.AddEntryAsync(Ref("U2", date: null));
        await history.AddEntryAsync(Ref("D_NEW", date: day5));

        List<string> page = await history.GetPageAsync(0, 10);

        // Dated entries first (newest date first), then undated entries newest-
        // inserted first.
        Assert.That(page, Is.EqualTo(new[] { "D_NEW", "D_OLD", "U2", "U1" }));
    }

    [Test]
    public async Task GetPage_RespectsOffsetAndLimit()
    {
        IPatientHistoryIndexGrain history = History();
        DateTime baseDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 10; i++)
            await history.AddEntryAsync(Ref($"R{i}", date: baseDate.AddDays(i)));

        // Newest first: R9, R8, ... ; page (offset 2, limit 3) -> R7, R6, R5.
        Assert.That(await history.GetPageAsync(2, 3), Is.EqualTo(new[] { "R7", "R6", "R5" }));
    }

    [Test]
    public async Task RemoveEntry_RemovesById()
    {
        IPatientHistoryIndexGrain history = History();
        await history.AddRangeAsync([Ref("A"), Ref("B"), Ref("C")]);

        await history.RemoveEntryAsync("B");

        Assert.That(await history.GetAllIdsAsync(), Is.EqualTo(new[] { "A", "C" }));
    }

    [Test]
    public async Task RemoveEntry_Missing_IsNoOp()
    {
        IPatientHistoryIndexGrain history = History();
        await history.AddEntryAsync(Ref("A"));

        await history.RemoveEntryAsync("NONEXISTENT");

        Assert.That(await history.GetCountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task EmptyItemId_IsIgnored()
    {
        IPatientHistoryIndexGrain history = History();

        await history.AddEntryAsync(Ref(string.Empty));

        Assert.That(await history.GetCountAsync(), Is.EqualTo(0));
    }
}
