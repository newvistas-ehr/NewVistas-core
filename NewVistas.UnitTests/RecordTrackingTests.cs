// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

file class ChartGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("rtChartStore");
    }
}

file class ChartIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("rtChartIndexStore");
    }
}

file class ChartRequestGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("rtRequestStore");
    }
}

file class ChartRequestIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("rtRequestIndexStore");
    }
}

file class RTIntegrationSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("rtChartStore");
        siloBuilder.AddMemoryGrainStorage("rtChartIndexStore");
        siloBuilder.AddMemoryGrainStorage("rtRequestStore");
        siloBuilder.AddMemoryGrainStorage("rtRequestIndexStore");
    }
}

[TestFixture]
public class ChartGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ChartGrain_CanInitializeChart()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");

        await grain.InitializeChartAsync(patientId, "Smith, John", "CN-001", "File Room A");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("Smith, John"));
        Assert.That(state.ChartNumber, Is.EqualTo("CN-001"));
        Assert.That(state.CurrentLocation, Is.EqualTo("File Room A"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.FileRoom));
        Assert.That(state.IsCheckedOut, Is.False);
        Assert.That(state.IsLost, Is.False);
        Assert.That(state.Volumes, Has.Count.EqualTo(1));
        Assert.That(state.Volumes[0].VolumeNumber, Is.EqualTo(1));
        Assert.That(state.Volumes[0].IsActive, Is.True);
        Assert.That(state.MovementHistory, Has.Count.EqualTo(1));
        Assert.That(state.MovementHistory[0].Action, Is.EqualTo(ChartMovementAction.Initialized));
    }

    [Test]
    public async Task ChartGrain_CanCheckOutChart()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "Jones, Mary", "CN-002", "File Room B");

        DateTime expectedReturn = DateTime.UtcNow.AddDays(3);
        await grain.CheckOutChartAsync("DR-001", "Dr. Evans", "Cardiology Clinic",
            ChartLocationType.ClinicOutpatient, expectedReturn, "Clerk1");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsCheckedOut, Is.True);
        Assert.That(state.CurrentBorrowerId, Is.EqualTo("DR-001"));
        Assert.That(state.CurrentBorrowerName, Is.EqualTo("Dr. Evans"));
        Assert.That(state.CurrentLocation, Is.EqualTo("Cardiology Clinic"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.ClinicOutpatient));
        Assert.That(state.CheckOutDate, Is.Not.Null);
        Assert.That(state.MovementHistory, Has.Count.EqualTo(2));
        Assert.That(state.MovementHistory[1].Action, Is.EqualTo(ChartMovementAction.CheckedOut));
    }

    [Test]
    public async Task ChartGrain_CanCheckInChart()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "Brown, Lisa", "CN-003", "File Room A");
        await grain.CheckOutChartAsync("DR-002", "Dr. Patel", "Ortho Ward",
            ChartLocationType.InpatientWard, null, "Clerk1");

        await grain.CheckInChartAsync("Clerk2");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsCheckedOut, Is.False);
        Assert.That(state.CurrentLocation, Is.EqualTo("File Room A"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.FileRoom));
        Assert.That(state.CurrentBorrowerId, Is.EqualTo(string.Empty));
        Assert.That(state.CheckOutDate, Is.Null);
        Assert.That(state.MovementHistory.Last().Action, Is.EqualTo(ChartMovementAction.CheckedIn));
    }

    [Test]
    public async Task ChartGrain_CanTransferChart()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "Davis, Tom", "CN-004", "File Room A");
        await grain.CheckOutChartAsync("DR-003", "Dr. Kim", "Surgery",
            ChartLocationType.InpatientWard, null, "Clerk1");

        await grain.TransferChartAsync("Radiology", ChartLocationType.Radiology, "TECH-01", "Tech Adams", "Clerk2");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.CurrentLocation, Is.EqualTo("Radiology"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.Radiology));
        Assert.That(state.CurrentBorrowerName, Is.EqualTo("Tech Adams"));
        Assert.That(state.MovementHistory.Last().Action, Is.EqualTo(ChartMovementAction.Transferred));
    }

    [Test]
    public async Task ChartGrain_CanAddVolume()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "White, Anna", "CN-005", "File Room B");

        await grain.AddVolumeAsync(2, "01/2020 - present");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.Volumes, Has.Count.EqualTo(2));
        Assert.That(state.Volumes[0].IsActive, Is.False);
        Assert.That(state.Volumes[1].IsActive, Is.True);
        Assert.That(state.Volumes[1].VolumeNumber, Is.EqualTo(2));
        Assert.That(state.MovementHistory.Last().Action, Is.EqualTo(ChartMovementAction.VolumeAdded));
    }

    [Test]
    public async Task ChartGrain_CanMarkChartLost()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "Green, Sam", "CN-006", "File Room A");

        await grain.MarkChartLostAsync("Last seen in surgery, now missing", "Supervisor");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsLost, Is.True);
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.Lost));
        Assert.That(state.CurrentLocation, Is.EqualTo("Unknown"));
        Assert.That(state.MovementHistory.Last().Action, Is.EqualTo(ChartMovementAction.Lost));
        Assert.That(state.MovementHistory.Last().Notes, Does.Contain("missing"));
    }

    [Test]
    public async Task ChartGrain_CanMarkChartFound()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "Clark, Beth", "CN-007", "File Room A");
        await grain.MarkChartLostAsync("Misplaced", "Staff");

        await grain.MarkChartFoundAsync("Radiology Storage", ChartLocationType.Radiology, "Clerk1");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsLost, Is.False);
        Assert.That(state.CurrentLocation, Is.EqualTo("Radiology Storage"));
        Assert.That(state.MovementHistory.Last().Action, Is.EqualTo(ChartMovementAction.Found));
    }

    [Test]
    public async Task ChartGrain_MovementHistoryTracksAllActions()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IChartGrain grain = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        await grain.InitializeChartAsync(patientId, "Hall, Eric", "CN-008", "File Room A");
        await grain.CheckOutChartAsync("DR-004", "Dr. Lee", "Neurology",
            ChartLocationType.ClinicOutpatient, null, "Clerk1");
        await grain.CheckInChartAsync("Clerk2");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.MovementHistory, Has.Count.EqualTo(3));
        Assert.That(state.MovementHistory[0].Action, Is.EqualTo(ChartMovementAction.Initialized));
        Assert.That(state.MovementHistory[1].Action, Is.EqualTo(ChartMovementAction.CheckedOut));
        Assert.That(state.MovementHistory[2].Action, Is.EqualTo(ChartMovementAction.CheckedIn));
    }
}

