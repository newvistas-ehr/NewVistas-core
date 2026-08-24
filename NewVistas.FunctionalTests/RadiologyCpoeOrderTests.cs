// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Radiology ordered through CPOE must behave like every other order: it appears in the
/// patient's order list, and filing the report closes it out. The bulk-import path must
/// keep creating no order, so a 500-patient import does not flood the order list.
///
/// This is the same contract the lab side gained in beta.14 — radiology had the identical
/// gap, where an ordered study was invisible to the Orders tab because nothing wrote to
/// the order index.
/// </summary>
[TestFixture]
public class RadiologyCpoeOrderTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Wf(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private static string NewPatient() => $"RADCPOE-{Guid.NewGuid()}";

    [Test]
    public async Task PlaceRadiologyOrder_AppearsInTheOrderList()
    {
        string pid = NewPatient();

        await Wf(pid).PlaceRadiologyOrderAsync(
            "XR DXA Bone Density, Hip and Spine", null, "77080", "DXA",
            "PROV-1", "Dr. Ordering", "ROUTINE",
            "Osteoporosis surveillance", "Osteoporosis monitoring", null, "MAIN");

        List<OrderSummary> orders = await Wf(pid).GetOrdersByFilterAsync(2);

        Assert.That(orders, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(orders[0].OrderType, Is.EqualTo("Radiology"));
            Assert.That(orders[0].OrderText, Does.Contain("DXA"));
        });
    }

    [Test]
    public async Task PlaceRadiologyOrder_LinksTheStudyBackToTheOrder()
    {
        string pid = NewPatient();

        string radId = await Wf(pid).PlaceRadiologyOrderAsync(
            "CT Chest with Contrast", null, "71260", "CT",
            "PROV-1", "Dr. Ordering", "ROUTINE", null, null, null, null);

        RadiologyState study = await Wf(pid).GetRadiologyStudyAsync(radId);

        Assert.That(study.OrderId, Is.Not.Null.And.Not.Empty,
            "a CPOE study must carry the id of the order it fulfils");
    }

    [Test]
    public async Task FilingTheReport_CompletesTheLinkedOrder()
    {
        string pid = NewPatient();

        string radId = await Wf(pid).PlaceRadiologyOrderAsync(
            "US Abdomen Complete", null, "76700", "ULTRASOUND",
            "PROV-1", "Dr. Ordering", "ROUTINE", null, null, null, null);

        // Filter 2 = Current (active) view, filter 4 = Completed/Expired — the same lenses
        // LabWorkflowTests uses to prove the lab side of this contract.
        List<OrderSummary> current = await Wf(pid).GetOrdersByFilterAsync(2);
        Assert.That(current, Has.Count.EqualTo(1), "a placed study should sit in the active order view");
        string orderId = current[0].OrderId;

        await Wf(pid).CompleteRadiologyAsync(radId, "No acute findings.", "Normal study.", "PROV-2", "Dr. Reader");

        Assert.Multiple(async () =>
        {
            Assert.That(await Wf(pid).GetOrdersByFilterAsync(2), Is.Empty,
                "a read study should leave the active order view");
            Assert.That((await Wf(pid).GetOrdersByFilterAsync(4)).Any(o => o.OrderId == orderId), Is.True,
                "and appear under Completed/Expired");
        });
    }

    [Test]
    public async Task BulkImportPath_StillCreatesNoOrder()
    {
        string pid = NewPatient();

        // OrderRadiologyStudyAsync is the historical/bulk path. It must stay order-free so
        // importers and seeded history do not flood the order list.
        await Wf(pid).OrderRadiologyStudyAsync(
            "XR Chest PA/LAT", null, "71046", "GENERAL RADIOLOGY",
            "PROV-1", "Dr. Importer", "ROUTINE", null, null, null, null, null);

        List<OrderSummary> orders = await Wf(pid).GetOrdersByFilterAsync(2);

        Assert.That(orders, Is.Empty, "the bulk import path must not create a CPOE order");
        Assert.That((await Wf(pid).GetRadiologyStudiesAsync(10)), Has.Count.EqualTo(1),
            "but the study itself must still be recorded");
    }

    [Test]
    public async Task CompletingAnImportedStudy_DoesNotThrow()
    {
        string pid = NewPatient();

        // An imported study carries no OrderId; completing it must simply skip the order step.
        string radId = await Wf(pid).OrderRadiologyStudyAsync(
            "MR Brain", null, "70551", "MRI",
            "PROV-1", "Dr. Importer", "ROUTINE", null, null, null, null, null);

        Assert.DoesNotThrowAsync(async () =>
            await Wf(pid).CompleteRadiologyAsync(radId, "Report text.", "Impression.", "PROV-2", "Dr. Reader"));
    }
}
