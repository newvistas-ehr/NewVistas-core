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
/// Functional tests for VistA Fee Basis — Files #162, #162.1, #162.5, #162.6, #162.7.
/// Tests authorization/invoice lifecycle, cross-grain payment chain, and batch disbursement.
/// </summary>
[TestFixture]
public class FeeBasisWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IFeeAuthorizationGrain NewAuth()
        => _cluster.GrainFactory.GetGrain<IFeeAuthorizationGrain>($"FEE-AUTH:{Guid.NewGuid()}");

    private IFeeAuthorizationGrain GetAuth(string id)
        => _cluster.GrainFactory.GetGrain<IFeeAuthorizationGrain>(id);

    private IFeeInvoiceGrain NewInvoice()
        => _cluster.GrainFactory.GetGrain<IFeeInvoiceGrain>($"FEE-INVOICE:{Guid.NewGuid()}");

    private IFeeVendorGrain GetVendor(string id)
        => _cluster.GrainFactory.GetGrain<IFeeVendorGrain>($"FEE-VENDOR:{id}");

    private IFeeVendorIndexGrain GetVendorIndex()
        => _cluster.GrainFactory.GetGrain<IFeeVendorIndexGrain>("FEE-VENDOR-IDX");

    private IFeeAuthorizationIndexGrain GetAuthIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IFeeAuthorizationIndexGrain>($"FEE-AUTH-IDX:{patientId}");

    private IFeeBatchPaymentGrain NewBatch()
        => _cluster.GrainFactory.GetGrain<IFeeBatchPaymentGrain>($"FEE-BATCH:{Guid.NewGuid()}");

    // ─── Authorization Creation ───────────────────────────────────────────

    [Test]
    public async Task CreateAuthorization_SetsPendingStatus()
    {
        IFeeAuthorizationGrain auth = NewAuth();
        await auth.CreateAsync(
            "PAT-FEE-001", "VENDOR-001", "Dr. Smith Community Care",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(6), 500m,
            "USR-001", "Supervisor", "Outpatient evaluation", null, "J11.1", "AUTH-2024-001", null);

        FeeAuthorizationState state = await auth.GetAsync();
        Assert.That(state.Status, Is.EqualTo(FeeAuthorizationStatus.Active));
    }

    [Test]
    public async Task CreateAuthorization_StoresAmountAndVendor()
    {
        IFeeAuthorizationGrain auth = NewAuth();
        await auth.CreateAsync(
            "PAT-FEE-002", "VENDOR-002", "City Dental",
            FeeServiceType.Dental, DateTime.UtcNow, DateTime.UtcNow,
            null, 1000m,
            "USR-001", "Supervisor", "Dental evaluation", 4, null, null, null);

        FeeAuthorizationState state = await auth.GetAsync();
        Assert.That(state.AuthorizedAmount, Is.EqualTo(1000m));
        Assert.That(state.RemainingAmount, Is.EqualTo(1000m));
        Assert.That(state.VendorName, Is.EqualTo("City Dental"));
        Assert.That(state.ServiceType, Is.EqualTo(FeeServiceType.Dental));
    }

    [Test]
    public async Task SuspendAuthorization_SetsSuspendedStatus()
    {
        IFeeAuthorizationGrain auth = NewAuth();
        await auth.CreateAsync(
            "PAT-FEE-010", "VENDOR-001", "Dr. Smith",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            null, 500m, "USR-001", "Supervisor", "Eval", null, null, null, null);

        await auth.SuspendAsync("Pending eligibility review", "USR-002");

        FeeAuthorizationState state = await auth.GetAsync();
        Assert.That(state.Status, Is.EqualTo(FeeAuthorizationStatus.Suspended));
    }

    [Test]
    public async Task CancelAuthorization_SetsCancelledStatus()
    {
        IFeeAuthorizationGrain auth = NewAuth();
        await auth.CreateAsync(
            "PAT-FEE-011", "VENDOR-001", "Dr. Smith",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            null, 500m, "USR-001", "Supervisor", "Eval", null, null, null, null);

        await auth.CancelAsync("Patient deceased", "USR-002");

        FeeAuthorizationState state = await auth.GetAsync();
        Assert.That(state.Status, Is.EqualTo(FeeAuthorizationStatus.Cancelled));
    }

    // ─── Invoice Submission and Review ───────────────────────────────────

    [Test]
    public async Task SubmitInvoice_SetsReceivedStatus()
    {
        IFeeInvoiceGrain invoice = NewInvoice();
        await invoice.SubmitAsync(
            "FEE-AUTH-001", "PAT-FEE-020", "VENDOR-001", "Dr. Smith",
            "INV-2024-001", DateTime.UtcNow, "Outpatient",
            250m, "J11.1", new List<string> { "99213" }, null, null);

        FeeInvoiceState state = await invoice.GetAsync();
        Assert.That(state.Status, Is.EqualTo(FeeInvoiceStatus.Received));
        Assert.That(state.BilledAmount, Is.EqualTo(250m));
    }

    [Test]
    public async Task ApproveInvoice_SetsApprovedStatus()
    {
        IFeeInvoiceGrain invoice = NewInvoice();
        await invoice.SubmitAsync(
            "FEE-AUTH-002", "PAT-FEE-021", "VENDOR-001", "Dr. Smith",
            "INV-2024-002", DateTime.UtcNow, "Outpatient",
            250m, null, new List<string>(), null, null);

        await invoice.ApproveAsync(240m, "USR-REVIEWER", "Reviewer Name");

        FeeInvoiceState state = await invoice.GetAsync();
        Assert.That(state.Status, Is.EqualTo(FeeInvoiceStatus.Approved));
        Assert.That(state.ApprovedAmount, Is.EqualTo(240m));
    }

    [Test]
    public async Task RejectInvoice_SetsRejectedStatus()
    {
        IFeeInvoiceGrain invoice = NewInvoice();
        await invoice.SubmitAsync(
            "FEE-AUTH-003", "PAT-FEE-022", "VENDOR-002", "City Dental",
            "INV-2024-003", DateTime.UtcNow, "Dental",
            500m, null, new List<string>(), null, null);

        await invoice.RejectAsync("Missing supporting documentation", "USR-REVIEWER", "Reviewer Name");

        FeeInvoiceState state = await invoice.GetAsync();
        Assert.That(state.Status, Is.EqualTo(FeeInvoiceStatus.Rejected));
        Assert.That(state.RejectionReason, Is.Not.Null.And.Not.Empty);
    }

    // ─── Invoice Payment → Authorization Update (cross-grain) ────────────

    [Test]
    public async Task PayInvoice_SetsInvoicePaidStatus()
    {
        // Arrange: create auth then invoice using pre-generated key string
        string authId = $"FEE-AUTH:{Guid.NewGuid()}";
        IFeeAuthorizationGrain auth = _cluster.GrainFactory.GetGrain<IFeeAuthorizationGrain>(authId);
        await auth.CreateAsync(
            "PAT-FEE-030", "VENDOR-001", "Dr. Smith",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            null, 500m, "USR-001", "Supervisor", "Eval", null, null, null, null);

        IFeeInvoiceGrain invoice = NewInvoice();
        await invoice.SubmitAsync(
            authId, "PAT-FEE-030", "VENDOR-001", "Dr. Smith",
            "INV-2024-010", DateTime.UtcNow, "Outpatient",
            200m, null, new List<string>(), null, null);
        await invoice.ApproveAsync(200m, "USR-REVIEWER", "Reviewer");

        // Act: pay the invoice
        await invoice.PayAsync(200m, "Check", "CHK-500", DateTime.UtcNow);

        FeeInvoiceState invState = await invoice.GetAsync();
        Assert.That(invState.Status, Is.EqualTo(FeeInvoiceStatus.Paid));
    }

    [Test]
    public async Task PayInvoice_UpdatesAuthorizationSpentAmount()
    {
        // Arrange
        string authId = $"FEE-AUTH:{Guid.NewGuid()}";
        IFeeAuthorizationGrain auth = _cluster.GrainFactory.GetGrain<IFeeAuthorizationGrain>(authId);
        await auth.CreateAsync(
            "PAT-FEE-031", "VENDOR-001", "Dr. Smith",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            null, 500m, "USR-001", "Supervisor", "Eval", null, null, null, null);

        IFeeInvoiceGrain invoice = NewInvoice();
        await invoice.SubmitAsync(
            authId, "PAT-FEE-031", "VENDOR-001", "Dr. Smith",
            "INV-2024-011", DateTime.UtcNow, "Outpatient",
            200m, null, new List<string>(), null, null);
        await invoice.ApproveAsync(200m, "USR-REVIEWER", "Reviewer");

        // Act
        await invoice.PayAsync(200m, "Check", "CHK-501", DateTime.UtcNow);

        // Assert: auth should show 200 spent, 300 remaining
        FeeAuthorizationState authState = await auth.GetAsync();
        Assert.That(authState.SpentAmount, Is.EqualTo(200m));
        Assert.That(authState.RemainingAmount, Is.EqualTo(300m));
    }

    [Test]
    public async Task PayInvoice_ExhaustsAuthorization_SetsExhaustedStatus()
    {
        // Arrange: auth with exactly 200, invoice for 200
        string authId = $"FEE-AUTH:{Guid.NewGuid()}";
        IFeeAuthorizationGrain auth = _cluster.GrainFactory.GetGrain<IFeeAuthorizationGrain>(authId);
        await auth.CreateAsync(
            "PAT-FEE-032", "VENDOR-001", "Dr. Smith",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            null, 200m, "USR-001", "Supervisor", "Eval", null, null, null, null);

        IFeeInvoiceGrain invoice = NewInvoice();
        await invoice.SubmitAsync(
            authId, "PAT-FEE-032", "VENDOR-001", "Dr. Smith",
            "INV-2024-012", DateTime.UtcNow, "Outpatient",
            200m, null, new List<string>(), null, null);
        await invoice.ApproveAsync(200m, "USR-REVIEWER", "Reviewer");

        // Act
        await invoice.PayAsync(200m, "EFT", null, DateTime.UtcNow);

        // Assert: auth exhausted
        FeeAuthorizationState authState = await auth.GetAsync();
        Assert.That(authState.Status, Is.EqualTo(FeeAuthorizationStatus.Exhausted));
        Assert.That(authState.RemainingAmount, Is.EqualTo(0m));
    }

    // ─── Batch Payment ───────────────────────────────────────────────────

    [Test]
    public async Task CreateBatch_TotalAmountStartsAtZero()
    {
        IFeeBatchPaymentGrain batch = NewBatch();
        await batch.CreateAsync("VENDOR-001", "Dr. Smith", DateTime.UtcNow, "Check", null, null);

        FeeBatchPaymentState state = await batch.GetAsync();
        Assert.That(state.TotalAmount, Is.EqualTo(0m));
        Assert.That(state.IsPosted, Is.False);
    }

    [Test]
    public async Task AddInvoiceToBatch_IncrementsTotalAmount()
    {
        IFeeBatchPaymentGrain batch = NewBatch();
        await batch.CreateAsync("VENDOR-001", "Dr. Smith", DateTime.UtcNow, "Check", null, null);

        await batch.AddInvoiceAsync("FEE-INVOICE-A", "FEE-AUTH-A", "PAT-001", "Pat One", "VENDOR-001", "Dr. Smith", 150m);
        await batch.AddInvoiceAsync("FEE-INVOICE-B", "FEE-AUTH-B", "PAT-002", "Pat Two", "VENDOR-001", "Dr. Smith", 250m);

        FeeBatchPaymentState state = await batch.GetAsync();
        Assert.That(state.TotalAmount, Is.EqualTo(400m));
        Assert.That(state.InvoiceEntries, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PostBatch_SetsIsPostedTrue()
    {
        // Arrange — need real auth+invoice chain for PostAsync to call IFeeInvoiceGrain.PayAsync
        string authId = $"FEE-AUTH:{Guid.NewGuid()}";
        IFeeAuthorizationGrain auth = _cluster.GrainFactory.GetGrain<IFeeAuthorizationGrain>(authId);
        await auth.CreateAsync(
            "PAT-FEE-BATCH-001", "VENDOR-001", "Dr. Smith",
            FeeServiceType.Outpatient, DateTime.UtcNow, DateTime.UtcNow,
            null, 300m, "USR-001", "Supervisor", "Eval", null, null, null, null);

        string invoiceId = $"FEE-INVOICE:{Guid.NewGuid()}";
        IFeeInvoiceGrain invoice = _cluster.GrainFactory.GetGrain<IFeeInvoiceGrain>(invoiceId);
        await invoice.SubmitAsync(
            authId, "PAT-FEE-BATCH-001", "VENDOR-001", "Dr. Smith",
            "INV-BATCH-001", DateTime.UtcNow, "Outpatient",
            100m, null, new List<string>(), null, null);
        await invoice.ApproveAsync(100m, "USR-REVIEWER", "Reviewer");

        IFeeBatchPaymentGrain batch = NewBatch();
        await batch.CreateAsync("VENDOR-001", "Dr. Smith", DateTime.UtcNow, "Check", "CHK-BATCH-001", null);
        await batch.AddInvoiceAsync(invoiceId, authId, "PAT-FEE-BATCH-001", "Patient Name", "VENDOR-001", "Dr. Smith", 100m);

        // Act
        await batch.PostAsync("USR-FISCAL", "Fiscal Officer");

        FeeBatchPaymentState state = await batch.GetAsync();
        Assert.That(state.IsPosted, Is.True);
        Assert.That(state.PostedDate, Is.Not.Null);
    }

    // ─── Vendor Index ─────────────────────────────────────────────────────

    [Test]
    public async Task VendorIndex_AddOrUpdate_AppearsInGetAll()
    {
        IFeeVendorIndexGrain index = GetVendorIndex();

        string vendorId = $"FEE-VENDOR-{Guid.NewGuid()}";
        FeeVendorIndexEntry entry = new FeeVendorIndexEntry
        {
            VendorId = vendorId,
            VendorName = "Community Care Clinic",
            VendorType = nameof(FeeVendorType.Organization),
            IsActive = true
        };

        await index.AddOrUpdateAsync(entry);
        List<FeeVendorIndexEntry> all = await index.GetAllAsync();

        Assert.That(all.Any(v => v.VendorId == vendorId), Is.True);
    }

    [Test]
    public async Task VendorIndex_GetActive_ExcludesInactiveVendors()
    {
        IFeeVendorIndexGrain index = GetVendorIndex();

        string activeId = $"FEE-VENDOR-{Guid.NewGuid()}";
        string inactiveId = $"FEE-VENDOR-{Guid.NewGuid()}";

        await index.AddOrUpdateAsync(new FeeVendorIndexEntry { VendorId = activeId, VendorName = "Active Vendor", VendorType = nameof(FeeVendorType.Individual), IsActive = true });
        await index.AddOrUpdateAsync(new FeeVendorIndexEntry { VendorId = inactiveId, VendorName = "Inactive Vendor", VendorType = nameof(FeeVendorType.Individual), IsActive = false });

        List<FeeVendorIndexEntry> active = await index.GetActiveAsync();

        Assert.That(active.Any(v => v.VendorId == activeId), Is.True);
        Assert.That(active.Any(v => v.VendorId == inactiveId), Is.False);
    }
}
