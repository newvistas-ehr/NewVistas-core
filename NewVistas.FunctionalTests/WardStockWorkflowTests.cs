// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NUnit.Framework;
using Orleans.Hosting;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class WardStockWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private string UniqueWard() => $"WARD-{Guid.NewGuid():N}";

    // ─── Item Grain ──────────────────────────────────────────────────────────

    [Test]
    public async Task ItemGrain_CanSetup()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-001");
        await grain.SetupItemAsync("DRUG-001", "ACETAMINOPHEN 500MG", ward, "Medicine 3A", 100, 20, 80, "TAB", false);

        WardStockItemState state = await grain.GetItemAsync();
        Assert.That(state.DrugName, Is.EqualTo("ACETAMINOPHEN 500MG"));
        Assert.That(state.QuantityOnHand, Is.EqualTo(100)); // Starts at par
        Assert.That(state.ParLevel, Is.EqualTo(100));
    }

    [Test]
    public async Task ItemGrain_CanDispense()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-D");
        await grain.SetupItemAsync("DRUG-D", "TEST DRUG", ward, "Ward", 100, 20, 80, "TAB", false);

        bool ok = await grain.DispenseAsync(10);
        Assert.That(ok, Is.True);
        Assert.That((await grain.GetItemAsync()).QuantityOnHand, Is.EqualTo(90));
    }

    [Test]
    public async Task ItemGrain_DispenseFailsWhenInsufficient()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-INS");
        await grain.SetupItemAsync("DRUG-INS", "LOW DRUG", ward, "Ward", 5, 2, 10, "TAB", false);

        bool ok = await grain.DispenseAsync(10);
        Assert.That(ok, Is.False);
        Assert.That((await grain.GetItemAsync()).QuantityOnHand, Is.EqualTo(5));
    }

    [Test]
    public async Task ItemGrain_CanReceive()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-R");
        await grain.SetupItemAsync("DRUG-R", "RECV DRUG", ward, "Ward", 50, 10, 40, "TAB", false);
        await grain.ReceiveAsync(25);

        WardStockItemState state = await grain.GetItemAsync();
        Assert.That(state.QuantityOnHand, Is.EqualTo(75));
        Assert.That(state.LastReplenishmentDate, Is.Not.Null);
    }

    [Test]
    public async Task ItemGrain_AutoReplenishTriggersWhenBelowReorderPoint()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-AR");
        // Par=30, reorder at 10, qty starts at 30
        await grain.SetupItemAsync("DRUG-AR", "AUTO REPL DRUG", ward, "Ward", 30, 10, 20, "TAB", false);

        // Dispense 25 → qty = 5, which is below reorder point (10)
        await grain.DispenseAsync(25);

        WardStockItemState state = await grain.GetItemAsync();
        Assert.That(state.QuantityOnHand, Is.EqualTo(5));
        Assert.That(state.ReplenishmentPending, Is.True);

        // Check replenishment log has the request
        var log = _cluster.GrainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{ward}");
        List<ReplenishmentRequest> pending = await log.GetPendingRequestsAsync();
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].DrugName, Is.EqualTo("AUTO REPL DRUG"));
        Assert.That(pending[0].QuantityRequested, Is.EqualTo(20));
    }

    [Test]
    public async Task ItemGrain_NoDoubleReplenishment()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-ND");
        await grain.SetupItemAsync("DRUG-ND", "NO DBL", ward, "Ward", 30, 10, 20, "TAB", false);

        // First dispense triggers replenishment
        await grain.DispenseAsync(25);
        // Second dispense should NOT trigger another (already pending)
        await grain.DispenseAsync(2);

        var log = _cluster.GrainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{ward}");
        List<ReplenishmentRequest> all = await log.GetRequestsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ItemGrain_ReceiveClearsPending()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-RC");
        await grain.SetupItemAsync("DRUG-RC", "CLR PENDING", ward, "Ward", 30, 10, 20, "TAB", false);
        await grain.DispenseAsync(25); // triggers pending
        await grain.ReceiveAsync(20);

        Assert.That((await grain.GetItemAsync()).ReplenishmentPending, Is.False);
    }

    [Test]
    public async Task ItemGrain_DisabledAutoReplenishDoesNotTrigger()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-DIS");
        await grain.SetupItemAsync("DRUG-DIS", "DISABLED", ward, "Ward", 30, 10, 20, "TAB", false);
        await grain.SetAutoReplenishAsync(false);
        await grain.DispenseAsync(25);

        Assert.That((await grain.GetItemAsync()).ReplenishmentPending, Is.False);
    }

    [Test]
    public async Task ItemGrain_CanRecordInventoryCount()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-IC");
        await grain.SetupItemAsync("DRUG-IC", "COUNT DRUG", ward, "Ward", 100, 20, 80, "TAB", false);
        await grain.RecordInventoryCountAsync(85);

        WardStockItemState state = await grain.GetItemAsync();
        Assert.That(state.QuantityOnHand, Is.EqualTo(85));
        Assert.That(state.LastInventoryDate, Is.Not.Null);
    }

    [Test]
    public async Task ItemGrain_CanSetParLevel()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-PAR");
        await grain.SetupItemAsync("DRUG-PAR", "PAR DRUG", ward, "Ward", 50, 10, 40, "TAB", false);
        await grain.SetParLevelAsync(100, 25, 75);

        WardStockItemState state = await grain.GetItemAsync();
        Assert.That(state.ParLevel, Is.EqualTo(100));
        Assert.That(state.ReorderPoint, Is.EqualTo(25));
        Assert.That(state.ReorderQuantity, Is.EqualTo(75));
    }

    // ─── Index Grain ─────────────────────────────────────────────────────────

    [Test]
    public async Task Index_CanTrackItems()
    {
        var index = _cluster.GrainFactory.GetGrain<IWardStockIndexGrain>($"WARD-STOCK-INDEX:{UniqueWard()}");
        await index.AddOrUpdateAsync(new WardStockSummaryEntry { DrugId = "D1", DrugName = "Drug 1", QuantityOnHand = 100, ParLevel = 100, ReorderPoint = 20, NeedsReplenishment = false });
        await index.AddOrUpdateAsync(new WardStockSummaryEntry { DrugId = "D2", DrugName = "Drug 2", QuantityOnHand = 5, ParLevel = 50, ReorderPoint = 10, NeedsReplenishment = true });

        Assert.That(await index.GetAllItemsAsync(), Has.Count.EqualTo(2));
        Assert.That(await index.GetLowStockItemsAsync(), Has.Count.EqualTo(1));
    }

    // ─── Replenishment Log ───────────────────────────────────────────────────

    [Test]
    public async Task ReplenishLog_CanFillRequest()
    {
        var log = _cluster.GrainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{UniqueWard()}");
        await log.AddRequestAsync(new ReplenishmentRequest { RequestId = "R1", DrugId = "D1", DrugName = "Drug", QuantityRequested = 50, Status = "PENDING", RequestDate = DateTime.UtcNow });
        await log.FillRequestAsync("R1");

        List<ReplenishmentRequest> all = await log.GetRequestsAsync();
        Assert.That(all[0].Status, Is.EqualTo("FILLED"));
        Assert.That(all[0].FilledDate, Is.Not.Null);
    }

    // ─── Full Workflow ───────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_DispenseTriggersReplenishThenReceive()
    {
        string ward = UniqueWard();
        var grain = _cluster.GrainFactory.GetGrain<IWardStockItemGrain>($"WARD-STOCK:{ward}:DRUG-FW");
        await grain.SetupItemAsync("DRUG-FW", "FULL WORKFLOW", ward, "Ward", 50, 15, 35, "TAB", false);

        // Dispense down below reorder point
        await grain.DispenseAsync(40); // qty = 10 (below 15)

        // Verify auto-replenishment triggered
        WardStockItemState lowState = await grain.GetItemAsync();
        Assert.That(lowState.ReplenishmentPending, Is.True);
        Assert.That(lowState.QuantityOnHand, Is.EqualTo(10));

        // Pharmacy fills the request
        var log = _cluster.GrainFactory.GetGrain<IWardReplenishmentLogGrain>($"WARD-REPLENISH-LOG:{ward}");
        List<ReplenishmentRequest> pending = await log.GetPendingRequestsAsync();
        await log.FillRequestAsync(pending[0].RequestId);

        // Receive stock
        await grain.ReceiveAsync(35);
        WardStockItemState filled = await grain.GetItemAsync();
        Assert.That(filled.QuantityOnHand, Is.EqualTo(45));
        Assert.That(filled.ReplenishmentPending, Is.False);
    }
}
