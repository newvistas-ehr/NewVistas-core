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
public class BedManagementWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Bed Grain ───────────────────────────────────────────────────────────

    [Test]
    public async Task BedGrain_CanSetup()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:TEST-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "01", "A", "REGULAR", "MAIN");

        BedState state = await grain.GetBedAsync();
        Assert.That(state.WardId, Is.EqualTo("WARD-MED-3A"));
        Assert.That(state.Status, Is.EqualTo("AVAILABLE"));
        Assert.That(state.BedType, Is.EqualTo("REGULAR"));
    }

    [Test]
    public async Task BedGrain_CanAssignPatient()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:ASSIGN-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-ICU-1", "ICU", "03", "B", "ICU", "MAIN");
        await grain.AssignPatientAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow.AddDays(3));

        BedState state = await grain.GetBedAsync();
        Assert.That(state.Status, Is.EqualTo("OCCUPIED"));
        Assert.That(state.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(state.OccupiedSince, Is.Not.Null);
    }

    [Test]
    public async Task BedGrain_CanDischargePatient()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:DC-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "05", "A", "REGULAR", "MAIN");
        await grain.AssignPatientAsync("PAT-002", "SMITH,JANE", null);
        await grain.DischargePatientAsync();

        BedState state = await grain.GetBedAsync();
        Assert.That(state.Status, Is.EqualTo("CLEANING"));
        Assert.That(state.PatientId, Is.Null);
    }

    [Test]
    public async Task BedGrain_CanReserveAndClear()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:RES-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "06", "B", "REGULAR", "MAIN");
        await grain.ReserveForPatientAsync("PAT-003", "WILLIAMS,BOB");

        BedState reserved = await grain.GetBedAsync();
        Assert.That(reserved.Status, Is.EqualTo("RESERVED"));
        Assert.That(reserved.ReservedForPatientName, Is.EqualTo("WILLIAMS,BOB"));

        await grain.ClearReservationAsync();
        Assert.That((await grain.GetBedAsync()).Status, Is.EqualTo("AVAILABLE"));
    }

    [Test]
    public async Task BedGrain_AssignClearsReservation()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:RESCLR-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "07", "A", "REGULAR", "MAIN");
        await grain.ReserveForPatientAsync("PAT-004", "BROWN,ALICE");
        await grain.AssignPatientAsync("PAT-004", "BROWN,ALICE", null);

        BedState state = await grain.GetBedAsync();
        Assert.That(state.Status, Is.EqualTo("OCCUPIED"));
        Assert.That(state.ReservedForPatientId, Is.Null);
    }

    [Test]
    public async Task BedGrain_CanBlockAndUnblock()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:BLK-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "08", "A", "REGULAR", "MAIN");
        await grain.BlockBedAsync("Plumbing repair");

        Assert.That((await grain.GetBedAsync()).Status, Is.EqualTo("BLOCKED"));

        await grain.UnblockBedAsync();
        Assert.That((await grain.GetBedAsync()).Status, Is.EqualTo("AVAILABLE"));
    }

    [Test]
    public async Task BedGrain_CanSetIsolation()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:ISO-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "09", "A", "REGULAR", "MAIN");
        await grain.SetIsolationAsync("AIRBORNE");

        Assert.That((await grain.GetBedAsync()).IsolationType, Is.EqualTo("AIRBORNE"));
    }

    [Test]
    public async Task BedGrain_CanSetOutOfService()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:OOS-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "10", "A", "REGULAR", "MAIN");
        await grain.SetOutOfServiceAsync("Bed frame broken");

        BedState state = await grain.GetBedAsync();
        Assert.That(state.Status, Is.EqualTo("OUT_OF_SERVICE"));
        Assert.That(state.BlockReason, Is.EqualTo("Bed frame broken"));
    }

    // ─── Bed Board ───────────────────────────────────────────────────────────

    [Test]
    public async Task BedBoard_SelfSeedsDemoBeds()
    {
        var board = _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:DEMO-{Guid.NewGuid():N}");
        List<BedSummaryEntry> beds = await board.GetAllBedsAsync();

        // 3 wards * 12 beds + 1 ICU * 8 beds = 44
        Assert.That(beds, Has.Count.EqualTo(44));
        Assert.That(await board.GetAvailableBedCountAsync(), Is.EqualTo(44));
    }

    [Test]
    public async Task BedBoard_CanFilterByWard()
    {
        var board = _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:WARD-{Guid.NewGuid():N}");
        await board.GetAllBedsAsync(); // trigger seeding

        List<BedSummaryEntry> icuBeds = await board.GetBedsByWardAsync("WARD-ICU-1");
        Assert.That(icuBeds, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task BedBoard_CanFilterByType()
    {
        var board = _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:TYPE-{Guid.NewGuid():N}");
        await board.GetAllBedsAsync(); // trigger seeding

        List<BedSummaryEntry> icuBeds = await board.GetAvailableBedsByTypeAsync("ICU");
        Assert.That(icuBeds, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task BedBoard_CanUpdateBedStatus()
    {
        var board = _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:UPD-{Guid.NewGuid():N}");
        List<BedSummaryEntry> beds = await board.GetAllBedsAsync();
        string bedId = beds[0].BedId;

        // Mark as occupied
        await board.AddOrUpdateBedAsync(new BedSummaryEntry
        {
            BedId = bedId, WardId = beds[0].WardId, WardName = beds[0].WardName,
            RoomNumber = beds[0].RoomNumber, BedPosition = beds[0].BedPosition,
            Status = "OCCUPIED", BedType = beds[0].BedType, PatientName = "JONES,M"
        });

        Assert.That(await board.GetOccupiedBedCountAsync(), Is.EqualTo(1));
        Assert.That(await board.GetAvailableBedCountAsync(), Is.EqualTo(43));
    }

    [Test]
    public async Task BedBoard_CountsAreAccurate()
    {
        var board = _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:CNT-{Guid.NewGuid():N}");
        await board.GetAllBedsAsync();

        int total = await board.GetTotalBedCountAsync();
        int available = await board.GetAvailableBedCountAsync();
        int occupied = await board.GetOccupiedBedCountAsync();

        Assert.That(total, Is.EqualTo(44));
        Assert.That(available, Is.EqualTo(44));
        Assert.That(occupied, Is.EqualTo(0));
    }

    // ─── Full Workflow ───────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_AdmitToDischargeToCleanToAvailable()
    {
        var grain = _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:MAIN:FULL-{Guid.NewGuid():N}");
        await grain.SetupBedAsync("WARD-MED-3A", "Medicine 3A", "11", "A", "REGULAR", "MAIN");

        // Admit
        await grain.AssignPatientAsync("PAT-FULL", "FULL,TEST", DateTime.UtcNow.AddDays(5));
        Assert.That((await grain.GetBedAsync()).Status, Is.EqualTo("OCCUPIED"));

        // Discharge → Cleaning
        await grain.DischargePatientAsync();
        Assert.That((await grain.GetBedAsync()).Status, Is.EqualTo("CLEANING"));

        // Clean → Available
        await grain.SetAvailableAsync();
        BedState final = await grain.GetBedAsync();
        Assert.That(final.Status, Is.EqualTo("AVAILABLE"));
        Assert.That(final.PatientId, Is.Null);
    }
}