[TestFixture]
public class ChartIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ChartIndexGrain_CanUpsertAndRetrieve()
    {
        IChartIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-CHART-IDX-T1");

        ChartIndexEntry entry = new()
        {
            PatientId = "PAT-IDX-001",
            PatientName = "Adams, Joe",
            ChartNumber = "C-001",
            CurrentLocation = "File Room",
            CurrentLocationType = ChartLocationType.FileRoom,
            IsCheckedOut = false,
            VolumeCount = 1
        };
        await grain.UpsertChartAsync(entry);

        List<ChartIndexEntry> all = await grain.GetAllChartsAsync();
        Assert.That(all.Any(c => c.PatientId == "PAT-IDX-001"), Is.True);
    }

    [Test]
    public async Task ChartIndexGrain_FilterCheckedOut()
    {
        IChartIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-CHART-IDX-T2");

        await grain.UpsertChartAsync(new ChartIndexEntry { PatientId = "PAT-CO-1", PatientName = "A", IsCheckedOut = true, CurrentBorrowerName = "Dr. X" });
        await grain.UpsertChartAsync(new ChartIndexEntry { PatientId = "PAT-CO-2", PatientName = "B", IsCheckedOut = false });

        List<ChartIndexEntry> checkedOut = await grain.GetCheckedOutChartsAsync();
        Assert.That(checkedOut.All(c => c.IsCheckedOut), Is.True);
        Assert.That(checkedOut.Any(c => c.PatientId == "PAT-CO-1"), Is.True);
        Assert.That(checkedOut.Any(c => c.PatientId == "PAT-CO-2"), Is.False);
    }

    [Test]
    public async Task ChartIndexGrain_FilterLost()
    {
        IChartIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-CHART-IDX-T3");

        await grain.UpsertChartAsync(new ChartIndexEntry { PatientId = "PAT-LOST-1", PatientName = "X", IsLost = true });
        await grain.UpsertChartAsync(new ChartIndexEntry { PatientId = "PAT-LOST-2", PatientName = "Y", IsLost = false });

        List<ChartIndexEntry> lost = await grain.GetLostChartsAsync();
        Assert.That(lost.All(c => c.IsLost), Is.True);
        Assert.That(lost.Any(c => c.PatientId == "PAT-LOST-1"), Is.True);
    }

    [Test]
    public async Task ChartIndexGrain_FilterOverdue()
    {
        IChartIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-CHART-IDX-T4");

        await grain.UpsertChartAsync(new ChartIndexEntry
        {
            PatientId = "PAT-OD-1",
            PatientName = "Overdue",
            IsCheckedOut = true,
            ExpectedReturnDate = DateTime.UtcNow.AddDays(-5)
        });
        await grain.UpsertChartAsync(new ChartIndexEntry
        {
            PatientId = "PAT-OD-2",
            PatientName = "OnTime",
            IsCheckedOut = true,
            ExpectedReturnDate = DateTime.UtcNow.AddDays(5)
        });

        List<ChartIndexEntry> overdue = await grain.GetOverdueChartsAsync();
        Assert.That(overdue.Any(c => c.PatientId == "PAT-OD-1"), Is.True);
        Assert.That(overdue.Any(c => c.PatientId == "PAT-OD-2"), Is.False);
    }
}

