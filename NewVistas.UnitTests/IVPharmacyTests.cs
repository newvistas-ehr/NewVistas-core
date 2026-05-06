// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// IVAdmixOrderGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class IVAdmixOrderGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IIVAdmixOrderGrain NewOrder() =>
        _cluster.GrainFactory.GetGrain<IIVAdmixOrderGrain>($"IVAD-ORDER:{Guid.NewGuid()}");

    private static async Task CreateBasicOrder(IIVAdmixOrderGrain grain, string patientId = "PAT-001")
    {
        await grain.CreateOrderAsync(
            patientId,
            baseSolution: "Normal Saline",
            baseSolutionVolumeMl: 250,
            route: IVAdmixRoute.Peripheral,
            frequency: IVAdmixFrequency.Q8H,
            containerType: IVContainerType.Bag,
            containerCount: 3,
            priority: IVAdmixPriority.Routine,
            linkedInpatientOrderId: null,
            infusionRateStr: "125 mL/hr",
            infusionRateMlHr: 125m,
            infusionDurationHours: 2m,
            routeDescription: null,
            frequencyDescription: null,
            startDateTime: DateTime.UtcNow,
            stopDateTime: DateTime.UtcNow.AddDays(3),
            providerId: "PROV-001",
            providerName: "Dr. Provider",
            notes: null);
    }

    // ── Create / Basic ─────────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanCreateOrder()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        IVAdmixOrderState state = await grain.GetOrderAsync();

        Assert.That(state.BaseSolution, Is.EqualTo("Normal Saline"));
        Assert.That(state.BaseSolutionVolumeMl, Is.EqualTo(250));
        Assert.That(state.Route, Is.EqualTo(IVAdmixRoute.Peripheral));
        Assert.That(state.Frequency, Is.EqualTo(IVAdmixFrequency.Q8H));
        Assert.That(state.ContainerType, Is.EqualTo(IVContainerType.Bag));
        Assert.That(state.ContainerCount, Is.EqualTo(3));
        Assert.That(state.Priority, Is.EqualTo(IVAdmixPriority.Routine));
        Assert.That(state.InfusionRateStr, Is.EqualTo("125 mL/hr"));
        Assert.That(state.InfusionRateMlHr, Is.EqualTo(125m));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Provider"));
    }

    [Test]
    public async Task IVAdmixOrderGrain_OrderIdMatchesGrainKey()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        IVAdmixOrderState state = await grain.GetOrderAsync();

        Assert.That(state.OrderId, Is.EqualTo(grain.GetPrimaryKeyString()));
    }

    [Test]
    public async Task IVAdmixOrderGrain_DefaultStatusIsPending()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        IVAdmixOrderState state = await grain.GetOrderAsync();

        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Pending));
    }

    [Test]
    public async Task IVAdmixOrderGrain_TotalVolumeEqualsBaseSolutionOnCreate()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        IVAdmixOrderState state = await grain.GetOrderAsync();

        Assert.That(state.TotalVolumeMl, Is.EqualTo(250));
    }

    // ── Additives ──────────────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanAddAdditive()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        var additive = new IVAdmixAdditive
        {
            DrugName = "Potassium Chloride",
            DrugId = "KCL-001",
            Dose = "20",
            DoseUnit = "mEq",
            IsBaseSolution = false
        };
        await grain.AddAdditiveAsync(additive);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Additives, Has.Count.EqualTo(1));
        Assert.That(state.Additives[0].DrugName, Is.EqualTo("Potassium Chloride"));
        Assert.That(state.Additives[0].Dose, Is.EqualTo("20"));
        Assert.That(state.Additives[0].DoseUnit, Is.EqualTo("mEq"));
    }

    [Test]
    public async Task IVAdmixOrderGrain_CanAddMultipleAdditives()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        await grain.AddAdditiveAsync(new IVAdmixAdditive { DrugName = "KCl", Dose = "20", DoseUnit = "mEq" });
        await grain.AddAdditiveAsync(new IVAdmixAdditive { DrugName = "MgSO4", Dose = "2", DoseUnit = "g" });

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Additives, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task IVAdmixOrderGrain_CanRemoveAdditive()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        await grain.AddAdditiveAsync(new IVAdmixAdditive { DrugName = "KCl", Dose = "20", DoseUnit = "mEq" });
        await grain.AddAdditiveAsync(new IVAdmixAdditive { DrugName = "MgSO4", Dose = "2", DoseUnit = "g" });
        await grain.RemoveAdditiveAsync("KCl");

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Additives, Has.Count.EqualTo(1));
        Assert.That(state.Additives[0].DrugName, Is.EqualTo("MgSO4"));
    }

    // ── Lifecycle: Verify ──────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanVerifyOrder()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        DateTime verifiedDate = DateTime.UtcNow;
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", verifiedDate);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Verified));
        Assert.That(state.PharmacistId, Is.EqualTo("PHARM-001"));
        Assert.That(state.PharmacistName, Is.EqualTo("Jane Pharmacist"));
        Assert.That(state.VerifiedDate, Is.EqualTo(verifiedDate));
    }

    // ── Lifecycle: Compound ────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanStartCompounding()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", DateTime.UtcNow);

        DateTime startDate = DateTime.UtcNow;
        await grain.StartCompoundingAsync("TECH-001", "Bob Technician", startDate);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Compounding));
        Assert.That(state.CompoundedById, Is.EqualTo("TECH-001"));
        Assert.That(state.CompoundedByName, Is.EqualTo("Bob Technician"));
        Assert.That(state.CompoundingStartDate, Is.EqualTo(startDate));
    }

    [Test]
    public async Task IVAdmixOrderGrain_CanCompleteCompounding()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", DateTime.UtcNow);
        await grain.StartCompoundingAsync("TECH-001", "Bob Technician", DateTime.UtcNow);

        DateTime completedDate = DateTime.UtcNow;
        DateTime expiry = completedDate.AddDays(1);
        await grain.CompleteCompoundingAsync(completedDate, "LOT-2025-001", expiry);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Ready));
        Assert.That(state.LotNumber, Is.EqualTo("LOT-2025-001"));
        Assert.That(state.ExpirationDate, Is.EqualTo(expiry));
        Assert.That(state.CompoundingCompleteDate, Is.EqualTo(completedDate));
    }

    // ── Lifecycle: Label ───────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanPrintLabel()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        DateTime printDate = DateTime.UtcNow;
        await grain.PrintLabelAsync("TECH-001", printDate);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.LabelPrinted, Is.True);
        Assert.That(state.LabelPrintedBy, Is.EqualTo("TECH-001"));
        Assert.That(state.LabelPrintedDate, Is.EqualTo(printDate));
    }

    // ── Lifecycle: Dispense / Administer ───────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanDispenseOrder()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", DateTime.UtcNow);
        await grain.StartCompoundingAsync("TECH-001", "Bob Technician", DateTime.UtcNow);
        await grain.CompleteCompoundingAsync(DateTime.UtcNow, null, null);

        DateTime dispensed = DateTime.UtcNow;
        await grain.DispenseOrderAsync(dispensed);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Dispensed));
        Assert.That(state.DispensingDateTime, Is.EqualTo(dispensed));
    }

    [Test]
    public async Task IVAdmixOrderGrain_CanRecordAdministration()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", DateTime.UtcNow);
        await grain.StartCompoundingAsync("TECH-001", "Bob Technician", DateTime.UtcNow);
        await grain.CompleteCompoundingAsync(DateTime.UtcNow, null, null);
        await grain.DispenseOrderAsync(DateTime.UtcNow);

        DateTime admin = DateTime.UtcNow;
        await grain.RecordAdministrationAsync(admin);

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Administered));
        Assert.That(state.AdministrationDateTime, Is.EqualTo(admin));
    }

    // ── Lifecycle: Discontinue / Cancel ────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_CanDiscontinueOrder()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", DateTime.UtcNow);

        await grain.DiscontinueOrderAsync("Physician order changed");

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Discontinued));
        Assert.That(state.DiscontinuationReason, Is.EqualTo("Physician order changed"));
    }

    [Test]
    public async Task IVAdmixOrderGrain_CanCancelOrder()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);

        await grain.CancelOrderAsync("Ordered in error");

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Ordered in error"));
    }

    // ── Stat Order ─────────────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_StatPriority()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await grain.CreateOrderAsync(
            "PAT-002", "D5W", 100, IVAdmixRoute.Central, IVAdmixFrequency.Continuous,
            IVContainerType.Bag, 1, IVAdmixPriority.STAT,
            null, "50 mL/hr", 50m, null, null, null, null, null,
            null, "Dr. Stat", "STAT: electrolyte replacement");

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Priority, Is.EqualTo(IVAdmixPriority.STAT));
    }

    // ── LastModifiedDate ───────────────────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_LastModifiedDateUpdatesOnVerify()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await CreateBasicOrder(grain);
        IVAdmixOrderState before = await grain.GetOrderAsync();

        await Task.Delay(5);
        await grain.VerifyOrderAsync("PHARM-001", "Jane Pharmacist", DateTime.UtcNow);

        IVAdmixOrderState after = await grain.GetOrderAsync();
        Assert.That(after.LastModifiedDate, Is.GreaterThanOrEqualTo(before.LastModifiedDate));
    }

    // ── PICC Central with Vancomycin ───────────────────────────────────────

    [Test]
    public async Task IVAdmixOrderGrain_PiccRouteWithVancomycin()
    {
        IIVAdmixOrderGrain grain = NewOrder();
        await grain.CreateOrderAsync(
            "PAT-003", "Normal Saline", 250, IVAdmixRoute.PICC,
            IVAdmixFrequency.Q12H, IVContainerType.Bag, 2, IVAdmixPriority.ASAP,
            null, "Over 60 min", null, 1m, null, null,
            DateTime.UtcNow, null, "PROV-002", "Dr. Infect", "Vancomycin IV");

        await grain.AddAdditiveAsync(new IVAdmixAdditive
        {
            DrugName = "Vancomycin",
            DrugId = "VANC-001",
            Dose = "1500",
            DoseUnit = "mg",
            IsBaseSolution = false
        });

        IVAdmixOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Route, Is.EqualTo(IVAdmixRoute.PICC));
        Assert.That(state.Priority, Is.EqualTo(IVAdmixPriority.ASAP));
        Assert.That(state.Additives, Has.Count.EqualTo(1));
        Assert.That(state.Additives[0].DrugName, Is.EqualTo("Vancomycin"));
        Assert.That(state.Additives[0].DoseUnit, Is.EqualTo("mg"));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// IVAdmixOrderIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class IVAdmixOrderIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IIVAdmixOrderIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IIVAdmixOrderIndexGrain>($"IVAD-ORDER-IDX:{Guid.NewGuid()}");

    private static IVAdmixOrderIndexEntry MakeEntry(
        string orderId,
        IVAdmixOrderStatus status = IVAdmixOrderStatus.Pending,
        IVAdmixPriority priority = IVAdmixPriority.Routine,
        DateTime? createdDate = null) =>
        new()
        {
            OrderId       = orderId,
            Status        = status,
            Priority      = priority,
            BaseSolution  = "Normal Saline",
            TotalVolumeMl = 250,
            Route         = IVAdmixRoute.Peripheral,
            Frequency     = IVAdmixFrequency.Q8H,
            CreatedDate   = createdDate ?? DateTime.UtcNow,
        };

    [Test]
    public async Task IVAdmixOrderIndexGrain_EmptyOnStart()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();
        List<IVAdmixOrderIndexEntry> all = await index.GetAllOrdersAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_CanUpsertAndRetrieve()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();
        string orderId = $"IVAD-ORDER:{Guid.NewGuid()}";

        await index.UpsertOrderAsync(MakeEntry(orderId, IVAdmixOrderStatus.Pending));

        List<IVAdmixOrderIndexEntry> all = await index.GetAllOrdersAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].OrderId, Is.EqualTo(orderId));
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_UpsertUpdatesExistingEntry()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();
        string orderId = $"IVAD-ORDER:{Guid.NewGuid()}";

        await index.UpsertOrderAsync(MakeEntry(orderId, IVAdmixOrderStatus.Pending));
        await index.UpsertOrderAsync(MakeEntry(orderId, IVAdmixOrderStatus.Verified));

        List<IVAdmixOrderIndexEntry> all = await index.GetAllOrdersAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(IVAdmixOrderStatus.Verified));
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_GetPendingFiltersCorrectly()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();

        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Pending));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Verified));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Compounding));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Dispensed));

        List<IVAdmixOrderIndexEntry> pending = await index.GetPendingOrdersAsync();
        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(pending.All(o => o.Status == IVAdmixOrderStatus.Pending || o.Status == IVAdmixOrderStatus.Verified), Is.True);
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_GetActiveFiltersCompoundingAndReady()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();

        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Pending));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Compounding));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Ready));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Dispensed));

        List<IVAdmixOrderIndexEntry> active = await index.GetActiveOrdersAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(o => o.Status == IVAdmixOrderStatus.Compounding || o.Status == IVAdmixOrderStatus.Ready), Is.True);
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_GetByStatusFiltersExactly()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();

        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Administered));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Administered));
        await index.UpsertOrderAsync(MakeEntry($"IVAD-ORDER:{Guid.NewGuid()}", IVAdmixOrderStatus.Cancelled));

        List<IVAdmixOrderIndexEntry> administered = await index.GetOrdersByStatusAsync(IVAdmixOrderStatus.Administered);
        Assert.That(administered, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_OrderedByCreatedDateDescending()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();
        DateTime t1 = DateTime.UtcNow.AddHours(-3);
        DateTime t2 = DateTime.UtcNow.AddHours(-1);
        DateTime t3 = DateTime.UtcNow;

        string id1 = $"IVAD-ORDER:{Guid.NewGuid()}";
        string id2 = $"IVAD-ORDER:{Guid.NewGuid()}";
        string id3 = $"IVAD-ORDER:{Guid.NewGuid()}";

        await index.UpsertOrderAsync(MakeEntry(id1, createdDate: t1));
        await index.UpsertOrderAsync(MakeEntry(id2, createdDate: t3));
        await index.UpsertOrderAsync(MakeEntry(id3, createdDate: t2));

        List<IVAdmixOrderIndexEntry> all = await index.GetAllOrdersAsync();
        Assert.That(all[0].OrderId, Is.EqualTo(id2)); // newest first
        Assert.That(all[1].OrderId, Is.EqualTo(id3));
        Assert.That(all[2].OrderId, Is.EqualTo(id1));
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_CanRemoveOrder()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();
        string orderId = $"IVAD-ORDER:{Guid.NewGuid()}";

        await index.UpsertOrderAsync(MakeEntry(orderId));
        await index.RemoveOrderAsync(orderId);

        List<IVAdmixOrderIndexEntry> all = await index.GetAllOrdersAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IVAdmixOrderIndexGrain_RemoveIdempotent()
    {
        IIVAdmixOrderIndexGrain index = NewIndex();
        string orderId = $"IVAD-ORDER:{Guid.NewGuid()}";

        await index.UpsertOrderAsync(MakeEntry(orderId));
        await index.RemoveOrderAsync(orderId);
        await index.RemoveOrderAsync(orderId); // second remove should not throw

        List<IVAdmixOrderIndexEntry> all = await index.GetAllOrdersAsync();
        Assert.That(all, Is.Empty);
    }
}
