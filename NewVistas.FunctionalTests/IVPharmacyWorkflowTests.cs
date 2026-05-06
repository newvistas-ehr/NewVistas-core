// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for IV Pharmacy — VistA Files #50.8, #53.4.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class IVPharmacyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Helper ─────────────────────────────────────────────────────────────────

    private Task<string> CreateOrder(IPatientWorkflowGrain wf)
        => wf.CreateIVAdmixOrderAsync(
            baseSolution: "Normal Saline 0.9%",
            baseSolutionVolumeMl: 1000,
            route: IVAdmixRoute.Peripheral,
            frequency: IVAdmixFrequency.Continuous,
            containerType: IVContainerType.Bag,
            containerCount: 1,
            priority: IVAdmixPriority.Routine,
            linkedInpatientOrderId: null,
            infusionRateStr: "125 mL/hr",
            infusionRateMlHr: 125m,
            infusionDurationHours: 8m,
            routeDescription: null,
            frequencyDescription: null,
            startDateTime: DateTime.UtcNow,
            stopDateTime: DateTime.UtcNow.AddHours(8),
            providerId: "PROV-001",
            providerName: "Dr. Adams",
            notes: "Patient NPO — maintenance fluids.");

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateIVAdmixOrder_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);

        Assert.That(orderId, Is.Not.Null.And.Not.Empty);

        List<IVAdmixOrderIndexEntry> all = await wf.GetIVAdmixOrdersAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].OrderId, Is.EqualTo(orderId));
        Assert.That(all[0].BaseSolution, Is.EqualTo("Normal Saline 0.9%"));
        Assert.That(all[0].Status, Is.EqualTo(IVAdmixOrderStatus.Pending));
        Assert.That(all[0].Priority, Is.EqualTo(IVAdmixPriority.Routine));
    }

    [Test]
    public async Task GetIVAdmixOrder_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.BaseSolution, Is.EqualTo("Normal Saline 0.9%"));
        Assert.That(state.BaseSolutionVolumeMl, Is.EqualTo(1000));
        Assert.That(state.Route, Is.EqualTo(IVAdmixRoute.Peripheral));
        Assert.That(state.Frequency, Is.EqualTo(IVAdmixFrequency.Continuous));
        Assert.That(state.InfusionRateMlHr, Is.EqualTo(125m));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Adams"));
    }

    [Test]
    public async Task AddIVAdmixAdditive_IncreasesAdditiveList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);

        IVAdmixAdditive additive = new IVAdmixAdditive
        {
            DrugName = "Potassium Chloride",
            DrugId = "KCL-001",
            Dose = "20",
            DoseUnit = "mEq",
            IsBaseSolution = false
        };

        await wf.AddIVAdmixAdditiveAsync(orderId, additive);

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Additives, Has.Count.EqualTo(1));
        Assert.That(state.Additives[0].DrugName, Is.EqualTo("Potassium Chloride"));
        Assert.That(state.Additives[0].DoseUnit, Is.EqualTo("mEq"));
    }

    [Test]
    public async Task RemoveIVAdmixAdditive_RemovesByDrugName()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);

        await wf.AddIVAdmixAdditiveAsync(orderId, new IVAdmixAdditive
        {
            DrugName = "Potassium Chloride",
            Dose = "20",
            DoseUnit = "mEq"
        });
        await wf.AddIVAdmixAdditiveAsync(orderId, new IVAdmixAdditive
        {
            DrugName = "Magnesium Sulfate",
            Dose = "2",
            DoseUnit = "g"
        });

        await wf.RemoveIVAdmixAdditiveAsync(orderId, "Potassium Chloride");

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Additives, Has.Count.EqualTo(1));
        Assert.That(state.Additives[0].DrugName, Is.EqualTo("Magnesium Sulfate"));
    }

    [Test]
    public async Task VerifyIVAdmixOrder_SetsVerifiedStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);
        DateTime verifiedDate = DateTime.UtcNow;

        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-001", "Dr. Patel, PharmD", verifiedDate);

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Verified));
        Assert.That(state.PharmacistId, Is.EqualTo("RPH-001"));
        Assert.That(state.PharmacistName, Is.EqualTo("Dr. Patel, PharmD"));
        Assert.That(state.VerifiedDate, Is.Not.Null);
    }

    [Test]
    public async Task StartAndCompleteCompounding_TransitionsToReady()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);

        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-001", "Dr. Patel", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(orderId, "TECH-001", "Kim Lee, CPhT", DateTime.UtcNow);

        IVAdmixOrderState compounding = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(compounding.Status, Is.EqualTo(IVAdmixOrderStatus.Compounding));
        Assert.That(compounding.CompoundedByName, Is.EqualTo("Kim Lee, CPhT"));

        DateTime expirationDate = DateTime.UtcNow.AddHours(24);
        await wf.CompleteIVAdmixCompoundingAsync(orderId, DateTime.UtcNow, "LOT-2025-001", expirationDate);

        IVAdmixOrderState ready = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(ready.Status, Is.EqualTo(IVAdmixOrderStatus.Ready));
        Assert.That(ready.LotNumber, Is.EqualTo("LOT-2025-001"));
        Assert.That(ready.ExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task PrintIVAdmixLabel_RecordsPrintDetails()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);
        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-001", "Dr. Patel", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(orderId, "TECH-001", "Kim Lee", DateTime.UtcNow);
        await wf.CompleteIVAdmixCompoundingAsync(orderId, DateTime.UtcNow, null, null);

        DateTime printedDate = DateTime.UtcNow;
        await wf.PrintIVAdmixLabelAsync(orderId, "Kim Lee, CPhT", printedDate);

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.LabelPrinted, Is.True);
        Assert.That(state.LabelPrintedBy, Is.EqualTo("Kim Lee, CPhT"));
        Assert.That(state.LabelPrintedDate, Is.Not.Null);
    }

    [Test]
    public async Task DispenseIVAdmixOrder_SetsDispensedStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);
        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-001", "Dr. Patel", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(orderId, "TECH-001", "Kim Lee", DateTime.UtcNow);
        await wf.CompleteIVAdmixCompoundingAsync(orderId, DateTime.UtcNow, null, null);

        DateTime dispensedDate = DateTime.UtcNow;
        await wf.DispenseIVAdmixOrderAsync(orderId, dispensedDate);

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Dispensed));
        Assert.That(state.DispensingDateTime, Is.Not.Null);
    }

    [Test]
    public async Task RecordIVAdmixAdministration_SetsAdministeredStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);
        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-001", "Dr. Patel", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(orderId, "TECH-001", "Kim Lee", DateTime.UtcNow);
        await wf.CompleteIVAdmixCompoundingAsync(orderId, DateTime.UtcNow, null, null);
        await wf.DispenseIVAdmixOrderAsync(orderId, DateTime.UtcNow);

        DateTime adminDate = DateTime.UtcNow;
        await wf.RecordIVAdmixAdministrationAsync(orderId, adminDate);

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Administered));
        Assert.That(state.AdministrationDateTime, Is.Not.Null);
    }

    [Test]
    public async Task CancelIVAdmixOrder_SetsCancelledStatusWithReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);

        await wf.CancelIVAdmixOrderAsync(orderId, "Order entered in error");

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Order entered in error"));

        List<IVAdmixOrderIndexEntry> index = await wf.GetIVAdmixOrdersAsync();
        Assert.That(index[0].Status, Is.EqualTo(IVAdmixOrderStatus.Cancelled));
    }

    [Test]
    public async Task DiscontinueIVAdmixOrder_SetsDiscontinuedStatusWithReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string orderId = await CreateOrder(wf);
        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-001", "Dr. Patel", DateTime.UtcNow);

        await wf.DiscontinueIVAdmixOrderAsync(orderId, "Patient discharged — no longer needed");

        IVAdmixOrderState state = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(state.Status, Is.EqualTo(IVAdmixOrderStatus.Discontinued));
        Assert.That(state.DiscontinuationReason, Does.Contain("discharged"));
    }

    [Test]
    public async Task GetPendingIVAdmixOrders_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string order1 = await CreateOrder(wf);
        string order2 = await CreateOrder(wf);

        // Verify order2 so it's Verified, not Pending
        await wf.VerifyIVAdmixOrderAsync(order2, "RPH-002", "Dr. Lee", DateTime.UtcNow);

        List<IVAdmixOrderIndexEntry> pending = await wf.GetPendingIVAdmixOrdersAsync();
        // Both Pending and Verified are returned by GetPendingIVAdmixOrdersAsync
        Assert.That(pending, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetActiveIVAdmixOrders_ReturnsCompoundingAndReady()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string order1 = await CreateOrder(wf);
        string order2 = await CreateOrder(wf);

        // Move order1 to Compounding
        await wf.VerifyIVAdmixOrderAsync(order1, "RPH-001", "Dr. Patel", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(order1, "TECH-001", "Kim Lee", DateTime.UtcNow);

        // Move order2 to Ready
        await wf.VerifyIVAdmixOrderAsync(order2, "RPH-002", "Dr. Lee", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(order2, "TECH-002", "Sam Park", DateTime.UtcNow);
        await wf.CompleteIVAdmixCompoundingAsync(order2, DateTime.UtcNow, null, null);

        List<IVAdmixOrderIndexEntry> active = await wf.GetActiveIVAdmixOrdersAsync();
        Assert.That(active, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FullLifecycle_PendingThroughAdministered()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Create
        string orderId = await wf.CreateIVAdmixOrderAsync(
            "D5W", 500,
            IVAdmixRoute.Central, IVAdmixFrequency.Q8H,
            IVContainerType.Bag, 3,
            IVAdmixPriority.ASAP,
            "ORD-12345", "Over 30 min", null, 0.5m,
            "Central line — R subclavian", "Every 8 hours",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(3),
            "PROV-002", "Dr. Nguyen",
            "Vancomycin piggyback");

        // Add additive
        await wf.AddIVAdmixAdditiveAsync(orderId, new IVAdmixAdditive
        {
            DrugName = "Vancomycin",
            DrugId = "VANC-001",
            Dose = "1000",
            DoseUnit = "mg",
            IsBaseSolution = false
        });

        // Verify
        await wf.VerifyIVAdmixOrderAsync(orderId, "RPH-003", "Dr. Kim, PharmD", DateTime.UtcNow);

        // Compound
        await wf.StartIVAdmixCompoundingAsync(orderId, "TECH-003", "Jane Smith", DateTime.UtcNow);
        await wf.CompleteIVAdmixCompoundingAsync(orderId, DateTime.UtcNow, "LOT-VANC-001", DateTime.UtcNow.AddHours(12));

        // Print label
        await wf.PrintIVAdmixLabelAsync(orderId, "Jane Smith", DateTime.UtcNow);

        // Dispense
        await wf.DispenseIVAdmixOrderAsync(orderId, DateTime.UtcNow);

        // Administer
        await wf.RecordIVAdmixAdministrationAsync(orderId, DateTime.UtcNow);

        // Verify final state
        IVAdmixOrderState final_ = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(final_.Status, Is.EqualTo(IVAdmixOrderStatus.Administered));
        Assert.That(final_.BaseSolution, Is.EqualTo("D5W"));
        Assert.That(final_.Priority, Is.EqualTo(IVAdmixPriority.ASAP));
        Assert.That(final_.Route, Is.EqualTo(IVAdmixRoute.Central));
        Assert.That(final_.Additives, Has.Count.EqualTo(1));
        Assert.That(final_.Additives[0].DrugName, Is.EqualTo("Vancomycin"));
        Assert.That(final_.LabelPrinted, Is.True);
        Assert.That(final_.LotNumber, Is.EqualTo("LOT-VANC-001"));
    }

    [Test]
    public async Task MultiplePatients_IndependentOrders()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await CreateOrder(wf1);
        await CreateOrder(wf2);
        await CreateOrder(wf2);

        List<IVAdmixOrderIndexEntry> p1Orders = await wf1.GetIVAdmixOrdersAsync();
        List<IVAdmixOrderIndexEntry> p2Orders = await wf2.GetIVAdmixOrdersAsync();

        Assert.That(p1Orders, Has.Count.EqualTo(1));
        Assert.That(p2Orders, Has.Count.EqualTo(2));
    }
}