[TestFixture]
public class ChartRequestGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ChartRequestGrain_CanCreateRequest()
    {
        string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
        IChartRequestGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestGrain>(requestId);

        await grain.CreateRequestAsync("PAT-001", "Wilson, Ray", "STAFF-01", "Clerk Adams",
            DateTime.UtcNow.AddHours(4), ChartRequestPriority.Urgent,
            "Orthopedic Clinic", ChartRequestType.PatientCare, "Needed for afternoon appointment");

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.RequestedByName, Is.EqualTo("Clerk Adams"));
        Assert.That(state.Priority, Is.EqualTo(ChartRequestPriority.Urgent));
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Pending));
        Assert.That(state.RequestedForLocation, Is.EqualTo("Orthopedic Clinic"));
    }

    [Test]
    public async Task ChartRequestGrain_CanFulfillRequest()
    {
        string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
        IChartRequestGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestGrain>(requestId);
        await grain.CreateRequestAsync("PAT-002", "Moore, Sue", "STAFF-02", "Clerk Bob",
            DateTime.UtcNow.AddHours(2), ChartRequestPriority.Routine,
            "File Room", ChartRequestType.ROI, "");

        await grain.FulfillRequestAsync("FileClerl");

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Pulled));
        Assert.That(state.FulfilledBy, Is.EqualTo("FileClerl"));
        Assert.That(state.FulfilledDate, Is.Not.Null);
    }

    [Test]
    public async Task ChartRequestGrain_CanMarkDelivered()
    {
        string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
        IChartRequestGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestGrain>(requestId);
        await grain.CreateRequestAsync("PAT-003", "Taylor, Dan", "STAFF-03", "Clerk Carol",
            DateTime.UtcNow.AddHours(1), ChartRequestPriority.STAT,
            "ICU", ChartRequestType.PatientCare, "STAT request");
        await grain.FulfillRequestAsync("Clerk1");
        await grain.MarkInTransitAsync("Courier1");

        await grain.MarkDeliveredAsync("Courier1");

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Delivered));
    }

    [Test]
    public async Task ChartRequestGrain_CanMarkNotFound()
    {
        string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
        IChartRequestGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestGrain>(requestId);
        await grain.CreateRequestAsync("PAT-004", "Harris, Jill", "STAFF-04", "Clerk Dave",
            DateTime.UtcNow.AddHours(2), ChartRequestPriority.Urgent,
            "Surgery", ChartRequestType.PatientCare, "");

        await grain.MarkNotFoundAsync();

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.NotFound));
    }

    [Test]
    public async Task ChartRequestGrain_CanCancelRequest()
    {
        string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
        IChartRequestGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestGrain>(requestId);
        await grain.CreateRequestAsync("PAT-005", "Lewis, Faye", "STAFF-05", "Clerk Eve",
            DateTime.UtcNow.AddDays(1), ChartRequestPriority.Routine,
            "Lab", ChartRequestType.Research, "");

        await grain.CancelRequestAsync("Patient appointment cancelled");

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient appointment cancelled"));
    }
}

