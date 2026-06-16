// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Engineering module — VistA Files #6914 (Engineering) and #6920 (Work Orders).
/// Tests individual grains directly via TestCluster.
/// MUMPS routines: ENSITE.m, ENWORK.m, ENWLIS.m.
/// </summary>
[TestFixture]
public class EngineeringTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEngineeringWorkOrderGrain NewWorkOrderGrain()
        => _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderGrain>(
            $"ENG-WO:{Guid.NewGuid():N}");

    private IEngineeringWorkOrderIndexGrain WorkOrderIndex()
        => _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>("ENG-WO-IDX");

    private IFacilityGrain NewFacilityGrain()
        => _cluster.GrainFactory.GetGrain<IFacilityGrain>(
            $"ENG-FAC:{Guid.NewGuid():N}");

    private IFacilityIndexGrain FacilityIndex()
        => _cluster.GrainFactory.GetGrain<IFacilityIndexGrain>("ENG-FAC-IDX");

    // Helper: create a standard work order on a grain
    private static async Task<IEngineeringWorkOrderGrain> CreateStandardWorkOrder(
        IEngineeringWorkOrderGrain grain,
        WorkOrderType type = WorkOrderType.Repair,
        WorkOrderPriority priority = WorkOrderPriority.Routine,
        EngineeringShop shop = EngineeringShop.General)
    {
        await grain.CreateAsync(
            workOrderNumber: $"WO-2024-{Guid.NewGuid():N}"[..12],
            facilityId: "ENG-FAC:TEST-001",
            facilityName: "Building A — Room 201",
            locationDescription: "West corridor, 2nd floor",
            workOrderType: type,
            priority: priority,
            shop: shop,
            description: "Fix leaking pipe under sink",
            requestedById: "STAFF-001",
            requestedByName: "Jane Doe",
            estimatedHours: 2.0m,
            estimatedPartsCost: 50.00m,
            scheduledDate: DateTime.UtcNow.AddDays(2));
        return grain;
    }

    // ── EngineeringWorkOrderGrain tests ───────────────────────────────────────

    [Test]
    public async Task WorkOrderGrain_CanCreate()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        DateTime scheduled = DateTime.UtcNow.AddDays(3);

        // Act
        await grain.CreateAsync(
            workOrderNumber: "WO-2024-0001",
            facilityId: "ENG-FAC:BLDG-A",
            facilityName: "Building A",
            locationDescription: "Room 101",
            workOrderType: WorkOrderType.Repair,
            priority: WorkOrderPriority.Urgent,
            shop: EngineeringShop.Plumbing,
            description: "Water leak in ceiling",
            requestedById: "STAFF-001",
            requestedByName: "John Smith",
            estimatedHours: 3.0m,
            estimatedPartsCost: 150.00m,
            scheduledDate: scheduled);

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.WorkOrderNumber, Is.EqualTo("WO-2024-0001"));
        Assert.That(state.FacilityId, Is.EqualTo("ENG-FAC:BLDG-A"));
        Assert.That(state.FacilityName, Is.EqualTo("Building A"));
        Assert.That(state.LocationDescription, Is.EqualTo("Room 101"));
        Assert.That(state.WorkOrderType, Is.EqualTo(WorkOrderType.Repair));
        Assert.That(state.Priority, Is.EqualTo(WorkOrderPriority.Urgent));
        Assert.That(state.Shop, Is.EqualTo(EngineeringShop.Plumbing));
        Assert.That(state.Description, Is.EqualTo("Water leak in ceiling"));
        Assert.That(state.RequestedByName, Is.EqualTo("John Smith"));
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Open));
        Assert.That(state.EstimatedHours, Is.EqualTo(3.0m));
        Assert.That(state.EstimatedPartsCost, Is.EqualTo(150.00m));
        Assert.That(state.ScheduledDate, Is.EqualTo(scheduled));
        Assert.That(state.ActualHours, Is.EqualTo(0m));
        Assert.That(state.LaborEntries, Is.Empty);
        Assert.That(state.PartsEntries, Is.Empty);
        Assert.That(state.Notes, Is.Empty);
        Assert.That(state.CreatedDate, Is.Not.EqualTo(DateTime.MinValue));
    }

    [Test]
    public async Task WorkOrderGrain_CanAssign()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);

        // Act
        await grain.AssignAsync("TECH-001", "Bob Technician");

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.AssignedToId, Is.EqualTo("TECH-001"));
        Assert.That(state.AssignedToName, Is.EqualTo("Bob Technician"));
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Open)); // Assign does not change status
    }

    [Test]
    public async Task WorkOrderGrain_CanStart()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);

        // Act
        await grain.StartAsync("TECH-002", "Alice Engineer");

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.InProgress));
        Assert.That(state.AssignedToName, Is.EqualTo("Alice Engineer"));
        Assert.That(state.StartedDate, Is.Not.Null);
    }

    [Test]
    public async Task WorkOrderGrain_CanPlaceOnHoldAndResume()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);
        await grain.StartAsync("TECH-001", "Bob Technician");

        // Act — hold
        await grain.PlaceOnHoldAsync("Waiting for parts");

        // Assert — on hold
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.OnHold));
        Assert.That(state.HoldReason, Is.EqualTo("Waiting for parts"));

        // Act — resume
        await grain.ResumeAsync();

        // Assert — back to in progress
        state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.InProgress));
        Assert.That(state.HoldReason, Is.Null);
    }

    [Test]
    public async Task WorkOrderGrain_CanComplete()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);
        await grain.StartAsync("TECH-001", "Bob Technician");

        // Act
        DateTime completedAt = DateTime.UtcNow;
        await grain.CompleteAsync(completedAt);

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
    }

    [Test]
    public async Task WorkOrderGrain_CanCancel()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);

        // Act
        await grain.CancelAsync("MGMT-001", "Manager Jones", "Resolved without repair");

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Cancelled));
        Assert.That(state.CancelledById, Is.EqualTo("MGMT-001"));
        Assert.That(state.CancelledByName, Is.EqualTo("Manager Jones"));
        Assert.That(state.CancellationReason, Is.EqualTo("Resolved without repair"));
        Assert.That(state.CancelledDate, Is.Not.Null);
    }

    [Test]
    public async Task WorkOrderGrain_CanAddMultipleLaborEntries_AccumulatesHours()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);
        await grain.StartAsync("TECH-001", "Bob Technician");

        // Act — two separate labor entries
        DateTime day1 = DateTime.UtcNow.AddDays(-1);
        DateTime day2 = DateTime.UtcNow;
        await grain.AddLaborAsync("TECH-001", "Bob Technician", 2.5m, day1, "Initial assessment and diagnosis");
        await grain.AddLaborAsync("TECH-001", "Bob Technician", 1.5m, day2, "Completed repair and test");

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.LaborEntries, Has.Count.EqualTo(2));
        Assert.That(state.ActualHours, Is.EqualTo(4.0m));

        WoLaborEntry entry1 = state.LaborEntries.First(e => e.HoursWorked == 2.5m);
        Assert.That(entry1.TechnicianName, Is.EqualTo("Bob Technician"));
        Assert.That(entry1.Notes, Is.EqualTo("Initial assessment and diagnosis"));
    }

    [Test]
    public async Task WorkOrderGrain_CanAddParts()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain, shop: EngineeringShop.Plumbing);

        // Act
        await grain.AddPartAsync("PRT-VALVE-001", "Ball valve 1/2 inch", 2, 12.50m);
        await grain.AddPartAsync("PRT-PIPE-002", "PVC pipe 1/2 inch 10ft", 1, 8.75m);

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.PartsEntries, Has.Count.EqualTo(2));

        WoPartEntry valve = state.PartsEntries.First(p => p.PartNumber == "PRT-VALVE-001");
        Assert.That(valve.Quantity, Is.EqualTo(2));
        Assert.That(valve.UnitCost, Is.EqualTo(12.50m));

        WoPartEntry pipe = state.PartsEntries.First(p => p.PartNumber == "PRT-PIPE-002");
        Assert.That(pipe.PartDescription, Is.EqualTo("PVC pipe 1/2 inch 10ft"));
    }

    [Test]
    public async Task WorkOrderGrain_CanAddNotes()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);

        // Act
        await grain.AddNoteAsync("TECH-001", "Bob Technician", "Arrived on site, assessed the leak");
        await grain.AddNoteAsync("MGMT-001", "Supervisor Williams", "Approved additional parts order");

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Notes, Has.Count.EqualTo(2));

        WoNoteEntry note1 = state.Notes.First(n => n.AuthorId == "TECH-001");
        Assert.That(note1.NoteText, Is.EqualTo("Arrived on site, assessed the leak"));
        Assert.That(note1.EnteredDate, Is.Not.EqualTo(DateTime.MinValue));
    }

    [Test]
    public async Task WorkOrderGrain_CanUpdatePriority()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain, priority: WorkOrderPriority.Routine);

        // Act — escalate to emergency
        await grain.UpdatePriorityAsync(WorkOrderPriority.Emergency);

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.Priority, Is.EqualTo(WorkOrderPriority.Emergency));
    }

    [Test]
    public async Task WorkOrderGrain_CanUpdateScheduledDate()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);
        DateTime originalDate = (await grain.GetWorkOrderAsync()).ScheduledDate!.Value;

        // Act — reschedule
        DateTime newDate = originalDate.AddDays(7);
        await grain.UpdateScheduledDateAsync(newDate);

        // Assert
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.ScheduledDate, Is.EqualTo(newDate));
    }

    [Test]
    public async Task WorkOrderGrain_LaborHoursRecalculatedOnAdd()
    {
        // Arrange
        IEngineeringWorkOrderGrain grain = NewWorkOrderGrain();
        await CreateStandardWorkOrder(grain);

        // Act — add 3 labor entries
        await grain.AddLaborAsync("TECH-A", "Alice", 1.0m, DateTime.UtcNow, null);
        await grain.AddLaborAsync("TECH-B", "Bob", 2.5m, DateTime.UtcNow, null);
        await grain.AddLaborAsync("TECH-A", "Alice", 0.5m, DateTime.UtcNow, null);

        // Assert — actual hours = sum of all entries
        EngineeringWorkOrderState state = await grain.GetWorkOrderAsync();
        Assert.That(state.ActualHours, Is.EqualTo(4.0m));
        Assert.That(state.LaborEntries, Has.Count.EqualTo(3));
    }

    // ── WorkOrderIndexGrain tests ─────────────────────────────────────────────

    [Test]
    public async Task WorkOrderIndexGrain_CanAddAndSearch()
    {
        // Arrange — isolated index key to prevent cross-test pollution
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-TEST-{Guid.NewGuid():N}");

        WorkOrderIndexEntry entry1 = new()
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-2024-0001",
            FacilityId = "ENG-FAC:BLDG-A",
            FacilityName = "Building A",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Urgent,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.Plumbing,
            RequestedByName = "Jane Doe",
            CreatedDate = DateTime.UtcNow.AddHours(-2),
        };
        WorkOrderIndexEntry entry2 = new()
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-2024-0002",
            FacilityId = "ENG-FAC:BLDG-B",
            FacilityName = "Building B",
            WorkOrderType = WorkOrderType.PreventiveMaintenance,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.InProgress,
            Shop = EngineeringShop.Hvac,
            AssignedToName = "Bob Tech",
            RequestedByName = "John Smith",
            CreatedDate = DateTime.UtcNow.AddHours(-1),
        };

        await index.AddOrUpdateAsync(entry1);
        await index.AddOrUpdateAsync(entry2);

        // Act — search by shop
        List<WorkOrderIndexEntry> plumbingResults =
            await index.SearchAsync("ENG-FAC:BLDG-A", null, null, null, null, null, null, null, 50);
        List<WorkOrderIndexEntry> hvacResults =
            await index.SearchAsync(null, EngineeringShop.Hvac, null, null, null, null, null, null, 50);

        // Assert
        Assert.That(plumbingResults, Has.Count.EqualTo(1));
        Assert.That(plumbingResults[0].WorkOrderNumber, Is.EqualTo("WO-2024-0001"));

        Assert.That(hvacResults, Has.Count.EqualTo(1));
        Assert.That(hvacResults[0].Shop, Is.EqualTo(EngineeringShop.Hvac));
    }

    [Test]
    public async Task WorkOrderIndexGrain_CanFilterByStatus()
    {
        // Arrange
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-STATUS-{Guid.NewGuid():N}");

        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-A",
            FacilityId = "F-1",
            FacilityName = "Facility 1",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff A",
            CreatedDate = DateTime.UtcNow,
        });
        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-B",
            FacilityId = "F-1",
            FacilityName = "Facility 1",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Completed,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff B",
            CreatedDate = DateTime.UtcNow.AddDays(-1),
        });

        // Act
        List<WorkOrderIndexEntry> openOnly =
            await index.SearchAsync(null, null, WorkOrderStatus.Open, null, null, null, null, null, 50);
        List<WorkOrderIndexEntry> completedOnly =
            await index.SearchAsync(null, null, WorkOrderStatus.Completed, null, null, null, null, null, 50);

        // Assert
        Assert.That(openOnly, Has.Count.EqualTo(1));
        Assert.That(openOnly[0].Status, Is.EqualTo(WorkOrderStatus.Open));

        Assert.That(completedOnly, Has.Count.EqualTo(1));
        Assert.That(completedOnly[0].Status, Is.EqualTo(WorkOrderStatus.Completed));
    }

    [Test]
    public async Task WorkOrderIndexGrain_CanFilterByPriority()
    {
        // Arrange
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-PRI-{Guid.NewGuid():N}");

        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-EMG",
            FacilityId = "F-1",
            FacilityName = "F1",
            WorkOrderType = WorkOrderType.Emergency,
            Priority = WorkOrderPriority.Emergency,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.Electrical,
            RequestedByName = "Staff",
            CreatedDate = DateTime.UtcNow,
        });
        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-RTN",
            FacilityId = "F-2",
            FacilityName = "F2",
            WorkOrderType = WorkOrderType.PreventiveMaintenance,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.Mechanical,
            RequestedByName = "Staff",
            CreatedDate = DateTime.UtcNow,
        });

        // Act
        List<WorkOrderIndexEntry> emergencyResults =
            await index.SearchAsync(null, null, null, WorkOrderPriority.Emergency, null, null, null, null, 50);

        // Assert
        Assert.That(emergencyResults, Has.Count.EqualTo(1));
        Assert.That(emergencyResults[0].Priority, Is.EqualTo(WorkOrderPriority.Emergency));
    }

    [Test]
    public async Task WorkOrderIndexGrain_GetActive_ReturnsOnlyOpenAndInProgress()
    {
        // Arrange
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-ACT-{Guid.NewGuid():N}");

        foreach (WorkOrderStatus status in new[] { WorkOrderStatus.Open, WorkOrderStatus.InProgress, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled })
        {
            await index.AddOrUpdateAsync(new WorkOrderIndexEntry
            {
                WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
                WorkOrderNumber = $"WO-{status}",
                FacilityId = "F-1",
                FacilityName = "F1",
                WorkOrderType = WorkOrderType.Repair,
                Priority = WorkOrderPriority.Routine,
                Status = status,
                Shop = EngineeringShop.General,
                RequestedByName = "Staff",
                CreatedDate = DateTime.UtcNow,
            });
        }

        // Act
        List<WorkOrderIndexEntry> active = await index.GetActiveAsync(50);

        // Assert — only Open and InProgress
        Assert.That(active.All(e => e.Status == WorkOrderStatus.Open || e.Status == WorkOrderStatus.InProgress), Is.True);
        Assert.That(active, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task WorkOrderIndexGrain_AddOrUpdate_ReplacesExisting()
    {
        // Arrange
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-UPD-{Guid.NewGuid():N}");

        string workOrderId = $"ENG-WO:{Guid.NewGuid():N}";

        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = workOrderId,
            WorkOrderNumber = "WO-UPD-001",
            FacilityId = "F-1",
            FacilityName = "F1",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff",
            CreatedDate = DateTime.UtcNow,
        });

        // Act — update same WO to InProgress with assignment
        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = workOrderId,
            WorkOrderNumber = "WO-UPD-001",
            FacilityId = "F-1",
            FacilityName = "F1",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Urgent,
            Status = WorkOrderStatus.InProgress,
            Shop = EngineeringShop.General,
            AssignedToName = "Bob Tech",
            RequestedByName = "Staff",
            CreatedDate = DateTime.UtcNow,
        });

        // Assert — only one entry, updated
        List<WorkOrderIndexEntry> results =
            await index.SearchAsync(null, null, null, null, null, null, null, null, 50);
        WorkOrderIndexEntry updated = results.Single(e => e.WorkOrderId == workOrderId);
        Assert.That(updated.Status, Is.EqualTo(WorkOrderStatus.InProgress));
        Assert.That(updated.Priority, Is.EqualTo(WorkOrderPriority.Urgent));
        Assert.That(updated.AssignedToName, Is.EqualTo("Bob Tech"));
    }

    [Test]
    public async Task WorkOrderIndexGrain_GetByFacility_FiltersCorrectly()
    {
        // Arrange
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-FAC-{Guid.NewGuid():N}");

        string facilityA = $"ENG-FAC:{Guid.NewGuid():N}";
        string facilityB = $"ENG-FAC:{Guid.NewGuid():N}";

        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-FA-001",
            FacilityId = facilityA,
            FacilityName = "Facility A",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff",
            CreatedDate = DateTime.UtcNow,
        });
        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-FB-001",
            FacilityId = facilityB,
            FacilityName = "Facility B",
            WorkOrderType = WorkOrderType.Inspection,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff",
            CreatedDate = DateTime.UtcNow,
        });

        // Act
        List<WorkOrderIndexEntry> aResults = await index.GetByFacilityAsync(facilityA, 50);
        List<WorkOrderIndexEntry> bResults = await index.GetByFacilityAsync(facilityB, 50);

        // Assert
        Assert.That(aResults, Has.Count.EqualTo(1));
        Assert.That(aResults[0].FacilityName, Is.EqualTo("Facility A"));

        Assert.That(bResults, Has.Count.EqualTo(1));
        Assert.That(bResults[0].FacilityName, Is.EqualTo("Facility B"));
    }

    // ── FacilityGrain tests ───────────────────────────────────────────────────

    [Test]
    public async Task FacilityGrain_CanCreate()
    {
        // Arrange
        IFacilityGrain grain = NewFacilityGrain();

        // Act
        await grain.UpsertAsync(
            facilityName: "Building A — MRI Suite",
            category: FacilityCategory.Equipment,
            building: "Building A",
            floor: "1",
            room: "1B-MRI",
            departmentId: "DEPT-RADIOLOGY",
            departmentName: "Radiology",
            equipmentType: "MRI Scanner",
            serialNumber: "SN-MRI-12345",
            model: "Siemens MAGNETOM",
            manufacturer: "Siemens Healthineers",
            installationDate: new DateTime(2020, 3, 15),
            warrantyExpirationDate: new DateTime(2025, 3, 15),
            description: "3T MRI scanner for diagnostic imaging");

        // Assert
        FacilityState state = await grain.GetFacilityAsync();
        Assert.That(state.FacilityName, Is.EqualTo("Building A — MRI Suite"));
        Assert.That(state.Category, Is.EqualTo(FacilityCategory.Equipment));
        Assert.That(state.Building, Is.EqualTo("Building A"));
        Assert.That(state.Floor, Is.EqualTo("1"));
        Assert.That(state.Room, Is.EqualTo("1B-MRI"));
        Assert.That(state.DepartmentName, Is.EqualTo("Radiology"));
        Assert.That(state.EquipmentType, Is.EqualTo("MRI Scanner"));
        Assert.That(state.SerialNumber, Is.EqualTo("SN-MRI-12345"));
        Assert.That(state.Manufacturer, Is.EqualTo("Siemens Healthineers"));
        Assert.That(state.Status, Is.EqualTo(FacilityStatus.Active));
        Assert.That(state.WorkOrderCount, Is.EqualTo(0));
    }

    [Test]
    public async Task FacilityGrain_CanUpdateViaUpsert()
    {
        // Arrange
        IFacilityGrain grain = NewFacilityGrain();
        await grain.UpsertAsync("Old Name", FacilityCategory.Room, null, null, null,
            null, null, null, null, null, null, null, null, null);

        // Act
        await grain.UpsertAsync("New Name", FacilityCategory.Equipment, "Bldg C", "3",
            "301", null, "Nursing", "Patient Lift", "SN-001", "Model X", "Acme",
            null, null, "Updated description");

        // Assert
        FacilityState state = await grain.GetFacilityAsync();
        Assert.That(state.FacilityName, Is.EqualTo("New Name"));
        Assert.That(state.Category, Is.EqualTo(FacilityCategory.Equipment));
        Assert.That(state.Building, Is.EqualTo("Bldg C"));
        Assert.That(state.DepartmentName, Is.EqualTo("Nursing"));
        Assert.That(state.EquipmentType, Is.EqualTo("Patient Lift"));
    }

    [Test]
    public async Task FacilityGrain_CanIncrementWorkOrderCount()
    {
        // Arrange
        IFacilityGrain grain = NewFacilityGrain();
        await grain.UpsertAsync("Test Facility", FacilityCategory.Room, null, null, null,
            null, null, null, null, null, null, null, null, null);

        // Act
        await grain.IncrementWorkOrderCountAsync();
        await grain.IncrementWorkOrderCountAsync();
        await grain.IncrementWorkOrderCountAsync();

        // Assert
        FacilityState state = await grain.GetFacilityAsync();
        Assert.That(state.WorkOrderCount, Is.EqualTo(3));
    }

    [Test]
    public async Task FacilityGrain_CanCycleStatuses()
    {
        // Arrange
        IFacilityGrain grain = NewFacilityGrain();
        await grain.UpsertAsync("HVAC System", FacilityCategory.System, "All Buildings",
            null, null, null, "Facilities Management", null, null, null, null, null, null, "Main HVAC");

        // Assert initial status
        Assert.That((await grain.GetFacilityAsync()).Status, Is.EqualTo(FacilityStatus.Active));

        // Act — set under maintenance
        await grain.SetUnderMaintenanceAsync();
        Assert.That((await grain.GetFacilityAsync()).Status, Is.EqualTo(FacilityStatus.UnderMaintenance));

        // Act — restore active
        await grain.SetActiveAsync();
        Assert.That((await grain.GetFacilityAsync()).Status, Is.EqualTo(FacilityStatus.Active));

        // Act — decommission
        await grain.DecommissionAsync();
        Assert.That((await grain.GetFacilityAsync()).Status, Is.EqualTo(FacilityStatus.Decommissioned));
    }

    // ── FacilityIndexGrain tests ──────────────────────────────────────────────

    [Test]
    public async Task FacilityIndexGrain_CanSearchByName()
    {
        // Arrange — isolated index key
        IFacilityIndexGrain index =
            _cluster.GrainFactory.GetGrain<IFacilityIndexGrain>(
                $"ENG-FAC-IDX-TEST-{Guid.NewGuid():N}");

        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = "ENG-FAC:001",
            FacilityName = "Building A — Operating Room 1",
            Category = FacilityCategory.Room,
            Building = "Building A",
            Floor = "2",
            Status = FacilityStatus.Active,
        });
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = "ENG-FAC:002",
            FacilityName = "Building B — MRI Scanner",
            Category = FacilityCategory.Equipment,
            Building = "Building B",
            Floor = "1",
            DepartmentName = "Radiology",
            Status = FacilityStatus.Active,
        });
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = "ENG-FAC:003",
            FacilityName = "Old Boiler",
            Category = FacilityCategory.Utility,
            Building = "Building C",
            Status = FacilityStatus.Decommissioned,
        });

        // Act — search by name
        List<FacilityIndexEntry> buildingAResults = await index.SearchAsync("Building A", null, true, 50);
        List<FacilityIndexEntry> allActive = await index.SearchAsync(null, null, true, 50);
        List<FacilityIndexEntry> allIncDecommissioned = await index.SearchAsync(null, null, false, 50);
        List<FacilityIndexEntry> equipOnly = await index.SearchAsync(null, FacilityCategory.Equipment, true, 50);

        // Assert
        Assert.That(buildingAResults, Has.Count.EqualTo(1));
        Assert.That(buildingAResults[0].FacilityId, Is.EqualTo("ENG-FAC:001"));

        Assert.That(allActive, Has.Count.EqualTo(2));
        Assert.That(allIncDecommissioned, Has.Count.EqualTo(3));

        Assert.That(equipOnly, Has.Count.EqualTo(1));
        Assert.That(equipOnly[0].Category, Is.EqualTo(FacilityCategory.Equipment));
    }

    [Test]
    public async Task FacilityIndexGrain_AddOrUpdate_ReplacesExisting()
    {
        // Arrange
        IFacilityIndexGrain index =
            _cluster.GrainFactory.GetGrain<IFacilityIndexGrain>(
                $"ENG-FAC-IDX-UPD-{Guid.NewGuid():N}");

        string facilityId = $"ENG-FAC:{Guid.NewGuid():N}";

        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = facilityId,
            FacilityName = "Old Facility Name",
            Category = FacilityCategory.Room,
            Status = FacilityStatus.Active,
            WorkOrderCount = 0,
        });

        // Act — update
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = facilityId,
            FacilityName = "New Facility Name",
            Category = FacilityCategory.Room,
            Status = FacilityStatus.UnderMaintenance,
            WorkOrderCount = 3,
        });

        // Assert — only one entry, updated
        List<FacilityIndexEntry> all = await index.GetAllAsync();
        FacilityIndexEntry updated = all.Single(f => f.FacilityId == facilityId);
        Assert.That(updated.FacilityName, Is.EqualTo("New Facility Name"));
        Assert.That(updated.Status, Is.EqualTo(FacilityStatus.UnderMaintenance));
        Assert.That(updated.WorkOrderCount, Is.EqualTo(3));
    }

    [Test]
    public async Task FacilityIndexGrain_SearchByDepartment()
    {
        // Arrange
        IFacilityIndexGrain index =
            _cluster.GrainFactory.GetGrain<IFacilityIndexGrain>(
                $"ENG-FAC-IDX-DEPT-{Guid.NewGuid():N}");

        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = $"ENG-FAC:{Guid.NewGuid():N}",
            FacilityName = "Radiology MRI",
            Category = FacilityCategory.Equipment,
            DepartmentName = "Radiology",
            Status = FacilityStatus.Active,
        });
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = $"ENG-FAC:{Guid.NewGuid():N}",
            FacilityName = "Radiology CT Scanner",
            Category = FacilityCategory.Equipment,
            DepartmentName = "Radiology",
            Status = FacilityStatus.Active,
        });
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = $"ENG-FAC:{Guid.NewGuid():N}",
            FacilityName = "ICU Ventilator",
            Category = FacilityCategory.Equipment,
            DepartmentName = "Intensive Care",
            Status = FacilityStatus.Active,
        });

        // Act
        List<FacilityIndexEntry> radiologyResults = await index.SearchAsync("Radiology", null, true, 50);

        // Assert — matches department name
        Assert.That(radiologyResults, Has.Count.EqualTo(2));
        Assert.That(radiologyResults.All(f => f.DepartmentName == "Radiology"), Is.True);
    }

    [Test]
    public async Task FacilityIndexGrain_GetAll_ReturnsAlphabetically()
    {
        // Arrange
        IFacilityIndexGrain index =
            _cluster.GrainFactory.GetGrain<IFacilityIndexGrain>(
                $"ENG-FAC-IDX-SORT-{Guid.NewGuid():N}");

        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = "F-C",
            FacilityName = "Zebra Room",
            Category = FacilityCategory.Room,
            Status = FacilityStatus.Active,
        });
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = "F-A",
            FacilityName = "Alpha Building",
            Category = FacilityCategory.Building,
            Status = FacilityStatus.Active,
        });
        await index.AddOrUpdateAsync(new FacilityIndexEntry
        {
            FacilityId = "F-B",
            FacilityName = "Mango Suite",
            Category = FacilityCategory.Room,
            Status = FacilityStatus.Active,
        });

        // Act
        List<FacilityIndexEntry> all = await index.GetAllAsync();

        // Assert — alphabetical by FacilityName
        Assert.That(all[0].FacilityName, Is.EqualTo("Alpha Building"));
        Assert.That(all[1].FacilityName, Is.EqualTo("Mango Suite"));
        Assert.That(all[2].FacilityName, Is.EqualTo("Zebra Room"));
    }

    // ── Full workflow integration tests ───────────────────────────────────────

    [Test]
    public async Task FullWorkflow_CreateWorkOrder_AssignStartCompleteWithLabor()
    {
        // Arrange — create a facility first
        IFacilityGrain facility = NewFacilityGrain();
        string facilityId = facility.GetPrimaryKeyString();
        await facility.UpsertAsync("ED Treatment Room 3", FacilityCategory.Room,
            "Emergency Building", "1", "TR-3", null, "Emergency Department",
            null, null, null, null, null, null, "ED treatment room");
        await facility.IncrementWorkOrderCountAsync();

        // Create work order
        IEngineeringWorkOrderGrain workOrder = NewWorkOrderGrain();
        await workOrder.CreateAsync(
            workOrderNumber: "WO-2024-9999",
            facilityId: facilityId,
            facilityName: "ED Treatment Room 3",
            locationDescription: "Emergency Building, Room TR-3",
            workOrderType: WorkOrderType.Repair,
            priority: WorkOrderPriority.Urgent,
            shop: EngineeringShop.Electrical,
            description: "Emergency light fixture not working",
            requestedById: "STAFF-ED-001",
            requestedByName: "Nurse Williams",
            estimatedHours: 1.5m,
            estimatedPartsCost: 35.00m,
            scheduledDate: DateTime.UtcNow.AddHours(4));

        // Assert — Open
        EngineeringWorkOrderState state = await workOrder.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Open));

        // Assign and start
        await workOrder.AssignAsync("TECH-ELEC-001", "Mike Electrician");
        await workOrder.StartAsync("TECH-ELEC-001", "Mike Electrician");
        state = await workOrder.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.InProgress));
        Assert.That(state.StartedDate, Is.Not.Null);

        // Add note
        await workOrder.AddNoteAsync("TECH-ELEC-001", "Mike Electrician", "Replaced ballast; light works");

        // Log labor
        await workOrder.AddLaborAsync("TECH-ELEC-001", "Mike Electrician", 1.25m, DateTime.UtcNow, "Fixture replaced");

        // Add part
        await workOrder.AddPartAsync("BULB-T8-4FT", "T8 Fluorescent Bulb 4ft", 2, 8.50m);

        // Complete
        await workOrder.CompleteAsync(DateTime.UtcNow);

        // Assert final state
        state = await workOrder.GetWorkOrderAsync();
        Assert.That(state.Status, Is.EqualTo(WorkOrderStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
        Assert.That(state.ActualHours, Is.EqualTo(1.25m));
        Assert.That(state.Notes, Has.Count.EqualTo(1));
        Assert.That(state.PartsEntries, Has.Count.EqualTo(1));
        Assert.That(state.PartsEntries[0].PartNumber, Is.EqualTo("BULB-T8-4FT"));

        // Verify facility work order count
        FacilityState facilityState = await facility.GetFacilityAsync();
        Assert.That(facilityState.WorkOrderCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WorkOrderIndex_DateRange_FiltersCorrectly()
    {
        // Arrange
        IEngineeringWorkOrderIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(
                $"ENG-WO-IDX-DR-{Guid.NewGuid():N}");

        DateTime twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        DateTime yesterday = DateTime.UtcNow.AddDays(-1);
        DateTime today = DateTime.UtcNow;

        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-OLD",
            FacilityId = "F-1",
            FacilityName = "F1",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Completed,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff",
            CreatedDate = twoDaysAgo,
        });
        await index.AddOrUpdateAsync(new WorkOrderIndexEntry
        {
            WorkOrderId = $"ENG-WO:{Guid.NewGuid():N}",
            WorkOrderNumber = "WO-NEW",
            FacilityId = "F-1",
            FacilityName = "F1",
            WorkOrderType = WorkOrderType.Repair,
            Priority = WorkOrderPriority.Routine,
            Status = WorkOrderStatus.Open,
            Shop = EngineeringShop.General,
            RequestedByName = "Staff",
            CreatedDate = today,
        });

        // Act — search from yesterday to tomorrow
        List<WorkOrderIndexEntry> recentResults =
            await index.SearchAsync(null, null, null, null, null, null, yesterday, today.AddDays(1), 50);

        // Assert — only today's work order
        Assert.That(recentResults, Has.Count.EqualTo(1));
        Assert.That(recentResults[0].WorkOrderNumber, Is.EqualTo("WO-NEW"));
    }
}
