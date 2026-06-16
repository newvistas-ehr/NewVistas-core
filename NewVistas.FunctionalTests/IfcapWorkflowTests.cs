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
/// Functional tests for IFCAP (Integrated Funds Distribution, Control Point Activity,
/// and Procurement) — VistA Files #410, #2237, #442, #420.5, #440.
/// Tests the full procurement workflow: Control Point → Purchase Request → PO → Receipt.
/// </summary>
[TestFixture]
public class IfcapWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IControlPointGrain NewControlPoint()
        => _cluster.GrainFactory.GetGrain<IControlPointGrain>($"IFCAP-CP:{Guid.NewGuid()}");

    private IControlPointIndexGrain GetCpIndex()
        => _cluster.GrainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");

    private IPurchaseRequestGrain NewPR()
        => _cluster.GrainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{Guid.NewGuid()}");

    private IPurchaseRequestIndexGrain GetPRIndex(string cpId)
        => _cluster.GrainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");

    private IPurchaseOrderGrain NewPO()
        => _cluster.GrainFactory.GetGrain<IPurchaseOrderGrain>($"IFCAP-PO:{Guid.NewGuid()}");

    private IPurchaseOrderIndexGrain GetPOIndex()
        => _cluster.GrainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");

    private IReceivingReportGrain NewRR()
        => _cluster.GrainFactory.GetGrain<IReceivingReportGrain>($"IFCAP-RR:{Guid.NewGuid()}");

    private IIfcapVendorGrain GetVendor(string id)
        => _cluster.GrainFactory.GetGrain<IIfcapVendorGrain>($"IFCAP-VENDOR:{id}");

    private IIfcapVendorIndexGrain GetVendorIndex()
        => _cluster.GrainFactory.GetGrain<IIfcapVendorIndexGrain>("IFCAP-VENDOR-IDX");

    private IIfcapSiteParametersGrain GetSiteParams()
        => _cluster.GrainFactory.GetGrain<IIfcapSiteParametersGrain>("IFCAP-SITE-PARAMS");

    private List<PurchaseRequestLineItem> DefaultPRLines() => new()
    {
        new PurchaseRequestLineItem(1, "Office Supplies", 10m, "EA", 5m, 50m, null, null, null)
    };

    private List<PoLineItem> DefaultPOLines() => new()
    {
        new PoLineItem(1, "Office Supplies", 10m, 0m, "EA", 5m, 50m, null, null)
    };

    // ─── Control Point ────────────────────────────────────────────────────

    [Test]
    public async Task CreateControlPoint_SetsActiveStatus()
    {
        IControlPointGrain cp = NewControlPoint();
        await cp.CreateAsync("Medical Service", "FAC-001", "SVC-MED", 2025, "500-01",
            10000m, "USR-001", "Budget Officer");

        ControlPointState state = await cp.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ControlPointStatus.Active));
    }

    [Test]
    public async Task CreateControlPoint_StoresAllocationAndBalance()
    {
        IControlPointGrain cp = NewControlPoint();
        await cp.CreateAsync("Nursing Service", "FAC-001", "SVC-NUR", 2025, "500-02",
            5000m, "USR-001", "Budget Officer");

        ControlPointState state = await cp.GetAsync();
        Assert.That(state.AllocatedAmount, Is.EqualTo(5000m));
        Assert.That(state.RemainingBalance, Is.EqualTo(5000m));
        Assert.That(state.ObligatedAmount, Is.EqualTo(0m));
        Assert.That(state.ExpendedAmount, Is.EqualTo(0m));
    }

    [Test]
    public async Task AllocateFunds_IncreasesAllocatedAndRemainingBalance()
    {
        IControlPointGrain cp = NewControlPoint();
        await cp.CreateAsync("Supply Service", "FAC-001", "SVC-SUP", 2025, "500-03",
            2000m, "USR-001", "Budget Officer");

        await cp.AllocateFundsAsync(500m, "USR-BUDGET");

        ControlPointState state = await cp.GetAsync();
        Assert.That(state.AllocatedAmount, Is.EqualTo(2500m));
        Assert.That(state.RemainingBalance, Is.EqualTo(2500m));
    }

    [Test]
    public async Task ObligateFunds_MovesFromRemainingToObligated()
    {
        IControlPointGrain cp = NewControlPoint();
        await cp.CreateAsync("Lab Service", "FAC-001", "SVC-LAB", 2025, "500-04",
            3000m, "USR-001", "Budget Officer");

        await cp.ObligateFundsAsync(500m, "IFCAP-PR-REQ-001");

        ControlPointState state = await cp.GetAsync();
        Assert.That(state.RemainingBalance, Is.EqualTo(2500m));
        Assert.That(state.ObligatedAmount, Is.EqualTo(500m));
        Assert.That(state.RequestIds, Contains.Item("IFCAP-PR-REQ-001"));
    }

    [Test]
    public async Task ExpenditureFunds_MovesFromObligatedToExpended()
    {
        IControlPointGrain cp = NewControlPoint();
        await cp.CreateAsync("Pharmacy Service", "FAC-001", "SVC-RX", 2025, "500-05",
            4000m, "USR-001", "Budget Officer");
        await cp.ObligateFundsAsync(1000m, "IFCAP-PR-REQ-002");

        await cp.ExpenditureAsync(1000m, "IFCAP-PO-001");

        ControlPointState state = await cp.GetAsync();
        Assert.That(state.ObligatedAmount, Is.EqualTo(0m));
        Assert.That(state.ExpendedAmount, Is.EqualTo(1000m));
    }

    [Test]
    public async Task UpdateStatus_ChangesControlPointStatus()
    {
        IControlPointGrain cp = NewControlPoint();
        await cp.CreateAsync("Dental Service", "FAC-001", "SVC-DEN", 2025, "500-06",
            1000m, "USR-001", "Budget Officer");

        await cp.UpdateStatusAsync(ControlPointStatus.Inactive);

        ControlPointState state = await cp.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ControlPointStatus.Inactive));
    }

    // ─── Purchase Request (2237) ──────────────────────────────────────────

    [Test]
    public async Task CreatePurchaseRequest_SetsDraftStatus()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "John Smith", 2025,
            PurchaseRequestPriority.Routine, null,
            "Office supplies for ward", "Monthly restocking",
            50m, DefaultPRLines(), null, null, null);

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseRequestStatus.Draft));
    }

    [Test]
    public async Task CreatePurchaseRequest_StoresEstimatedCostAndLines()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "John Smith", 2025,
            PurchaseRequestPriority.Urgent, DateTime.UtcNow.AddDays(7),
            "Medical equipment", "Patient care need",
            200m, DefaultPRLines(), "VENDOR-001", "Supply Co.", "Urgent order");

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.EstimatedCost, Is.EqualTo(200m));
        Assert.That(state.Priority, Is.EqualTo(PurchaseRequestPriority.Urgent));
        Assert.That(state.LineItems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SubmitPurchaseRequest_SetsSubmittedStatus()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "John Smith", 2025,
            PurchaseRequestPriority.Routine, null,
            "Supplies", "Need supplies", 50m, DefaultPRLines(), null, null, null);

        await pr.SubmitAsync("USR-001");

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseRequestStatus.Submitted));
    }

    [Test]
    public async Task ApprovePurchaseRequest_SetsApprovedStatus()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "Requestor", 2025,
            PurchaseRequestPriority.Routine, null,
            "Supplies", "Need supplies", 50m, DefaultPRLines(), null, null, null);
        await pr.SubmitAsync("USR-001");

        await pr.ApproveAsync("USR-CP-OFFICER", "Control Point Officer");

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseRequestStatus.Approved));
        Assert.That(state.ApprovedByUserId, Is.EqualTo("USR-CP-OFFICER"));
        Assert.That(state.ApprovalDate, Is.Not.Null);
    }

    [Test]
    public async Task RejectPurchaseRequest_SetsRejectedStatus()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "Requestor", 2025,
            PurchaseRequestPriority.Routine, null,
            "Non-essential item", "Nice to have", 500m, DefaultPRLines(), null, null, null);
        await pr.SubmitAsync("USR-001");

        await pr.RejectAsync("USR-CP-OFFICER", "Insufficient budget justification");

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseRequestStatus.Rejected));
        Assert.That(state.RejectionReason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task CancelPurchaseRequest_SetsCancelledStatus()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "Requestor", 2025,
            PurchaseRequestPriority.Routine, null,
            "Supplies", "Need", 50m, DefaultPRLines(), null, null, null);

        await pr.CancelAsync("No longer needed");

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseRequestStatus.Cancelled));
    }

    [Test]
    public async Task FundPurchaseRequest_SetsFundedStatusAndRecordsPO()
    {
        IPurchaseRequestGrain pr = NewPR();
        await pr.CreateAsync(
            "IFCAP-CP-001", "USR-001", "Requestor", 2025,
            PurchaseRequestPriority.Routine, null,
            "Supplies", "Need", 50m, DefaultPRLines(), null, null, null);
        await pr.SubmitAsync("USR-001");
        await pr.ApproveAsync("USR-CP-OFFICER", "Officer");

        await pr.FundAsync("IFCAP-PO-FUNDED-001");

        PurchaseRequestState state = await pr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseRequestStatus.Funded));
        Assert.That(state.PurchaseOrderId, Is.EqualTo("IFCAP-PO-FUNDED-001"));
    }

    // ─── Purchase Order ───────────────────────────────────────────────────

    [Test]
    public async Task CreatePurchaseOrder_SetsOpenStatus()
    {
        IPurchaseOrderGrain po = NewPO();
        await po.CreateAsync(
            "IFCAP-PR-001", "IFCAP-CP-001",
            "VENDOR-001", "Office Depot", "123 Main St",
            DateTime.UtcNow.AddDays(14), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");

        PurchaseOrderState state = await po.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseOrderStatus.Open));
    }

    [Test]
    public async Task CreatePurchaseOrder_StoresVendorAndLines()
    {
        IPurchaseOrderGrain po = NewPO();
        await po.CreateAsync(
            "IFCAP-PR-002", "IFCAP-CP-001",
            "VENDOR-002", "Staples", null,
            DateTime.UtcNow.AddDays(7), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");

        PurchaseOrderState state = await po.GetAsync();
        Assert.That(state.VendorName, Is.EqualTo("Staples"));
        Assert.That(state.LineItems, Has.Count.EqualTo(1));
        Assert.That(state.AmountReceived, Is.EqualTo(0m));
    }

    [Test]
    public async Task RecordReceipt_FullQuantity_SetsReceivedStatus()
    {
        IPurchaseOrderGrain po = NewPO();
        await po.CreateAsync(
            "IFCAP-PR-003", "IFCAP-CP-001",
            "VENDOR-001", "Office Depot", null,
            DateTime.UtcNow.AddDays(14), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");

        string rrId = $"IFCAP-RR-{Guid.NewGuid()}";
        List<(int LineNumber, decimal QuantityAccepted)> receipts = new() { (1, 10m) };
        await po.RecordReceiptAsync(rrId, receipts);

        PurchaseOrderState state = await po.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseOrderStatus.Received));
        Assert.That(state.AmountReceived, Is.EqualTo(50m));
        Assert.That(state.ReceivingReportIds, Contains.Item(rrId));
    }

    [Test]
    public async Task RecordReceipt_PartialQuantity_SetsPartiallyReceivedStatus()
    {
        IPurchaseOrderGrain po = NewPO();
        await po.CreateAsync(
            "IFCAP-PR-004", "IFCAP-CP-001",
            "VENDOR-001", "Office Depot", null,
            DateTime.UtcNow.AddDays(14), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");

        string rrId = $"IFCAP-RR-{Guid.NewGuid()}";
        List<(int LineNumber, decimal QuantityAccepted)> receipts = new() { (1, 5m) }; // only 5 of 10
        await po.RecordReceiptAsync(rrId, receipts);

        PurchaseOrderState state = await po.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseOrderStatus.PartiallyReceived));
        Assert.That(state.AmountReceived, Is.EqualTo(25m)); // 5 × $5
    }

    [Test]
    public async Task CancelPurchaseOrder_SetsCancelledStatus()
    {
        IPurchaseOrderGrain po = NewPO();
        await po.CreateAsync(
            "IFCAP-PR-005", "IFCAP-CP-001",
            "VENDOR-001", "Office Depot", null,
            DateTime.UtcNow.AddDays(14), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");

        await po.CancelAsync("Vendor unable to fulfill", "USR-CONTRACTING");

        PurchaseOrderState state = await po.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseOrderStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ClosePurchaseOrder_SetsClosedStatus()
    {
        IPurchaseOrderGrain po = NewPO();
        await po.CreateAsync(
            "IFCAP-PR-006", "IFCAP-CP-001",
            "VENDOR-001", "Office Depot", null,
            DateTime.UtcNow.AddDays(14), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");
        string rrId = $"IFCAP-RR-{Guid.NewGuid()}";
        await po.RecordReceiptAsync(rrId, new List<(int, decimal)> { (1, 10m) });

        await po.CloseAsync("USR-CONTRACTING");

        PurchaseOrderState state = await po.GetAsync();
        Assert.That(state.Status, Is.EqualTo(PurchaseOrderStatus.Closed));
    }

    // ─── Receiving Report ─────────────────────────────────────────────────

    [Test]
    public async Task CreateReceivingReport_SetsDraftStatus()
    {
        IReceivingReportGrain rr = NewRR();
        List<RrLineItem> lines = new()
        {
            new RrLineItem(1, "Office Supplies", 10m, 10m, 10m, 5m, 50m, null)
        };

        await rr.CreateAsync("IFCAP-PO-001", "IFCAP-CP-001", "USR-001", "Receiver", lines, null);

        ReceivingReportState state = await rr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ReceivingReportStatus.Draft));
        Assert.That(state.LineItems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AcceptReceivingReport_SetsAcceptedStatus()
    {
        IReceivingReportGrain rr = NewRR();
        List<RrLineItem> lines = new()
        {
            new RrLineItem(1, "Supplies", 5m, 5m, 5m, 10m, 50m, null)
        };
        await rr.CreateAsync("IFCAP-PO-002", "IFCAP-CP-001", "USR-001", "Receiver", lines, null);

        await rr.AcceptAsync("USR-SUPERVISOR");

        ReceivingReportState state = await rr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ReceivingReportStatus.Accepted));
    }

    [Test]
    public async Task RejectReceivingReport_SetsRejectedStatus()
    {
        IReceivingReportGrain rr = NewRR();
        List<RrLineItem> lines = new()
        {
            new RrLineItem(1, "Supplies", 10m, 10m, 3m, 5m, 50m, null)
        };
        await rr.CreateAsync("IFCAP-PO-003", "IFCAP-CP-001", "USR-001", "Receiver", lines, null);

        await rr.RejectAsync("Items damaged in transit", "USR-SUPERVISOR");

        ReceivingReportState state = await rr.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ReceivingReportStatus.Rejected));
        Assert.That(state.RejectionReason, Is.Not.Null.And.Not.Empty);
    }

    // ─── Vendor ───────────────────────────────────────────────────────────

    [Test]
    public async Task VendorIndex_AddOrUpdate_AppearsInGetAll()
    {
        IIfcapVendorIndexGrain index = GetVendorIndex();

        string vendorId = $"IFCAP-VENDOR-{Guid.NewGuid()}";
        IfcapVendorIndexEntry entry = new IfcapVendorIndexEntry(vendorId, "Office Depot", "OD-001", true, false, false);

        await index.AddOrUpdateAsync(entry);
        List<IfcapVendorIndexEntry> all = await index.GetAllAsync();

        Assert.That(all.Any(v => v.VendorId == vendorId), Is.True);
    }

    [Test]
    public async Task VendorIndex_SearchByName_ReturnsMatchingVendors()
    {
        IIfcapVendorIndexGrain index = GetVendorIndex();

        string uniqueName = $"VetSup-{Guid.NewGuid():N}";
        string vendorId = $"IFCAP-VENDOR-{Guid.NewGuid()}";

        await index.AddOrUpdateAsync(new IfcapVendorIndexEntry(vendorId, uniqueName, "VS-001", true, true, true));

        List<IfcapVendorIndexEntry> results = await index.SearchAsync(uniqueName);

        Assert.That(results.Any(v => v.VendorId == vendorId), Is.True);
    }

    // ─── Site Parameters ─────────────────────────────────────────────────

    [Test]
    public async Task UpdateSiteParameters_StoresValues()
    {
        IIfcapSiteParametersGrain siteParams = GetSiteParams();
        await siteParams.UpdateAsync(
            "VAMC Main", "549", 2025, 30,
            true, 2500m, "549-25-", "USR-001");

        IfcapSiteParametersState state = await siteParams.GetAsync();
        Assert.That(state.SiteName, Is.EqualTo("VAMC Main"));
        Assert.That(state.FacilityNumber, Is.EqualTo("549"));
        Assert.That(state.FiscalYear, Is.EqualTo(2025));
        Assert.That(state.IsAutoApprovalEnabled, Is.True);
        Assert.That(state.AutoApprovalThreshold, Is.EqualTo(2500m));
        Assert.That(state.PONumberPrefix, Is.EqualTo("549-25-"));
    }

    // ─── Full Procurement Workflow (Integration) ──────────────────────────

    [Test]
    public async Task FullProcurementWorkflow_FromRequestToReceipt()
    {
        // Step 1: Create Control Point with budget
        IControlPointGrain cp = NewControlPoint();
        string cpId = cp.GetGrainId().ToString();
        await cp.CreateAsync("Test Service", "FAC-001", "SVC-TEST", 2025, "500-99",
            5000m, "USR-001", "Budget Officer");

        // Step 2: Create and submit a Purchase Request
        IPurchaseRequestGrain pr = NewPR();
        string prId = pr.GetGrainId().ToString();
        await pr.CreateAsync(
            cpId, "USR-002", "Requestor Name", 2025,
            PurchaseRequestPriority.Routine, null,
            "Test supplies", "Needed for operations",
            500m, DefaultPRLines(), "VENDOR-001", "Supply Co.", null);
        await pr.SubmitAsync("USR-002");

        // Step 3: Approve the request — obligates funds in CP
        await pr.ApproveAsync("USR-CP-OFFICER", "CP Officer");
        await cp.ObligateFundsAsync(500m, prId);

        // Step 4: Create a Purchase Order — expends funds in CP
        IPurchaseOrderGrain po = NewPO();
        string poId = po.GetGrainId().ToString();
        await po.CreateAsync(
            prId, cpId,
            "VENDOR-001", "Supply Co.", null,
            DateTime.UtcNow.AddDays(14), DefaultPOLines(),
            "USR-CONTRACTING", "Contracting Officer");
        await pr.FundAsync(poId);
        await cp.ExpenditureAsync(500m, poId);

        // Step 5: Receive goods — full receipt
        string rrId = $"IFCAP-RR-{Guid.NewGuid()}";
        await po.RecordReceiptAsync(rrId, new List<(int, decimal)> { (1, 10m) });

        // Verify final state of all entities
        ControlPointState cpState = await cp.GetAsync();
        Assert.That(cpState.ExpendedAmount, Is.EqualTo(500m));
        Assert.That(cpState.ObligatedAmount, Is.EqualTo(0m));
        Assert.That(cpState.RemainingBalance, Is.EqualTo(4500m));

        PurchaseRequestState prState = await pr.GetAsync();
        Assert.That(prState.Status, Is.EqualTo(PurchaseRequestStatus.Funded));
        Assert.That(prState.PurchaseOrderId, Is.EqualTo(poId));

        PurchaseOrderState poState = await po.GetAsync();
        Assert.That(poState.Status, Is.EqualTo(PurchaseOrderStatus.Received));
        Assert.That(poState.AmountReceived, Is.EqualTo(50m));
    }
}