[TestFixture]
public class ChartRequestIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ChartRequestIndexGrain_CanUpsertAndRetrieve()
    {
        IChartRequestIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-REQUEST-IDX-T1");

        ChartRequestIndexEntry entry = new()
        {
            RequestId = "REQ-001",
            PatientId = "PAT-001",
            PatientName = "Smith, Bob",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Pending,
            RequestDate = DateTime.UtcNow,
            NeededBy = DateTime.UtcNow.AddHours(4)
        };
        await grain.UpsertRequestAsync(entry);

        List<ChartRequestIndexEntry> all = await grain.GetAllRequestsAsync();
        Assert.That(all.Any(r => r.RequestId == "REQ-001"), Is.True);
    }

    [Test]
    public async Task ChartRequestIndexGrain_FilterPending()
    {
        IChartRequestIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-REQUEST-IDX-T2");

        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "REQ-P-1", PatientId = "P1", PatientName = "A",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Pending,
            RequestDate = DateTime.UtcNow, NeededBy = DateTime.UtcNow.AddHours(2)
        });
        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "REQ-P-2", PatientId = "P2", PatientName = "B",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Delivered,
            RequestDate = DateTime.UtcNow, NeededBy = DateTime.UtcNow.AddHours(2)
        });

        List<ChartRequestIndexEntry> pending = await grain.GetPendingRequestsAsync();
        Assert.That(pending.Any(r => r.RequestId == "REQ-P-1"), Is.True);
        Assert.That(pending.Any(r => r.RequestId == "REQ-P-2"), Is.False);
    }

    [Test]
    public async Task ChartRequestIndexGrain_FilterUrgent()
    {
        IChartRequestIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-REQUEST-IDX-T3");

        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "REQ-U-1", PatientId = "P1", PatientName = "A",
            Priority = ChartRequestPriority.STAT,
            Status = ChartRequestStatus.Pending,
            RequestDate = DateTime.UtcNow, NeededBy = DateTime.UtcNow.AddMinutes(30)
        });
        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "REQ-U-2", PatientId = "P2", PatientName = "B",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Pending,
            RequestDate = DateTime.UtcNow, NeededBy = DateTime.UtcNow.AddHours(8)
        });

        List<ChartRequestIndexEntry> urgent = await grain.GetUrgentRequestsAsync();
        Assert.That(urgent.Any(r => r.RequestId == "REQ-U-1"), Is.True);
        Assert.That(urgent.Any(r => r.RequestId == "REQ-U-2"), Is.False);
    }

    [Test]
    public async Task ChartRequestIndexGrain_FilterByPatient()
    {
        IChartRequestIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-REQUEST-IDX-T4");

        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "REQ-BP-1", PatientId = "PAT-TARGET", PatientName = "Target Patient",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Pending,
            RequestDate = DateTime.UtcNow, NeededBy = DateTime.UtcNow.AddHours(2)
        });
        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "REQ-BP-2", PatientId = "PAT-OTHER", PatientName = "Other Patient",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Pending,
            RequestDate = DateTime.UtcNow, NeededBy = DateTime.UtcNow.AddHours(2)
        });

        List<ChartRequestIndexEntry> forPatient = await grain.GetRequestsByPatientAsync("PAT-TARGET");
        Assert.That(forPatient.All(r => r.PatientId == "PAT-TARGET"), Is.True);
        Assert.That(forPatient.Any(r => r.RequestId == "REQ-BP-1"), Is.True);
        Assert.That(forPatient.Any(r => r.RequestId == "REQ-BP-2"), Is.False);
    }
}

