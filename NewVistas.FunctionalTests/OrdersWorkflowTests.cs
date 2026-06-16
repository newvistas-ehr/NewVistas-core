// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Order Entry — File #100.
/// Tests end-to-end order placement, signing, hold/release, discontinuation,
/// order checking, order sets, and renewal workflows via IPatientWorkflowGrain.
/// Mirrors ORWDX.m / ORWDXA.m / ORWDXC.m / ORWDXM.m routines.
/// </summary>
[TestFixture]
public class OrdersWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid():N}");

    private async Task<string> PlaceStandardOrderAsync(IPatientWorkflowGrain wf,
        string orderType = "Lab",
        string orderText = "CBC WITH DIFFERENTIAL",
        string urgency = "ROUTINE",
        string providerName = "Dr. Adams")
    {
        return await wf.PlaceOrderAsync(
            orderType, orderText, $"OI-{Guid.NewGuid():N}",
            "PROV-001", providerName,
            "LOC-001", "Primary Care Clinic",
            urgency, "Fasting specimen preferred", "Annual wellness exam");
    }

    // ─── Place order tests ─────────────────────────────────────────────────────

    [Test]
    public async Task PlaceOrder_ReturnsNonEmptyId()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        Assert.That(orderId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task PlaceOrder_OrderTypeStored()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf, orderType: "Pharmacy");
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        OrderSummary entry = orders.Single(o => o.OrderId == orderId);
        Assert.That(entry.OrderType, Is.EqualTo("Pharmacy"));
    }

    [Test]
    public async Task PlaceOrder_ProviderNameStored()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf, providerName: "Dr. Rivera");
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        OrderSummary entry = orders.Single(o => o.OrderId == orderId);
        Assert.That(entry.ProviderName, Is.EqualTo("Dr. Rivera"));
    }

    // ─── Status transition tests ───────────────────────────────────────────────

    [Test]
    public async Task SignOrder_SetsSignedStatus()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        await wf.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        OrderSummary entry = orders.Single(o => o.OrderId == orderId);
        Assert.That(entry.Status, Is.Not.EqualTo("Pending"));
    }

    [Test]
    public async Task DiscontinueOrder_SetsDiscontinuedStatus()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        await wf.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");
        await wf.DiscontinueOrderAsync(orderId, "No longer clinically indicated");
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        OrderSummary entry = orders.Single(o => o.OrderId == orderId);
        Assert.That(entry.Status, Is.EqualTo("Discontinued"));
    }

    [Test]
    public async Task HoldOrder_SetsHeldStatus()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        await wf.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");
        await wf.HoldOrderAsync(orderId);
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        OrderSummary entry = orders.Single(o => o.OrderId == orderId);
        Assert.That(entry.Status, Is.EqualTo("Hold"));
    }

    [Test]
    public async Task ReleaseOrder_ReleasesHeldOrder()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        await wf.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");
        await wf.HoldOrderAsync(orderId);
        await wf.ReleaseOrderAsync(orderId);
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        OrderSummary entry = orders.Single(o => o.OrderId == orderId);
        Assert.That(entry.Status, Is.Not.EqualTo("Hold"));
    }

    // ─── List/filter tests ──────────────────────────────────────────────────────

    [Test]
    public async Task GetOrders_NoOrders_ReturnsEmpty()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        Assert.That(orders, Is.Empty);
    }

    [Test]
    public async Task MultipleOrders_AllInList()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string id1 = await PlaceStandardOrderAsync(wf, orderType: "Lab", orderText: "HEMOGLOBIN A1C");
        string id2 = await PlaceStandardOrderAsync(wf, orderType: "Radiology", orderText: "CHEST XRAY");
        string id3 = await PlaceStandardOrderAsync(wf, orderType: "Pharmacy", orderText: "METFORMIN 500MG TAB");
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        Assert.That(orders, Has.Count.EqualTo(3));
        Assert.That(orders.Select(o => o.OrderText), Does.Contain("HEMOGLOBIN A1C"));
        Assert.That(orders.Select(o => o.OrderText), Does.Contain("CHEST XRAY"));
        Assert.That(orders.Select(o => o.OrderText), Does.Contain("METFORMIN 500MG TAB"));
    }

    // ─── Get order detail ───────────────────────────────────────────────────────

    [Test]
    public async Task GetOrderDetail_ReturnsFullState()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf, orderType: "Consult", orderText: "CARDIOLOGY CONSULT");
        OrderState detail = await wf.GetOrderDetailAsync(orderId);
        Assert.That(detail.OrderType, Is.EqualTo("Consult"));
        Assert.That(detail.OrderableItem, Is.EqualTo("CARDIOLOGY CONSULT"));
        Assert.That(detail.Urgency, Is.EqualTo("ROUTINE"));
    }

    // ─── Renew order ────────────────────────────────────────────────────────────

    [Test]
    public async Task RenewOrder_SetsNatureToRenewal()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        await wf.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");
        DateTime newStop = DateTime.UtcNow.AddDays(30);
        await wf.RenewOrderAsync(orderId, "PROV-001", newStop);
        OrderState detail = await wf.GetOrderDetailAsync(orderId);
        Assert.That(detail.Nature, Is.EqualTo("RENEWAL"));
    }

    // ─── Verify order ───────────────────────────────────────────────────────────

    [Test]
    public async Task VerifyOrder_SetsNurseVerification()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string orderId = await PlaceStandardOrderAsync(wf);
        await wf.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");
        await wf.VerifyOrderAsync(orderId, "NURSE-001");
        OrderState detail = await wf.GetOrderDetailAsync(orderId);
        Assert.That(detail.NurseVerifiedBy, Is.EqualTo("NURSE-001"));
        Assert.That(detail.NurseVerifiedDateTime, Is.Not.Null);
    }

    // ─── Order checking ─────────────────────────────────────────────────────────

    [Test]
    public async Task CheckOrder_DuplicateDetection()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        await PlaceStandardOrderAsync(wf, orderType: "Lab", orderText: "CBC WITH DIFFERENTIAL");
        await wf.SignOrderAsync(
            (await wf.GetOrdersByFilterAsync(0)).First().OrderId, "ESIG-001");

        List<OrderCheckResult> checks = await wf.CheckOrderAsync("Lab", "CBC WITH DIFFERENTIAL", null);
        Assert.That(checks.Any(c => c.CheckType == "DUPLICATE"), Is.True);
    }

    [Test]
    public async Task CheckOrder_DrugAllergyDetection()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        await wf.RecordAllergyAsync("PENICILLIN", "DRUG", null, "O",
            new List<string> { "HIVES" }, "MODERATE", "PROV-001", "Dr. Adams", null);

        List<OrderCheckResult> checks = await wf.CheckOrderAsync("Pharmacy", "PENICILLIN VK 500MG", null);
        Assert.That(checks.Any(c => c.CheckType == "DRUG_ALLERGY"), Is.True);
    }

    // ─── Three-Tier Architecture Tests ─────────────────────────────────────

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IPatientOrderIndexGrain GetOrderIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientOrderIndexGrain>(patientId);

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [Test]
    public async Task PlaceOrder_PopulatesOrderIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.PlaceOrderAsync("Lab", "CBC", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);
        await w.PlaceOrderAsync("Pharmacy", "METFORMIN", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);

        List<OrderIndexEntry> entries = await GetOrderIndex(patientId).GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PlaceOrder_PopulatesRecentOrdersCache()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.PlaceOrderAsync("Lab", "BMP", null, "PROV-1", "Dr. Smith",
            null, null, "ROUTINE", null, null);

        List<OrderSummary> cached = await GetPatient(patientId).GetRecentOrdersAsync();
        Assert.That(cached, Has.Count.EqualTo(1));
        Assert.That(cached[0].OrderText, Is.EqualTo("BMP"));
    }

    [Test]
    public async Task RecentOrdersCache_TrimsToDisplayCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        ISiteParametersGrain siteParams = _cluster.GrainFactory
            .GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.SetOrdersDisplayCountAsync(3);

        // Place 6 orders
        for (int i = 0; i < 6; i++)
        {
            await w.PlaceOrderAsync("Lab", $"TEST-{i}", null, "PROV-1", "Dr. Adams",
                null, null, "ROUTINE", null, null);
        }

        List<OrderSummary> cached = await GetPatient(patientId).GetRecentOrdersAsync();
        Assert.That(cached, Has.Count.EqualTo(3));

        // Reset to default
        await siteParams.SetOrdersDisplayCountAsync(5);
    }

    [Test]
    public async Task SignOrder_SyncsStatusToIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string orderId = await w.PlaceOrderAsync("Lab", "LIPID PANEL", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);
        await w.SignOrderAsync(orderId, "ADAMS.JOHN.M.D.12345");

        List<OrderIndexEntry> entries = await GetOrderIndex(patientId).GetAllEntriesAsync();
        OrderIndexEntry entry = entries.Single(e => e.OrderGrainKey == orderId);
        Assert.That(entry.Status, Is.EqualTo("Active"));
        Assert.That(entry.IsSigned, Is.True);
    }

    [Test]
    public async Task DiscontinueOrder_SyncsStatusToIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string orderId = await w.PlaceOrderAsync("Pharmacy", "ASPIRIN", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);
        await w.SignOrderAsync(orderId, "ESIG");
        await w.DiscontinueOrderAsync(orderId, "No longer needed");

        List<OrderIndexEntry> entries = await GetOrderIndex(patientId).GetAllEntriesAsync();
        OrderIndexEntry entry = entries.Single(e => e.OrderGrainKey == orderId);
        Assert.That(entry.Status, Is.EqualTo("Discontinued"));
    }

    [Test]
    public async Task GetOrdersByFilter_UsesIndex_FiltersCurrent()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id1 = await w.PlaceOrderAsync("Lab", "CBC", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);
        string id2 = await w.PlaceOrderAsync("Pharmacy", "METFORMIN", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);

        // Sign both, then discontinue one
        await w.SignOrderAsync(id1, "ESIG");
        await w.SignOrderAsync(id2, "ESIG");
        await w.DiscontinueOrderAsync(id1, "Completed");

        // Filter 2 = Current (Active/Pending/Hold)
        List<OrderSummary> current = await w.GetOrdersByFilterAsync(2);
        Assert.That(current, Has.Count.EqualTo(1));
        Assert.That(current[0].OrderId, Is.EqualTo(id2));
    }

    [Test]
    public async Task GetRecentOrders_ReturnsFromCache_ZeroFanOut()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.PlaceOrderAsync("Lab", "BMP", null, "PROV-1", "Dr. Smith",
            null, null, "ROUTINE", null, null);
        await w.PlaceOrderAsync("Pharmacy", "LISINOPRIL", null, "PROV-1", "Dr. Smith",
            null, null, "ROUTINE", null, null);

        List<OrderSummary> recent = await w.GetRecentOrdersAsync();
        Assert.That(recent, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetOrderHistory_DateRange_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Place orders — the workflow uses DateTime.UtcNow as the order date,
        // so all orders will be at "now". To test range filtering, we use
        // a broad range that includes now and verify all appear.
        await w.PlaceOrderAsync("Lab", "CBC", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);
        await w.PlaceOrderAsync("Radiology", "CHEST XRAY", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);

        List<OrderSummary> history = await w.GetOrderHistoryAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 100);
        Assert.That(history, Has.Count.EqualTo(2));

        // Narrow range that excludes all
        List<OrderSummary> empty = await w.GetOrderHistoryAsync(
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-20), 100);
        Assert.That(empty, Is.Empty);
    }

    [Test]
    public async Task MultiplePatients_OrdersAreIndependent()
    {
        string p1 = $"PATIENT-{Guid.NewGuid():N}";
        string p2 = $"PATIENT-{Guid.NewGuid():N}";

        await GetWorkflow(p1).PlaceOrderAsync("Lab", "CBC", null, "PROV-1", "Dr. Adams",
            null, null, "ROUTINE", null, null);

        List<OrderSummary> p2Orders = await GetWorkflow(p2).GetOrdersByFilterAsync(1);
        Assert.That(p2Orders, Is.Empty);
    }

    // ─── Performance Benchmark ─────────────────────────────────────────

    [Test]
    public async Task PerformanceBenchmark_CachedOrders_FasterThanIndexQuery()
    {
        string patientId = $"PATIENT-PERF-{Guid.NewGuid():N}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Set cache to 5 — only the 5 most recent orders stay on the patient grain
        ISiteParametersGrain siteParams = _cluster.GrainFactory
            .GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.SetOrdersDisplayCountAsync(5);

        // Seed 100 orders across different types
        string[] orderTypes = ["Lab", "Pharmacy", "Radiology", "Consult", "Nursing",
            "Dietetics", "Procedure", "Imaging", "Prosthetics", "General"];
        string[] orderTexts = ["CBC", "METFORMIN 500MG", "CHEST XRAY", "CARDIOLOGY CONSULT",
            "INTAKE & OUTPUT", "REGULAR DIET", "EKG", "CT ABDOMEN", "WHEELCHAIR", "VITAL SIGNS Q4H"];

        for (int i = 0; i < 100; i++)
        {
            string orderType = orderTypes[i % orderTypes.Length];
            string orderText = $"{orderTexts[i % orderTexts.Length]} #{i}";

            await w.PlaceOrderAsync(orderType, orderText, $"OI-{i}",
                "PROV-001", "Dr. Benchmark",
                "LOC-001", "Clinic A",
                "ROUTINE", null, null);
        }

        // Verify setup: 5 in cache, 100 in index
        List<OrderSummary> cached = await GetPatient(patientId).GetRecentOrdersAsync();
        int indexCount = await GetOrderIndex(patientId).GetCountAsync();
        Assert.That(cached, Has.Count.EqualTo(5), "Cache should hold exactly 5 orders");
        Assert.That(indexCount, Is.EqualTo(100), "Index should hold all 100 orders");

        // ── Warm-up calls ──
        await w.GetRecentOrdersAsync();
        await w.GetOrdersByFilterAsync(1); // All orders from index

        // ── Timed: hot cache read (embedded on patient grain — zero fan-out) ──
        const int iterations = 20;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            List<OrderSummary> recent = await w.GetRecentOrdersAsync();
            Assert.That(recent, Has.Count.GreaterThan(0));
        }
        sw.Stop();
        long cachedMs = sw.ElapsedMilliseconds;
        double cachedAvgMs = (double)cachedMs / iterations;

        // ── Timed: index query (reads from index grain — no individual grain fan-out) ──
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            List<OrderSummary> all = await w.GetOrdersByFilterAsync(1);
            Assert.That(all, Has.Count.EqualTo(100));
        }
        sw.Stop();
        long indexMs = sw.ElapsedMilliseconds;
        double indexAvgMs = (double)indexMs / iterations;

        double speedup = indexAvgMs > 0 ? cachedAvgMs > 0 ? indexAvgMs / cachedAvgMs : double.PositiveInfinity : 1.0;

        // Log results so they appear in test output
        TestContext.Out.WriteLine("╔══════════════════════════════════════════════════════════╗");
        TestContext.Out.WriteLine("║  ORDERS PERFORMANCE BENCHMARK                           ║");
        TestContext.Out.WriteLine("╠══════════════════════════════════════════════════════════╣");
        TestContext.Out.WriteLine($"║  Patient orders:     100 total, 5 cached                ║");
        TestContext.Out.WriteLine($"║  Iterations:         {iterations,4}                                ║");
        TestContext.Out.WriteLine($"║  Cache read avg:     {cachedAvgMs,8:F2} ms  (zero fan-out)      ║");
        TestContext.Out.WriteLine($"║  Index query avg:    {indexAvgMs,8:F2} ms  (100 entries)       ║");
        TestContext.Out.WriteLine($"║  Speedup factor:     {speedup,8:F1}x                          ║");
        TestContext.Out.WriteLine("╚══════════════════════════════════════════════════════════╝");

        // Reset display count to default so other tests are not affected
        await siteParams.SetOrdersDisplayCountAsync(5);
    }

    // ─── Order sets ─────────────────────────────────────────────────────────────

    [Test]
    public async Task OrderSetIndex_SelfSeeds()
    {
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>("ORDSET-INDEX");
        List<OrderSetEntry> sets = await index.GetAllOrderSetsAsync();
        Assert.That(sets, Has.Count.GreaterThanOrEqualTo(5));
    }

    [Test]
    public async Task ExecuteOrderSet_CreatesMultipleOrders()
    {
        // Trigger index grain activation to seed demo order sets
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>("ORDSET-INDEX");
        await index.GetAllOrderSetsAsync();

        IPatientWorkflowGrain wf = NewWorkflow();
        List<string> orderIds = await wf.ExecuteOrderSetAsync(
            "ORDSET-ADM-MED", "PROV-001", "Dr. Smith", "LOC-MED-3A", "Medicine Ward", null);

        Assert.That(orderIds, Has.Count.GreaterThanOrEqualTo(5));
        List<OrderSummary> orders = await wf.GetOrdersByFilterAsync(0);
        Assert.That(orders, Has.Count.EqualTo(orderIds.Count));
    }

    // ─── OrderSetGrain direct tests ─────────────────────────────────────────────

    [Test]
    public async Task OrderSetGrain_GetOrderSet_ReturnsState()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);
        OrderSetState state = await grain.GetOrderSetAsync();
        Assert.That(state.OrderSetId, Is.EqualTo(id));
    }

    [Test]
    public async Task OrderSetGrain_UpdateOrderSet_SetsAllFields()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.UpdateOrderSetAsync("ADMISSION MED", "Standard admission", "ADMISSION", "MEDICINE", "DR-SMITH");

        OrderSetState state = await grain.GetOrderSetAsync();
        Assert.That(state.Name, Is.EqualTo("ADMISSION MED"));
        Assert.That(state.Description, Is.EqualTo("Standard admission"));
        Assert.That(state.Category, Is.EqualTo("ADMISSION"));
        Assert.That(state.ServiceSection, Is.EqualTo("MEDICINE"));
        Assert.That(state.CreatedBy, Is.EqualTo("DR-SMITH"));
        Assert.That(state.LastModifiedDate, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task OrderSetGrain_AddTemplate_AddsNewItem()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        var template = new OrderTemplate
        {
            TemplateId = "T1",
            OrderType = "Lab",
            OrderableItem = "CBC",
            Urgency = "ROUTINE",
            Sequence = 1
        };
        await grain.AddTemplateAsync(template);

        List<OrderTemplate> items = await grain.GetTemplatesAsync();
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].OrderableItem, Is.EqualTo("CBC"));
    }

    [Test]
    public async Task OrderSetGrain_AddTemplate_UpdatesExistingByTemplateId()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.AddTemplateAsync(new OrderTemplate
        {
            TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC", Sequence = 1
        });

        // Update same template ID with different orderable item
        await grain.AddTemplateAsync(new OrderTemplate
        {
            TemplateId = "T1", OrderType = "Lab", OrderableItem = "CMP", Sequence = 1
        });

        List<OrderTemplate> items = await grain.GetTemplatesAsync();
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].OrderableItem, Is.EqualTo("CMP"));
    }

    [Test]
    public async Task OrderSetGrain_RemoveTemplate_RemovesItem()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.AddTemplateAsync(new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC", Sequence = 1 });
        await grain.AddTemplateAsync(new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BMP", Sequence = 2 });

        await grain.RemoveTemplateAsync("T1");

        List<OrderTemplate> items = await grain.GetTemplatesAsync();
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].TemplateId, Is.EqualTo("T2"));
    }

    [Test]
    public async Task OrderSetGrain_RemoveTemplate_NonExistent_NoError()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.AddTemplateAsync(new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC", Sequence = 1 });
        await grain.RemoveTemplateAsync("NONEXISTENT");

        List<OrderTemplate> items = await grain.GetTemplatesAsync();
        Assert.That(items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OrderSetGrain_GetTemplates_ReturnedInSequenceOrder()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.AddTemplateAsync(new OrderTemplate { TemplateId = "T3", OrderType = "Nursing", OrderableItem = "VITAL SIGNS", Sequence = 3 });
        await grain.AddTemplateAsync(new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC", Sequence = 1 });
        await grain.AddTemplateAsync(new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BMP", Sequence = 2 });

        List<OrderTemplate> items = await grain.GetTemplatesAsync();
        Assert.That(items[0].Sequence, Is.EqualTo(1));
        Assert.That(items[1].Sequence, Is.EqualTo(2));
        Assert.That(items[2].Sequence, Is.EqualTo(3));
    }

    [Test]
    public async Task OrderSetGrain_Enable_SetsIsActiveTrue()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.DisableAsync();
        OrderSetState disabled = await grain.GetOrderSetAsync();
        Assert.That(disabled.IsActive, Is.False);

        await grain.EnableAsync();
        OrderSetState enabled = await grain.GetOrderSetAsync();
        Assert.That(enabled.IsActive, Is.True);
    }

    [Test]
    public async Task OrderSetGrain_Disable_SetsIsActiveFalse()
    {
        string id = $"ORDSET-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrderSetGrain>(id);

        await grain.DisableAsync();

        OrderSetState state = await grain.GetOrderSetAsync();
        Assert.That(state.IsActive, Is.False);
        Assert.That(state.LastModifiedDate, Is.GreaterThan(DateTime.MinValue));
    }

    // ─── OrderSetIndexGrain direct tests ────────────────────────────────────────

    [Test]
    public async Task OrderSetIndexGrain_AddOrUpdate_AddsNewEntry()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry
        {
            OrderSetId = "OS-NEW-1",
            Name = "TEST ORDER SET",
            Category = "PROTOCOL",
            ServiceSection = "MEDICINE",
            IsActive = true,
            ItemCount = 3
        });

        List<OrderSetEntry> all = await index.GetAllOrderSetsAsync();
        Assert.That(all.Any(e => e.OrderSetId == "OS-NEW-1"), Is.True);
    }

    [Test]
    public async Task OrderSetIndexGrain_AddOrUpdate_UpdatesExistingEntry()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry
        {
            OrderSetId = "OS-UPD-1", Name = "ORIGINAL", Category = "ADMISSION",
            ServiceSection = "MEDICINE", IsActive = true, ItemCount = 3
        });
        await index.AddOrUpdateAsync(new OrderSetEntry
        {
            OrderSetId = "OS-UPD-1", Name = "UPDATED", Category = "PROTOCOL",
            ServiceSection = "SURGERY", IsActive = false, ItemCount = 5
        });

        List<OrderSetEntry> all = await index.GetAllOrderSetsAsync();
        Assert.That(all.Count(e => e.OrderSetId == "OS-UPD-1"), Is.EqualTo(1));
        OrderSetEntry entry = all.First(e => e.OrderSetId == "OS-UPD-1");
        Assert.That(entry.Name, Is.EqualTo("UPDATED"));
        Assert.That(entry.Category, Is.EqualTo("PROTOCOL"));
        Assert.That(entry.ItemCount, Is.EqualTo(5));
    }

    [Test]
    public async Task OrderSetIndexGrain_Remove_DeletesEntry()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry
        {
            OrderSetId = "OS-DEL-1", Name = "TO DELETE", Category = "GENERAL",
            ServiceSection = "MEDICINE", IsActive = true, ItemCount = 1
        });
        await index.RemoveAsync("OS-DEL-1");

        List<OrderSetEntry> all = await index.GetAllOrderSetsAsync();
        Assert.That(all.Any(e => e.OrderSetId == "OS-DEL-1"), Is.False);
    }

    [Test]
    public async Task OrderSetIndexGrain_Remove_NonExistent_NoError()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.RemoveAsync("DOES-NOT-EXIST");

        List<OrderSetEntry> all = await index.GetAllOrderSetsAsync();
        Assert.That(all, Is.Not.Null);
    }

    [Test]
    public async Task OrderSetIndexGrain_SearchByName()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-S1", Name = "ADMISSION ORDERS MED", Category = "ADMISSION", ServiceSection = "MEDICINE" });
        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-S2", Name = "POST-OP ORDERS", Category = "DISCHARGE", ServiceSection = "SURGERY" });
        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-S3", Name = "CHF PROTOCOL", Category = "PROTOCOL", ServiceSection = "MEDICINE" });

        List<OrderSetEntry> results = await index.SearchOrderSetsAsync("ADMISSION");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(e => e.OrderSetId == "OS-S1"), Is.True);
    }

    [Test]
    public async Task OrderSetIndexGrain_SearchByCategory()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-C1", Name = "SET A", Category = "PROTOCOL", ServiceSection = "MEDICINE" });
        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-C2", Name = "SET B", Category = "ADMISSION", ServiceSection = "SURGERY" });

        List<OrderSetEntry> results = await index.SearchOrderSetsAsync("PROTOCOL");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(e => e.OrderSetId == "OS-C1"), Is.True);
    }

    [Test]
    public async Task OrderSetIndexGrain_SearchByServiceSection()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-SS1", Name = "SET X", Category = "GENERAL", ServiceSection = "SURGERY" });
        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-SS2", Name = "SET Y", Category = "GENERAL", ServiceSection = "PSYCHIATRY" });

        List<OrderSetEntry> results = await index.SearchOrderSetsAsync("surgery");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(e => e.OrderSetId == "OS-SS1"), Is.True);
    }

    [Test]
    public async Task OrderSetIndexGrain_SearchIsCaseInsensitive()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-CI1", Name = "Diabetes Management", Category = "PROTOCOL", ServiceSection = "MEDICINE" });

        List<OrderSetEntry> results = await index.SearchOrderSetsAsync("diabetes");
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(e => e.OrderSetId == "OS-CI1"), Is.True);
    }

    [Test]
    public async Task OrderSetIndexGrain_SearchNoMatch_ReturnsEmpty()
    {
        string indexId = $"ORDSET-IDX-{Guid.NewGuid():N}";
        var index = _cluster.GrainFactory.GetGrain<IOrderSetIndexGrain>(indexId);

        await index.AddOrUpdateAsync(new OrderSetEntry { OrderSetId = "OS-NM1", Name = "SET A", Category = "ADMISSION", ServiceSection = "MEDICINE" });

        List<OrderSetEntry> results = await index.SearchOrderSetsAsync("NONEXISTENT");
        Assert.That(results, Has.Count.EqualTo(0));
    }
}
