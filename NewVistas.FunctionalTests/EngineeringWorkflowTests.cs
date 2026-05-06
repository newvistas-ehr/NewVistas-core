// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Engineering Service — VistA File #6920.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class EngineeringWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEngineeringWorkOrderGrain GetWorkOrder(string id)
        => _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderGrain>(id);

    private IEngineeringWorkOrderIndexGrain GetIndex()
        => _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>("ENG-WO-IDX");

    // ── Work Order Tests ─────────────────────────────────────────────────────

    [Test]
    public async Task CreateWorkOrder_SetsOpenStatus()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0001", "FAC-001", "VA Medical Center Bldg 1",
            "Room 302, 3rd Floor", WorkOrderType.Repair,
            WorkOrderPriority.Routine, EngineeringShop.Plumbing,
            "Leaking faucet in patient room",
            "REQ-001", "Dr. Johnson",
            estimatedHours: 2m, estimatedPartsCost: 50m,
            scheduledDate: DateTime.UtcNow.AddDays(3));

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();

        Assert.That(state.WorkOrderNumber, Is.EqualTo("WO-2024-0001"));
        Assert.That(state.FacilityName, Is.EqualTo("VA Medical Center Bldg 1"));
        Assert.That(state.WorkOrderType, Is.EqualTo(WorkOrderType.Repair));
        Assert.That(state.Priority, Is.EqualTo(WorkOrderPriority.Routine));
        Assert.That(state.Shop, Is.EqualTo(EngineeringShop.Plumbing));
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Open));
        Assert.That(state.Description, Does.Contain("Leaking faucet"));
    }

    [Test]
    public async Task AssignWorkOrder_SetsAssignedFields()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0002", "FAC-001", "VA Medical Center",
            "Lobby", WorkOrderType.PreventiveMaintenance,
            WorkOrderPriority.Routine, EngineeringShop.Hvac,
            "Quarterly HVAC filter change",
            "REQ-002", "Facility Manager",
            estimatedHours: 4m, estimatedPartsCost: 200m,
            scheduledDate: DateTime.UtcNow.AddDays(7));

        await grain.AssignAsync("TECH-001", "Bob Wilson");

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.AssignedToId, Is.EqualTo("TECH-001"));
        Assert.That(state.AssignedToName, Is.EqualTo("Bob Wilson"));
    }

    [Test]
    public async Task StartWorkOrder_TransitionsToInProgress()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0003", "FAC-001", "VA Medical Center",
            "OR Suite 2", WorkOrderType.Emergency,
            WorkOrderPriority.Emergency, EngineeringShop.Electrical,
            "Power outlet sparking",
            "REQ-003", "Charge Nurse",
            null, null, null);

        await grain.StartAsync("TECH-002", "Mike Electrician");

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.InProgress));
        Assert.That(state.StartedDate, Is.Not.Null);
    }

    [Test]
    public async Task CompleteWorkOrder_SetsCompletedStatus()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0004", "FAC-001", "VA Medical Center",
            "Canteen", WorkOrderType.Repair,
            WorkOrderPriority.Routine, EngineeringShop.General,
            "Replace broken table",
            "REQ-004", "Canteen Manager",
            1m, 75m, null);

        await grain.StartAsync("TECH-003", "Jim Fix");
        await grain.CompleteAsync(DateTime.UtcNow);

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
    }

    [Test]
    public async Task CancelWorkOrder_SetsStatusAndReason()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0005", "FAC-001", "VA Medical Center",
            "Room 100", WorkOrderType.NewInstall,
            WorkOrderPriority.Routine, EngineeringShop.Carpentry,
            "Install new cabinets",
            "REQ-005", "Admin Officer",
            null, null, null);

        await grain.CancelAsync("ADMIN-001", "Admin Smith", "Project funding withdrawn");

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Cancelled));
        Assert.That(state.CancellationReason, Does.Contain("funding withdrawn"));
        Assert.That(state.CancelledByName, Is.EqualTo("Admin Smith"));
    }

    [Test]
    public async Task PlaceOnHoldAndResume_TransitionsCorrectly()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0006", "FAC-001", "VA Medical Center",
            "Parking Garage", WorkOrderType.Repair,
            WorkOrderPriority.Urgent, EngineeringShop.Mechanical,
            "Elevator motor replacement",
            "REQ-006", "Safety Officer",
            16m, 5000m, null);

        await grain.StartAsync("TECH-004", "Tom Mechanic");
        await grain.PlaceOnHoldAsync("Waiting for motor to arrive from supplier");

        EngineeringWorkOrderState stateHeld = await grain.GetWorkOrderAsync();
        Assert.That(stateHeld.Status, Is.EqualTo(WorkOrderStatus.OnHold));
        Assert.That(stateHeld.HoldReason, Does.Contain("motor to arrive"));

        await grain.ResumeAsync();
        EngineeringWorkOrderState stateResumed = await grain.GetWorkOrderAsync();
        Assert.That(stateResumed.Status, Is.EqualTo(WorkOrderStatus.InProgress));
    }

    [Test]
    public async Task AddLabor_UpdatesActualHours()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0007", "FAC-001", "VA Medical Center",
            "Bldg 2 Roof", WorkOrderType.Inspection,
            WorkOrderPriority.Routine, EngineeringShop.General,
            "Roof inspection for leaks",
            "REQ-007", "Facilities Dir",
            4m, null, null);

        await grain.StartAsync("TECH-005", "Ray Roofer");
        await grain.AddLaborAsync("TECH-005", "Ray Roofer", 2.5m, DateTime.UtcNow.AddDays(-1), "Inspected east side");
        await grain.AddLaborAsync("TECH-005", "Ray Roofer", 1.5m, DateTime.UtcNow, "Completed west side");

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.LaborEntries, Has.Count.EqualTo(2));
        Assert.That(state.ActualHours, Is.EqualTo(4.0m));
    }

    [Test]
    public async Task AddPart_RecordsPartsUsed()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0008", "FAC-001", "VA Medical Center",
            "Room 150", WorkOrderType.Repair,
            WorkOrderPriority.Routine, EngineeringShop.Plumbing,
            "Replace toilet valve",
            "REQ-008", "Ward Clerk",
            1m, 30m, null);

        await grain.AddPartAsync("PLB-VALVE-001", "Toilet fill valve assembly", 1, 24.99m);

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.PartsEntries, Has.Count.EqualTo(1));
        Assert.That(state.PartsEntries[0].PartDescription, Does.Contain("fill valve"));
        Assert.That(state.PartsEntries[0].UnitCost, Is.EqualTo(24.99m));
    }

    [Test]
    public async Task UpdatePriority_ChangesLevel()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0009", "FAC-001", "VA Medical Center",
            "Clinic A", WorkOrderType.Repair,
            WorkOrderPriority.Routine, EngineeringShop.Electrical,
            "Flickering lights",
            "REQ-009", "Clinic Nurse",
            null, null, null);

        await grain.UpdatePriorityAsync(WorkOrderPriority.Urgent);

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Priority, Is.EqualTo(WorkOrderPriority.Urgent));
    }

    [Test]
    public async Task AddNote_AppendsProgressNote()
    {
        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        IEngineeringWorkOrderGrain grain = GetWorkOrder(woId);

        await grain.CreateAsync(
            "WO-2024-0010", "FAC-001", "VA Medical Center",
            "Grounds", WorkOrderType.Alteration,
            WorkOrderPriority.Routine, EngineeringShop.Grounds,
            "Repave walkway near entrance",
            "REQ-010", "Safety Officer",
            null, null, null);

        await grain.AddNoteAsync("SUPER-001", "Supervisor Brown", "Concrete ordered, delivery expected Friday");

        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Notes, Has.Count.EqualTo(1));
        Assert.That(state.Notes[0].NoteText, Does.Contain("Concrete ordered"));
    }

    // ── Index Tests ──────────────────────────────────────────────────────────

    [Test]
    public async Task WorkOrderIndex_SearchByFacilityAndStatus()
    {
        IEngineeringWorkOrderIndexGrain index = GetIndex();

        string woId = $"ENG-WO-{Guid.NewGuid():N}";
        string facilityId = $"FAC-{Guid.NewGuid():N}";

        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = woId, WorkOrderNumber = "WO-IDX-001",
            FacilityId = facilityId, FacilityName = "Test Facility",
            WorkOrderType = WorkOrderType.Repair, Priority = WorkOrderPriority.Urgent,
            Status = WorkOrderStatus.InProgress, Shop = EngineeringShop.Plumbing,
            RequestedByName = "Test User", CreatedDate = DateTime.UtcNow
        });

        List<WorkOrderIndexEntry> results = await index.SearchAsync(
            facilityId, null, WorkOrderStatus.InProgress, null, null, null, null, null, 100);
        Assert.That(results.Any(w => w.WorkOrderId == woId), Is.True);
    }
}
