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
public class CmopWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Suspense Queue ──────────────────────────────────────────────────────

    [Test]
    public async Task SuspenseGrain_CanAddToQueue()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:SITE-{Guid.NewGuid():N}");
        await grain.AddToSuspenseAsync(new CmopSuspenseEntry
        {
            PrescriptionId = "RX-001", PatientId = "PAT-001", PatientName = "DOE,JOHN",
            DrugName = "LISINOPRIL 10MG", Quantity = 90, DaysSupply = 90, QueuedDate = DateTime.UtcNow
        });

        List<CmopSuspenseEntry> queue = await grain.GetQueuedPrescriptionsAsync();
        Assert.That(queue, Has.Count.EqualTo(1));
        Assert.That(queue[0].DrugName, Is.EqualTo("LISINOPRIL 10MG"));
    }

    [Test]
    public async Task SuspenseGrain_PreventsDuplicates()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:SITE-{Guid.NewGuid():N}");
        var entry = new CmopSuspenseEntry
        {
            PrescriptionId = "RX-DUP", PatientId = "PAT-001", PatientName = "DOE,JOHN",
            DrugName = "ASPIRIN 81MG", QueuedDate = DateTime.UtcNow
        };
        await grain.AddToSuspenseAsync(entry);
        await grain.AddToSuspenseAsync(entry);

        int count = await grain.GetQueueCountAsync();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task SuspenseGrain_CanRemoveFromQueue()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:SITE-{Guid.NewGuid():N}");
        await grain.AddToSuspenseAsync(new CmopSuspenseEntry
        {
            PrescriptionId = "RX-REM", PatientId = "PAT-001", PatientName = "DOE,JOHN",
            DrugName = "METFORMIN 500MG", QueuedDate = DateTime.UtcNow
        });
        await grain.RemoveFromSuspenseAsync("RX-REM");

        int count = await grain.GetQueueCountAsync();
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task SuspenseGrain_CanSetAutoTransmit()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:SITE-{Guid.NewGuid():N}");
        await grain.SetAutoTransmitAsync(false, 8);

        CmopSuspenseState state = await grain.GetSuspenseAsync();
        Assert.That(state.AutoTransmitEnabled, Is.False);
        Assert.That(state.AutoTransmitHour, Is.EqualTo(8));
    }

    [Test]
    public async Task SuspenseGrain_CanClearQueue()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:SITE-{Guid.NewGuid():N}");
        await grain.AddToSuspenseAsync(new CmopSuspenseEntry { PrescriptionId = "RX-C1", PatientId = "P", PatientName = "N", DrugName = "D1", QueuedDate = DateTime.UtcNow });
        await grain.AddToSuspenseAsync(new CmopSuspenseEntry { PrescriptionId = "RX-C2", PatientId = "P", PatientName = "N", DrugName = "D2", QueuedDate = DateTime.UtcNow });
        await grain.ClearQueueAsync();

        Assert.That(await grain.GetQueueCountAsync(), Is.EqualTo(0));
    }

    // ─── Transmission ────────────────────────────────────────────────────────

    [Test]
    public async Task TransmissionGrain_CanCreate()
    {
        string txId = $"TX-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await grain.CreateTransmissionAsync("CMOP-LEAV", "CMOP LEAVENWORTH", "SITE-500",
            new List<CmopPrescriptionEntry>
            {
                new() { PrescriptionId = "RX-T1", PatientId = "P1", PatientName = "DOE,JOHN", DrugName = "DRUG A" },
                new() { PrescriptionId = "RX-T2", PatientId = "P2", PatientName = "DOE,JANE", DrugName = "DRUG B" }
            });

        CmopTransmissionState tx = await grain.GetTransmissionAsync();
        Assert.That(tx.Status, Is.EqualTo("PENDING"));
        Assert.That(tx.PrescriptionCount, Is.EqualTo(2));
        Assert.That(tx.CmopFacilityName, Is.EqualTo("CMOP LEAVENWORTH"));
    }

    [Test]
    public async Task TransmissionGrain_CanTransmit()
    {
        string txId = $"TX-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await grain.CreateTransmissionAsync("CMOP-LEAV", "CMOP LEAVENWORTH", "SITE-500", new List<CmopPrescriptionEntry>());
        await grain.TransmitAsync();

        CmopTransmissionState tx = await grain.GetTransmissionAsync();
        Assert.That(tx.Status, Is.EqualTo("TRANSMITTED"));
        Assert.That(tx.TransmittedDate, Is.Not.Null);
    }

    [Test]
    public async Task TransmissionGrain_CanRecordDispensed()
    {
        string txId = $"TX-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await grain.CreateTransmissionAsync("CMOP-DAL", "CMOP DALLAS", "SITE-500", new List<CmopPrescriptionEntry>());
        await grain.TransmitAsync();
        await grain.AcknowledgeReceiptAsync();
        await grain.RecordDispensedAsync(8, 2, "NDC mismatch on 2 items");

        CmopTransmissionState tx = await grain.GetTransmissionAsync();
        Assert.That(tx.Status, Is.EqualTo("DISPENSED"));
        Assert.That(tx.DispensedCount, Is.EqualTo(8));
        Assert.That(tx.RejectedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task TransmissionGrain_CanRecordShipped()
    {
        string txId = $"TX-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await grain.CreateTransmissionAsync("CMOP-LEAV", "CMOP LEAVENWORTH", "SITE-500", new List<CmopPrescriptionEntry>());
        await grain.TransmitAsync();
        await grain.AcknowledgeReceiptAsync();
        await grain.RecordDispensedAsync(5, 0, null);
        await grain.RecordShippedAsync("USPS123456789", "USPS");

        CmopTransmissionState tx = await grain.GetTransmissionAsync();
        Assert.That(tx.Status, Is.EqualTo("SHIPPED"));
        Assert.That(tx.TrackingNumber, Is.EqualTo("USPS123456789"));
        Assert.That(tx.ShippingCarrier, Is.EqualTo("USPS"));
    }

    [Test]
    public async Task TransmissionGrain_CanCancel()
    {
        string txId = $"TX-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await grain.CreateTransmissionAsync("CMOP-LEAV", "CMOP LEAVENWORTH", "SITE-500", new List<CmopPrescriptionEntry>());
        await grain.CancelAsync("Patient deceased");

        CmopTransmissionState tx = await grain.GetTransmissionAsync();
        Assert.That(tx.Status, Is.EqualTo("CANCELLED"));
        Assert.That(tx.ErrorMessage, Is.EqualTo("Patient deceased"));
    }

    [Test]
    public async Task TransmissionGrain_CanRejectItem()
    {
        string txId = $"TX-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await grain.CreateTransmissionAsync("CMOP-LEAV", "CMOP LEAVENWORTH", "SITE-500",
            new List<CmopPrescriptionEntry>
            {
                new() { PrescriptionId = "RX-REJ", PatientId = "P", PatientName = "N", DrugName = "D", ItemStatus = "PENDING" }
            });
        await grain.RejectItemAsync("RX-REJ", "Drug not in stock");

        CmopTransmissionState tx = await grain.GetTransmissionAsync();
        Assert.That(tx.Prescriptions[0].ItemStatus, Is.EqualTo("REJECTED"));
        Assert.That(tx.Prescriptions[0].RejectReason, Is.EqualTo("Drug not in stock"));
        Assert.That(tx.RejectedCount, Is.EqualTo(1));
    }

    // ─── Transmission Index ──────────────────────────────────────────────────

    [Test]
    public async Task TransmissionIndex_CanAddAndRetrieve()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:SITE-{Guid.NewGuid():N}");
        await grain.AddOrUpdateAsync(new CmopTransmissionSummary { TransmissionId = "TX-IDX-1", CmopFacilityName = "CMOP LEAV", Status = "TRANSMITTED", PrescriptionCount = 5 });
        await grain.AddOrUpdateAsync(new CmopTransmissionSummary { TransmissionId = "TX-IDX-2", CmopFacilityName = "CMOP DALLAS", Status = "COMPLETED", PrescriptionCount = 3 });

        List<CmopTransmissionSummary> all = await grain.GetTransmissionsAsync();
        Assert.That(all, Has.Count.EqualTo(2));

        List<CmopTransmissionSummary> transmitted = await grain.GetTransmissionsByStatusAsync("TRANSMITTED");
        Assert.That(transmitted, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TransmissionIndex_CanUpdate()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:SITE-{Guid.NewGuid():N}");
        await grain.AddOrUpdateAsync(new CmopTransmissionSummary { TransmissionId = "TX-UPD", Status = "TRANSMITTED", PrescriptionCount = 5 });
        await grain.AddOrUpdateAsync(new CmopTransmissionSummary { TransmissionId = "TX-UPD", Status = "COMPLETED", PrescriptionCount = 5, DispensedCount = 5 });

        List<CmopTransmissionSummary> all = await grain.GetTransmissionsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo("COMPLETED"));
    }

    // ─── Full Workflow: Suspense → Transmit ──────────────────────────────────

    [Test]
    public async Task SuspenseGrain_TransmitQueueCreatesTransmission()
    {
        string siteId = $"SITE-{Guid.NewGuid():N}";
        var suspense = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");

        await suspense.AddToSuspenseAsync(new CmopSuspenseEntry { PrescriptionId = "RX-W1", PatientId = "P1", PatientName = "DOE,JOHN", DrugName = "DRUG A", Quantity = 90, QueuedDate = DateTime.UtcNow });
        await suspense.AddToSuspenseAsync(new CmopSuspenseEntry { PrescriptionId = "RX-W2", PatientId = "P2", PatientName = "DOE,JANE", DrugName = "DRUG B", Quantity = 60, QueuedDate = DateTime.UtcNow });

        string txId = await suspense.TransmitQueueAsync("CMOP-LEAV", "CMOP LEAVENWORTH");

        // Queue should be cleared
        Assert.That(await suspense.GetQueueCountAsync(), Is.EqualTo(0));

        // Transmission should exist with TRANSMITTED status
        var txGrain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        CmopTransmissionState tx = await txGrain.GetTransmissionAsync();
        Assert.That(tx.Status, Is.EqualTo("TRANSMITTED"));
        Assert.That(tx.PrescriptionCount, Is.EqualTo(2));
        Assert.That(tx.TransmittedDate, Is.Not.Null);

        // Index should have the transmission
        var index = _cluster.GrainFactory.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:{siteId}");
        List<CmopTransmissionSummary> list = await index.GetTransmissionsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].TransmissionId, Is.EqualTo(txId));
    }

    [Test]
    public async Task SuspenseGrain_TransmitEmptyQueueThrows()
    {
        var grain = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:SITE-{Guid.NewGuid():N}");
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await grain.TransmitQueueAsync("CMOP-LEAV", "CMOP LEAVENWORTH"));
    }

    // ─── Full Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_SuspenseToCompleted()
    {
        string siteId = $"SITE-{Guid.NewGuid():N}";
        var suspense = _cluster.GrainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");

        // Queue prescriptions
        await suspense.AddToSuspenseAsync(new CmopSuspenseEntry { PrescriptionId = "RX-LC1", PatientId = "P1", PatientName = "SMITH,A", DrugName = "MED A", Quantity = 90, QueuedDate = DateTime.UtcNow });

        // Transmit
        string txId = await suspense.TransmitQueueAsync("CMOP-LEAV", "CMOP LEAVENWORTH");
        var txGrain = _cluster.GrainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");

        // CMOP acknowledges receipt
        await txGrain.AcknowledgeReceiptAsync();
        Assert.That((await txGrain.GetTransmissionAsync()).Status, Is.EqualTo("RECEIVED"));

        // CMOP dispenses
        await txGrain.RecordDispensedAsync(1, 0, null);
        Assert.That((await txGrain.GetTransmissionAsync()).Status, Is.EqualTo("DISPENSED"));

        // CMOP ships
        await txGrain.RecordShippedAsync("USPS999", "USPS");
        Assert.That((await txGrain.GetTransmissionAsync()).Status, Is.EqualTo("SHIPPED"));

        // Mark complete
        await txGrain.CompleteAsync();
        CmopTransmissionState final = await txGrain.GetTransmissionAsync();
        Assert.That(final.Status, Is.EqualTo("COMPLETED"));
        Assert.That(final.TrackingNumber, Is.EqualTo("USPS999"));
    }
}