[TestFixture]
public class RTIntegrationTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task RT_FullCheckoutLifecycle()
    {
        string patientId = $"PAT-INT-{Guid.NewGuid()}";
        IChartGrain chart = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        IChartIndexGrain index = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-INT-IDX-1");

        await chart.InitializeChartAsync(patientId, "Nelson, Paul", "CN-INT-1", "File Room A");
        ChartState state = await chart.GetChartAsync();
        await index.UpsertChartAsync(new ChartIndexEntry { PatientId = patientId, PatientName = "Nelson, Paul", IsCheckedOut = false, CurrentLocationType = state.CurrentLocationType });

        await chart.CheckOutChartAsync("DR-INT-1", "Dr. Stone", "Neurology",
            ChartLocationType.ClinicOutpatient, DateTime.UtcNow.AddDays(2), "Clerk1");
        state = await chart.GetChartAsync();
        await index.UpsertChartAsync(new ChartIndexEntry { PatientId = patientId, PatientName = "Nelson, Paul", IsCheckedOut = true, CurrentBorrowerName = "Dr. Stone" });

        List<ChartIndexEntry> checkedOut = await index.GetCheckedOutChartsAsync();
        Assert.That(checkedOut.Any(c => c.PatientId == patientId), Is.True);

        await chart.CheckInChartAsync("Clerk2");
        state = await chart.GetChartAsync();
        await index.UpsertChartAsync(new ChartIndexEntry { PatientId = patientId, PatientName = "Nelson, Paul", IsCheckedOut = false });

        Assert.That(state.IsCheckedOut, Is.False);
        Assert.That(state.CurrentLocation, Is.EqualTo("File Room A"));
    }

    [Test]
    public async Task RT_RequestWorkflow_CreateAndFulfill()
    {
        string patientId = $"PAT-REQ-{Guid.NewGuid()}";
        string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
        IChartGrain chart = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        IChartRequestGrain request = _cluster.GrainFactory.GetGrain<IChartRequestGrain>(requestId);
        IChartRequestIndexGrain reqIndex = _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-INT-REQIDX-1");

        await chart.InitializeChartAsync(patientId, "Bell, Kate", "CN-REQ-1", "File Room B");
        await chart.SetRequestFlagAsync(true);

        await request.CreateRequestAsync(patientId, "Bell, Kate", "STAFF-01", "Clerk John",
            DateTime.UtcNow.AddHours(2), ChartRequestPriority.Urgent,
            "Pulmonary Clinic", ChartRequestType.PatientCare, "Pre-appointment review");
        ChartRequestState reqState = await request.GetRequestAsync();
        await reqIndex.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = reqState.RequestId,
            PatientId = reqState.PatientId,
            PatientName = reqState.PatientName,
            Priority = reqState.Priority,
            Status = reqState.Status,
            RequestDate = reqState.RequestDate,
            NeededBy = reqState.NeededBy
        });

        List<ChartRequestIndexEntry> pending = await reqIndex.GetPendingRequestsAsync();
        Assert.That(pending.Any(r => r.PatientId == patientId), Is.True);

        await request.FulfillRequestAsync("FileClerl2");
        reqState = await request.GetRequestAsync();
        Assert.That(reqState.Status, Is.EqualTo(ChartRequestStatus.Pulled));
    }

    [Test]
    public async Task RT_MultipleVolumes_ActiveVolumeTracked()
    {
        string patientId = $"PAT-VOL-{Guid.NewGuid()}";
        IChartGrain chart = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");

        await chart.InitializeChartAsync(patientId, "Fox, Matt", "CN-VOL-1", "File Room A");
        await chart.AddVolumeAsync(2, "01/2020 - 12/2024");
        await chart.AddVolumeAsync(3, "01/2025 - present");

        ChartState state = await chart.GetChartAsync();
        Assert.That(state.Volumes, Has.Count.EqualTo(3));
        List<ChartVolume> active = state.Volumes.Where(v => v.IsActive).ToList();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].VolumeNumber, Is.EqualTo(3));
    }

    [Test]
    public async Task RT_LostAndFound_WorksCorrectly()
    {
        string patientId = $"PAT-LF-{Guid.NewGuid()}";
        IChartGrain chart = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        IChartIndexGrain index = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-INT-IDX-LF");

        await chart.InitializeChartAsync(patientId, "Reed, May", "CN-LF-1", "File Room A");
        await chart.MarkChartLostAsync("Cannot locate after scan", "Supervisor");
        ChartState state = await chart.GetChartAsync();
        await index.UpsertChartAsync(new ChartIndexEntry { PatientId = patientId, PatientName = "Reed, May", IsLost = true });

        List<ChartIndexEntry> lost = await index.GetLostChartsAsync();
        Assert.That(lost.Any(c => c.PatientId == patientId), Is.True);

        await chart.MarkChartFoundAsync("Radiology Room 3", ChartLocationType.Radiology, "Tech Smith");
        state = await chart.GetChartAsync();
        Assert.That(state.IsLost, Is.False);
        Assert.That(state.CurrentLocation, Is.EqualTo("Radiology Room 3"));
    }

    [Test]
    public async Task RT_OverdueDetection_WorksCorrectly()
    {
        string patientId = $"PAT-OD-{Guid.NewGuid()}";
        IChartGrain chart = _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");
        IChartIndexGrain index = _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-INT-IDX-OD");

        await chart.InitializeChartAsync(patientId, "Cole, Pat", "CN-OD-1", "File Room A");
        await chart.CheckOutChartAsync("DR-005", "Dr. Long", "Surgery",
            ChartLocationType.InpatientWard, DateTime.UtcNow.AddDays(-3), "Clerk1");

        ChartState state = await chart.GetChartAsync();
        await index.UpsertChartAsync(new ChartIndexEntry
        {
            PatientId = patientId,
            PatientName = "Cole, Pat",
            IsCheckedOut = true,
            ExpectedReturnDate = state.ExpectedReturnDate
        });

        List<ChartIndexEntry> overdue = await index.GetOverdueChartsAsync();
        Assert.That(overdue.Any(c => c.PatientId == patientId), Is.True);
    }

    [Test]
    public async Task RT_RequestQueue_PrioritizationOrder()
    {
        IChartRequestIndexGrain grain = _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-INT-REQIDX-PRIO");

        DateTime now = DateTime.UtcNow;
        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "PRIO-R-1", PatientId = "P1", PatientName = "A",
            Priority = ChartRequestPriority.Routine,
            Status = ChartRequestStatus.Pending,
            RequestDate = now, NeededBy = now.AddHours(8)
        });
        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "PRIO-S-1", PatientId = "P2", PatientName = "B",
            Priority = ChartRequestPriority.STAT,
            Status = ChartRequestStatus.Pending,
            RequestDate = now, NeededBy = now.AddMinutes(30)
        });
        await grain.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = "PRIO-U-1", PatientId = "P3", PatientName = "C",
            Priority = ChartRequestPriority.Urgent,
            Status = ChartRequestStatus.Pending,
            RequestDate = now, NeededBy = now.AddHours(2)
        });

        List<ChartRequestIndexEntry> urgent = await grain.GetUrgentRequestsAsync();
        List<ChartRequestIndexEntry> myUrgent = urgent.Where(r => r.RequestId.StartsWith("PRIO-")).ToList();
        Assert.That(myUrgent.Any(r => r.RequestId == "PRIO-S-1"), Is.True);
        Assert.That(myUrgent.Any(r => r.RequestId == "PRIO-U-1"), Is.True);
        Assert.That(myUrgent.Any(r => r.RequestId == "PRIO-R-1"), Is.False);
    }
}
