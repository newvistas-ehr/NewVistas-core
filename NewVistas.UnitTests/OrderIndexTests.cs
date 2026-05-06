// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for PatientOrderIndexGrain — the per-patient order index
/// that eliminates N+1 fan-out for filtered order queries.
/// </summary>
[TestFixture]
public class PatientOrderIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientOrderIndexGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IPatientOrderIndexGrain>($"PATIENT-{Guid.NewGuid()}");

    [Test]
    public async Task OrderIndex_AddEntry_AppearsInAllEntries()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
        {
            OrderGrainKey = "ORDER-001",
            StartDate = now,
            OrderType = "Lab",
            Status = "Pending",
            OrderText = "CBC",
            ProviderName = "Dr. Smith",
            IsSigned = false
        });

        List<OrderIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].OrderGrainKey, Is.EqualTo("ORDER-001"));
        Assert.That(entries[0].OrderType, Is.EqualTo("Lab"));
    }

    [Test]
    public async Task OrderIndex_MultipleEntries_SortedByDateDescending()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime oldest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime middle = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime newest = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-OLDEST", StartDate = oldest, OrderType = "Lab", Status = "Completed", OrderText = "BMP" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-NEWEST", StartDate = newest, OrderType = "Pharmacy", Status = "Active", OrderText = "Metformin" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-MIDDLE", StartDate = middle, OrderType = "Consult", Status = "Pending", OrderText = "Cardiology" });

        List<OrderIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries[0].OrderGrainKey, Is.EqualTo("ORDER-NEWEST"));
        Assert.That(entries[1].OrderGrainKey, Is.EqualTo("ORDER-MIDDLE"));
        Assert.That(entries[2].OrderGrainKey, Is.EqualTo("ORDER-OLDEST"));
    }

    [Test]
    public async Task OrderIndex_UpdateExistingEntry_ReplacesOldEntry()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-UPD", StartDate = now, OrderType = "Lab", Status = "Pending", OrderText = "CBC" });

        // Update same order — status changed
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-UPD", StartDate = now, OrderType = "Lab", Status = "Active", OrderText = "CBC" });

        List<OrderIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Status, Is.EqualTo("Active"));
    }

    [Test]
    public async Task OrderIndex_RemoveEntry_RemovesFromIndex()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-KEEP", StartDate = now, OrderType = "Lab", Status = "Active", OrderText = "CBC" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "ORDER-REMOVE", StartDate = now.AddMinutes(-5), OrderType = "Pharmacy", Status = "Active", OrderText = "Aspirin" });

        await grain.RemoveOrderAsync("ORDER-REMOVE");

        List<OrderIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].OrderGrainKey, Is.EqualTo("ORDER-KEEP"));
    }

    [Test]
    public async Task OrderIndex_FilterByCurrent_ReturnsOnlyActiveAndPendingAndHold()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-ACTIVE", StartDate = now, Status = "Active", OrderText = "A" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-PENDING", StartDate = now, Status = "Pending", OrderText = "B" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-HOLD", StartDate = now, Status = "Hold", OrderText = "C" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-DC", StartDate = now, Status = "Discontinued", OrderText = "D" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-COMP", StartDate = now, Status = "Completed", OrderText = "E" });

        List<OrderIndexEntry> current = await grain.GetEntriesByFilterAsync(2); // Current
        Assert.That(current, Has.Count.EqualTo(3));
        Assert.That(current.All(e => e.Status is "Active" or "Pending" or "Hold"), Is.True);
    }

    [Test]
    public async Task OrderIndex_FilterByDiscontinued_ReturnsOnlyDiscontinued()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-ACTIVE2", StartDate = now, Status = "Active", OrderText = "A" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-DC2", StartDate = now, Status = "Discontinued", OrderText = "B" });

        List<OrderIndexEntry> dc = await grain.GetEntriesByFilterAsync(3); // Discontinued
        Assert.That(dc, Has.Count.EqualTo(1));
        Assert.That(dc[0].OrderGrainKey, Is.EqualTo("O-DC2"));
    }

    [Test]
    public async Task OrderIndex_FilterByUnsigned_ReturnsOnlyUnsigned()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-SIGNED", StartDate = now, Status = "Active", OrderText = "A", IsSigned = true });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-UNSIGNED", StartDate = now, Status = "Active", OrderText = "B", IsSigned = false });

        List<OrderIndexEntry> unsigned = await grain.GetEntriesByFilterAsync(11); // Unsigned
        Assert.That(unsigned, Has.Count.EqualTo(1));
        Assert.That(unsigned[0].OrderGrainKey, Is.EqualTo("O-UNSIGNED"));
    }

    [Test]
    public async Task OrderIndex_DateRange_FiltersCorrectly()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime jan = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime feb = new DateTime(2026, 2, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime mar = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-JAN", StartDate = jan, Status = "Completed", OrderText = "Jan order" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-FEB", StartDate = feb, Status = "Active", OrderText = "Feb order" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-MAR", StartDate = mar, Status = "Active", OrderText = "Mar order" });

        List<OrderIndexEntry> range = await grain.GetEntriesByDateRangeAsync(
            new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
        Assert.That(range, Has.Count.EqualTo(1));
        Assert.That(range[0].OrderGrainKey, Is.EqualTo("O-FEB"));
    }

    [Test]
    public async Task OrderIndex_Count_ReturnsCorrectCount()
    {
        IPatientOrderIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-C1", StartDate = now, Status = "Active", OrderText = "A" });
        await grain.AddOrUpdateOrderAsync(new OrderIndexEntry
            { OrderGrainKey = "O-C2", StartDate = now, Status = "Pending", OrderText = "B" });

        int count = await grain.GetCountAsync();
        Assert.That(count, Is.EqualTo(2));
    }
}
