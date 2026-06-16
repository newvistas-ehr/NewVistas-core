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
/// Functional tests for Record Tracking — VistA File #190.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class RecordTrackingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IChartGrain GetChart(string patientId)
        => _cluster.GrainFactory.GetGrain<IChartGrain>($"RT-CHART:{patientId}");

    private IChartIndexGrain GetChartIndex()
        => _cluster.GrainFactory.GetGrain<IChartIndexGrain>("RT-CHART-IDX");

    private IChartRequestGrain GetRequest(string id)
        => _cluster.GrainFactory.GetGrain<IChartRequestGrain>(id);

    private IChartRequestIndexGrain GetRequestIndex()
        => _cluster.GrainFactory.GetGrain<IChartRequestIndexGrain>("RT-REQUEST-IDX");

    // ── Chart Tests ──────────────────────────────────────────────────────────

    [Test]
    public async Task InitializeChart_CreatesChartRecord()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "DOE,JOHN", "C-2024-001", "File Room A");

        ChartState state = await grain.GetChartAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(state.ChartNumber, Is.EqualTo("C-2024-001"));
        Assert.That(state.HomeLocation, Is.EqualTo("File Room A"));
        Assert.That(state.IsCheckedOut, Is.False);
        Assert.That(state.IsLost, Is.False);
    }

    [Test]
    public async Task CheckOutChart_SetsCheckedOutStatus()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "SMITH,JANE", "C-2024-002", "File Room B");

        DateTime expectedReturn = DateTime.UtcNow.AddDays(3);
        await grain.CheckOutChartAsync(
            "DR-001", "Dr. Adams", "Primary Care Clinic A",
            ChartLocationType.ClinicOutpatient, expectedReturn,
            "File Clerk Brown");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsCheckedOut, Is.True);
        Assert.That(state.CurrentBorrowerName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.CurrentLocation, Is.EqualTo("Primary Care Clinic A"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.ClinicOutpatient));
        Assert.That(state.CheckOutDate, Is.Not.Null);
        Assert.That(state.ExpectedReturnDate, Is.Not.Null);
    }

    [Test]
    public async Task CheckInChart_ReturnsToFileRoom()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "GREEN,BOB", "C-2024-003", "File Room A");
        await grain.CheckOutChartAsync(
            "DR-002", "Dr. Wilson", "Surgery", ChartLocationType.InpatientWard,
            null, "File Clerk Smith");

        await grain.CheckInChartAsync("File Clerk Smith");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsCheckedOut, Is.False);
        Assert.That(state.CurrentLocation, Is.EqualTo("File Room A"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.FileRoom));
    }

    [Test]
    public async Task TransferChart_ChangesLocationAndBorrower()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "WHITE,TOM", "C-2024-004", "File Room B");
        await grain.CheckOutChartAsync(
            "DR-003", "Dr. Davis", "Clinic B", ChartLocationType.ClinicOutpatient,
            null, "Clerk Jones");

        await grain.TransferChartAsync(
            "Radiology Reading Room", ChartLocationType.Radiology,
            "RAD-001", "Dr. Radiologist",
            "Clerk Jones");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsCheckedOut, Is.True);
        Assert.That(state.CurrentLocation, Is.EqualTo("Radiology Reading Room"));
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.Radiology));
        Assert.That(state.CurrentBorrowerName, Is.EqualTo("Dr. Radiologist"));
    }

    [Test]
    public async Task SetRequestFlag_TogglesFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "KING,DAN", "C-2024-005", "File Room A");

        await grain.SetRequestFlagAsync(true);
        ChartState stateOn = await grain.GetChartAsync();
        Assert.That(stateOn.IsOnRequest, Is.True);

        await grain.SetRequestFlagAsync(false);
        ChartState stateOff = await grain.GetChartAsync();
        Assert.That(stateOff.IsOnRequest, Is.False);
    }

    [Test]
    public async Task AddVolume_AddsToVolumeList()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "BROWN,SUE", "C-2024-006", "File Room B");

        await grain.AddVolumeAsync(2, "01/2010 - 12/2019");
        await grain.AddVolumeAsync(3, "01/2020 - Present");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.Volumes, Has.Count.EqualTo(3)); // 1 from Initialize + 2 added
        Assert.That(state.Volumes[0].VolumeNumber, Is.EqualTo(1)); // default from Initialize
        Assert.That(state.Volumes[1].VolumeNumber, Is.EqualTo(2));
        Assert.That(state.Volumes[2].DateRange, Is.EqualTo("01/2020 - Present"));
    }

    [Test]
    public async Task MarkLost_SetsLostFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "GRAY,ALICE", "C-2024-007", "File Room A");
        await grain.CheckOutChartAsync(
            "DR-004", "Dr. Lee", "Lab", ChartLocationType.Lab,
            null, "Clerk Davis");

        await grain.MarkChartLostAsync("Chart not returned after 30 days", "Clerk Davis");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsLost, Is.True);
        Assert.That(state.CurrentLocationType, Is.EqualTo(ChartLocationType.Lost));
    }

    [Test]
    public async Task MarkFound_ClearsLostFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartGrain grain = GetChart(patientId);

        await grain.InitializeChartAsync(patientId, "LEE,PAT", "C-2024-008", "File Room B");
        await grain.MarkChartLostAsync("Cannot locate chart", "Clerk Brown");
        await grain.MarkChartFoundAsync("Found in Dr. Kim's office", ChartLocationType.ProviderOffice, "Clerk Brown");

        ChartState state = await grain.GetChartAsync();
        Assert.That(state.IsLost, Is.False);
        Assert.That(state.CurrentLocation, Is.EqualTo("Found in Dr. Kim's office"));
    }

    // ── Chart Index Tests ────────────────────────────────────────────────────

    [Test]
    public async Task ChartIndex_QueryCheckedOutCharts()
    {
        IChartIndexGrain index = GetChartIndex();

        string patientId = $"PAT-IDX-{Guid.NewGuid():N}";
        await index.UpsertChartAsync(new ChartIndexEntry
        {
            PatientId = patientId, PatientName = "TEST,CHART",
            ChartNumber = "C-IDX-001",
            CurrentLocation = "Clinic A",
            CurrentLocationType = ChartLocationType.ClinicOutpatient,
            IsCheckedOut = true, IsOnRequest = false, IsLost = false,
            CheckOutDate = DateTime.UtcNow,
            CurrentBorrowerName = "Dr. Test"
        });

        List<ChartIndexEntry> checkedOut = await index.GetCheckedOutChartsAsync();
        Assert.That(checkedOut.Any(c => c.PatientId == patientId), Is.True);
    }

    // ── Chart Request Tests ──────────────────────────────────────────────────

    [Test]
    public async Task CreateRequest_SetsPendingStatus()
    {
        string requestId = $"RT-REQUEST-{Guid.NewGuid():N}";
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IChartRequestGrain grain = GetRequest(requestId);

        await grain.CreateRequestAsync(
            patientId, "DOE,JOHN",
            "STAFF-001", "Dr. Adams",
            DateTime.UtcNow.AddHours(2),
            ChartRequestPriority.Urgent,
            "Primary Care Clinic B",
            ChartRequestType.PatientCare,
            "Patient has walk-in appointment");

        ChartRequestState state = await grain.GetRequestAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.RequestedByName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.Priority, Is.EqualTo(ChartRequestPriority.Urgent));
        Assert.That(state.RequestType, Is.EqualTo(ChartRequestType.PatientCare));
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Pending));
    }

    [Test]
    public async Task FulfillRequest_TransitionsToPulled()
    {
        string requestId = $"RT-REQUEST-{Guid.NewGuid():N}";
        IChartRequestGrain grain = GetRequest(requestId);

        await grain.CreateRequestAsync(
            "PAT-REQ-1", "SMITH,JANE", "STAFF-002", "Nurse Wilson",
            DateTime.UtcNow.AddHours(4), ChartRequestPriority.Routine,
            "Ward 3B", ChartRequestType.PatientCare, string.Empty);

        await grain.FulfillRequestAsync("File Clerk Brown");

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Pulled));
        Assert.That(state.FulfilledBy, Is.EqualTo("File Clerk Brown"));
        Assert.That(state.FulfilledDate, Is.Not.Null);
    }

    [Test]
    public async Task RequestFullLifecycle_PendingToDelivered()
    {
        string requestId = $"RT-REQUEST-{Guid.NewGuid():N}";
        IChartRequestGrain grain = GetRequest(requestId);

        await grain.CreateRequestAsync(
            "PAT-REQ-2", "GREEN,BOB", "STAFF-003", "Admin Clark",
            DateTime.UtcNow.AddDays(1), ChartRequestPriority.STAT,
            "Emergency Dept", ChartRequestType.PatientCare, "STAT request for ER");

        await grain.FulfillRequestAsync("Clerk Smith");
        await grain.MarkInTransitAsync("Runner Jones");
        ChartRequestState stateTransit = await grain.GetRequestAsync();
        Assert.That(stateTransit.Status, Is.EqualTo(ChartRequestStatus.InTransit));

        await grain.MarkDeliveredAsync("Runner Jones");
        ChartRequestState stateDelivered = await grain.GetRequestAsync();
        Assert.That(stateDelivered.Status, Is.EqualTo(ChartRequestStatus.Delivered));
    }

    [Test]
    public async Task CancelRequest_SetsCancelledStatus()
    {
        string requestId = $"RT-REQUEST-{Guid.NewGuid():N}";
        IChartRequestGrain grain = GetRequest(requestId);

        await grain.CreateRequestAsync(
            "PAT-REQ-3", "WHITE,TOM", "STAFF-004", "Dr. Miller",
            DateTime.UtcNow.AddDays(2), ChartRequestPriority.Routine,
            "Research Office", ChartRequestType.Research, string.Empty);

        await grain.CancelRequestAsync("Patient appointment cancelled");

        ChartRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ChartRequestStatus.Cancelled));
        Assert.That(state.CancellationReason, Does.Contain("appointment cancelled"));
    }

    // ── Request Index Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RequestIndex_QueryPendingRequests()
    {
        IChartRequestIndexGrain index = GetRequestIndex();

        string requestId = $"RT-REQUEST-{Guid.NewGuid():N}";
        await index.UpsertRequestAsync(new ChartRequestIndexEntry
        {
            RequestId = requestId, PatientId = "PAT-RIDX-1",
            PatientName = "TEST,REQUEST",
            RequestedByName = "Dr. Test",
            RequestDate = DateTime.UtcNow,
            NeededBy = DateTime.UtcNow.AddHours(4),
            Priority = ChartRequestPriority.Urgent,
            Status = ChartRequestStatus.Pending,
            RequestedForLocation = "Clinic A",
            RequestType = ChartRequestType.PatientCare
        });

        List<ChartRequestIndexEntry> pending = await index.GetPendingRequestsAsync();
        Assert.That(pending.Any(r => r.RequestId == requestId), Is.True);
    }
}
