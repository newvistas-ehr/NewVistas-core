// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NUnit.Framework;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Bed management against the unit-owns-beds model: IInpatientUnitGrain owns rooms,
/// beds, occupancy, reservations, and EVS turnover; IBedCapacityGrain is the per-
/// institution rollup/directory. Covers the bed lifecycle transition table (legal +
/// rejected transitions), reservation expiry (lazy sweep), occupancy rules, capacity
/// rollup correctness, the dirty-bed queue, and structure guards.
/// </summary>
[TestFixture]
public class BedManagementWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private IInpatientUnitGrain Unit(string institutionId, string unitId)
        => _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    private IBedCapacityGrain Capacity(string institutionId)
        => _cluster.GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");

    /// <summary>Fresh, isolated unit with <paramref name="beds"/> beds B1..Bn (no rooms).</summary>
    private async Task<(string Inst, string UnitId, IInpatientUnitGrain Grain)> NewUnitAsync(
        int beds = 4, string name = "Test Unit", string? unitType = "MedSurg")
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string unitId = $"U-{Guid.NewGuid():N}";
        IInpatientUnitGrain grain = Unit(inst, unitId);
        await grain.ConfigureUnitAsync(name, unitType, "Internal Medicine");
        for (int i = 1; i <= beds; i++)
            await grain.AddBedAsync($"B{i}", null, BedType.Regular);
        return (inst, unitId, grain);
    }

    private static UnitAdmissionRequest Admission(string patientId, string? bedId,
        string patientName = "TEST,PATIENT", bool overrideReservation = false) => new()
    {
        PatientId = patientId,
        PatientName = patientName,
        MovementId = $"ADT-{Guid.NewGuid()}",
        BedId = bedId,
        AdmitDate = DateTime.UtcNow,
        OverrideReservation = overrideReservation
    };

    private static async Task<InpatientBed> BedAsync(IInpatientUnitGrain grain, string bedId)
    {
        InpatientUnitState state = await grain.GetAsync();
        return state.Beds.First(b => b.BedId == bedId);
    }

    // ─── Transition table — legal transitions ───────────────────────────────

    [Test]
    public async Task Lifecycle_AddBed_StartsAvailable()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        InpatientBed bed = await BedAsync(unit, "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Available));
    }

    [Test]
    public async Task Lifecycle_FullEvsTurn_OccupyReleaseCleanAvailable()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        string pid = $"PAT-{Guid.NewGuid()}";

        // Available → Occupied
        await unit.AdmitPatientAsync(Admission(pid, "B1"));
        Assert.That((await BedAsync(unit, "B1")).State, Is.EqualTo(BedLifecycleState.Occupied));

        // Occupied → Dirty (release returns the vacated bed)
        string? vacated = await unit.ReleasePatientAsync(pid, $"ADT-{Guid.NewGuid()}");
        Assert.That(vacated, Is.EqualTo("B1"));
        InpatientBed dirty = await BedAsync(unit, "B1");
        Assert.That(dirty.State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(dirty.PatientId, Is.Null);
        Assert.That(dirty.DirtySince, Is.Not.Null);

        // Dirty → Cleaning → Available
        await unit.StartCleaningAsync("B1", "EVS,ONE");
        Assert.That((await BedAsync(unit, "B1")).State, Is.EqualTo(BedLifecycleState.Cleaning));
        await unit.MarkBedCleanAsync("B1", "EVS,ONE");
        InpatientBed clean = await BedAsync(unit, "B1");
        Assert.That(clean.State, Is.EqualTo(BedLifecycleState.Available));
        Assert.That(clean.LastCleanedAt, Is.Not.Null);
    }

    [Test]
    public async Task Lifecycle_DirtyDirectlyToAvailable_SkipStartAllowed()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        string pid = $"PAT-{Guid.NewGuid()}";
        await unit.AdmitPatientAsync(Admission(pid, "B1"));
        await unit.ReleasePatientAsync(pid, $"ADT-{Guid.NewGuid()}");

        // Dirty → Available without StartCleaning (one-click turn for small sites)
        await unit.MarkBedCleanAsync("B1", null);
        Assert.That((await BedAsync(unit, "B1")).State, Is.EqualTo(BedLifecycleState.Available));
    }

    [Test]
    public async Task Lifecycle_MarkDirty_AvailableToDirty()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.MarkBedDirtyAsync("B1");
        Assert.That((await BedAsync(unit, "B1")).State, Is.EqualTo(BedLifecycleState.Dirty));
    }

    [Test]
    public async Task Lifecycle_BlockAndUnblock()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.BlockBedAsync("B1", "Staffing hold");

        InpatientBed blocked = await BedAsync(unit, "B1");
        Assert.That(blocked.State, Is.EqualTo(BedLifecycleState.Blocked));
        Assert.That(blocked.BlockReason, Is.EqualTo("Staffing hold"));

        await unit.UnblockBedAsync("B1");
        InpatientBed unblocked = await BedAsync(unit, "B1");
        Assert.That(unblocked.State, Is.EqualTo(BedLifecycleState.Available));
        Assert.That(unblocked.BlockReason, Is.Null);
    }

    [Test]
    public async Task Lifecycle_OutOfService_ReturnsToDirtyNotAvailable()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.SetOutOfServiceAsync("B1", "Bed frame broken");

        InpatientBed oos = await BedAsync(unit, "B1");
        Assert.That(oos.State, Is.EqualTo(BedLifecycleState.OutOfService));
        Assert.That(oos.BlockReason, Is.EqualTo("Bed frame broken"));

        // ReturnToService → Dirty (must be cleaned before it is placeable)
        await unit.ReturnToServiceAsync("B1");
        Assert.That((await BedAsync(unit, "B1")).State, Is.EqualTo(BedLifecycleState.Dirty));
    }

    [Test]
    public async Task Lifecycle_SetIsolation_Persists()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.SetBedIsolationAsync("B1", BedIsolationType.Airborne);
        Assert.That((await BedAsync(unit, "B1")).Isolation, Is.EqualTo(BedIsolationType.Airborne));
    }

    // ─── Transition table — rejected transitions ────────────────────────────

    [Test]
    public async Task OccupiedBed_CannotBeBlocked()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1"));

        Assert.ThrowsAsync<InvalidOperationException>(() => unit.BlockBedAsync("B1", "nope"));
    }

    [Test]
    public async Task OccupiedBed_CannotBeSetOutOfService()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1"));

        Assert.ThrowsAsync<InvalidOperationException>(() => unit.SetOutOfServiceAsync("B1", "nope"));
    }

    [Test]
    public async Task Reserve_NonAvailableBed_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1"));   // Occupied
        await unit.MarkBedDirtyAsync("B2");                                       // Dirty

        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.ReserveBedAsync("B1", $"PAT-{Guid.NewGuid()}", "WAITING,ONE", null));
        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.ReserveBedAsync("B2", $"PAT-{Guid.NewGuid()}", "WAITING,TWO", null));
    }

    [Test]
    public async Task MarkClean_FromAvailable_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        Assert.ThrowsAsync<InvalidOperationException>(() => unit.MarkBedCleanAsync("B1", null));
    }

    [Test]
    public async Task StartCleaning_FromAvailable_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        Assert.ThrowsAsync<InvalidOperationException>(() => unit.StartCleaningAsync("B1", null));
    }

    [Test]
    public async Task Occupy_DirtyOrBlockedBed_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        await unit.MarkBedDirtyAsync("B1");
        await unit.BlockBedAsync("B2", "hold");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1")));
        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B2")));
    }

    // ─── Occupancy rules ─────────────────────────────────────────────────────

    [Test]
    public async Task DoubleOccupancy_SecondPatientIntoOccupiedBed_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1", "FIRST,PATIENT"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1", "SECOND,PATIENT")));
    }

    [Test]
    public async Task SamePatientTwiceOnUnit_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        string pid = $"PAT-{Guid.NewGuid()}";
        await unit.AdmitPatientAsync(Admission(pid, "B1"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AdmitPatientAsync(Admission(pid, "B2")));
    }

    [Test]
    public async Task AdmitPatient_IdempotentByMovementId()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        UnitAdmissionRequest request = Admission($"PAT-{Guid.NewGuid()}", "B1");

        await unit.AdmitPatientAsync(request);
        await unit.AdmitPatientAsync(request);   // retry — no-op success

        List<UnitCensusEntry> census = await unit.GetCensusAsync();
        Assert.That(census, Has.Count.EqualTo(1));
    }

    // ─── Reservations ────────────────────────────────────────────────────────

    [Test]
    public async Task Reserve_OccupyByOtherPatient_Throws_ButOverrideSucceeds()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        string reservedFor = $"PAT-{Guid.NewGuid()}";
        await unit.ReserveBedAsync("B1", reservedFor, "EXPECTED,PATIENT", null);

        // Someone else without override → rejected
        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1", "WALKIN,PATIENT")));

        // Same someone else WITH override → succeeds (bed-control override)
        string walkIn = $"PAT-{Guid.NewGuid()}";
        await unit.AdmitPatientAsync(Admission(walkIn, "B1", "WALKIN,PATIENT", overrideReservation: true));

        InpatientBed bed = await BedAsync(unit, "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(bed.PatientId, Is.EqualTo(walkIn));
        Assert.That(bed.ReservedForPatientId, Is.Null, "Override must clear the reservation fields.");
    }

    [Test]
    public async Task Reserve_ReservedPatientArrives_AutoClearsReservation()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        string pid = $"PAT-{Guid.NewGuid()}";
        await unit.ReserveBedAsync("B1", pid, "EXPECTED,PATIENT", DateTime.UtcNow.AddHours(4));

        await unit.AdmitPatientAsync(Admission(pid, "B1", "EXPECTED,PATIENT"));

        InpatientBed bed = await BedAsync(unit, "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(bed.PatientId, Is.EqualTo(pid));
        Assert.That(bed.ReservedForPatientId, Is.Null);
        Assert.That(bed.ReservationExpiresAt, Is.Null);
    }

    [Test]
    public async Task Reserve_ExpiredReservation_SweptBackToAvailable()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.ReserveBedAsync("B1", $"PAT-{Guid.NewGuid()}", "NOSHOW,PATIENT",
            DateTime.UtcNow.AddMinutes(-5));   // already expired

        // The next call sweeps the expired reservation → Available.
        InpatientUnitState state = await unit.GetAsync();
        InpatientBed bed = state.Beds.First(b => b.BedId == "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Available));
        Assert.That(bed.ReservedForPatientId, Is.Null);
    }

    [Test]
    public async Task Reserve_ExpiredReservation_BedIsPlaceableByAnotherPatient()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.ReserveBedAsync("B1", $"PAT-{Guid.NewGuid()}", "NOSHOW,PATIENT",
            DateTime.UtcNow.AddMinutes(-5));

        // No override needed — the sweep at the top of AdmitPatientAsync frees the bed.
        string pid = $"PAT-{Guid.NewGuid()}";
        await unit.AdmitPatientAsync(Admission(pid, "B1"));
        Assert.That((await BedAsync(unit, "B1")).PatientId, Is.EqualTo(pid));
    }

    [Test]
    public async Task Reserve_SamePatientReReserve_RefreshesExpiry()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        string pid = $"PAT-{Guid.NewGuid()}";
        DateTime first = DateTime.UtcNow.AddHours(1);
        DateTime second = DateTime.UtcNow.AddHours(8);

        await unit.ReserveBedAsync("B1", pid, "EXPECTED,PATIENT", first);
        await unit.ReserveBedAsync("B1", pid, "EXPECTED,PATIENT", second);   // idempotent refresh

        InpatientBed bed = await BedAsync(unit, "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Reserved));
        Assert.That(bed.ReservationExpiresAt, Is.EqualTo(second));
    }

    [Test]
    public async Task ClearReservation_Idempotent()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.ReserveBedAsync("B1", $"PAT-{Guid.NewGuid()}", "EXPECTED,PATIENT", null);

        await unit.ClearReservationAsync("B1");
        await unit.ClearReservationAsync("B1");   // double-clear is a no-op

        Assert.That((await BedAsync(unit, "B1")).State, Is.EqualTo(BedLifecycleState.Available));
    }

    // ─── Release / move ──────────────────────────────────────────────────────

    [Test]
    public async Task ReleasePatient_NotOnUnit_IsIdempotentNoOp()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        string? vacated = await unit.ReleasePatientAsync($"PAT-{Guid.NewGuid()}", $"ADT-{Guid.NewGuid()}");
        Assert.That(vacated, Is.Null);
    }

    [Test]
    public async Task MoveOccupant_IntraUnitSwap_OldBedDirty_CarriesNurseAndAcuity()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        string pid = $"PAT-{Guid.NewGuid()}";
        await unit.AdmitPatientAsync(Admission(pid, "B1"));
        await unit.AssignBedNurseAsync("B1", "RN-1", "NURSE,ONE");
        await unit.UpdateBedAcuityAsync("B1", AcuityLevel.IntensiveCare);

        await unit.MoveOccupantAsync(pid, "B2", $"ADT-{Guid.NewGuid()}", overrideReservation: false);

        InpatientBed oldBed = await BedAsync(unit, "B1");
        InpatientBed newBed = await BedAsync(unit, "B2");
        Assert.That(oldBed.State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(oldBed.PatientId, Is.Null);
        Assert.That(newBed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(newBed.PatientId, Is.EqualTo(pid));
        Assert.That(newBed.AttendingNurseId, Is.EqualTo("RN-1"), "Nursing assignment travels with the patient.");
        Assert.That(newBed.AcuityLevel, Is.EqualTo(AcuityLevel.IntensiveCare));
    }

    // ─── Capacity rollup + dirty queue ───────────────────────────────────────

    [Test]
    public async Task CapacityRollup_CountsCorrectAfterSequence()
    {
        var (inst, unitId, unit) = await NewUnitAsync(5, name: "Rollup Unit");
        string pid = $"PAT-{Guid.NewGuid()}";

        await unit.AdmitPatientAsync(Admission(pid, "B1"));                                   // Occupied
        await unit.ReserveBedAsync("B2", $"PAT-{Guid.NewGuid()}", "EXPECTED,PATIENT", null);  // Reserved
        await unit.MarkBedDirtyAsync("B3");                                                   // Dirty
        await unit.BlockBedAsync("B4", "hold");                                               // Blocked
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", null, "BOARDER,ED")); // Boarder

        List<UnitCapacitySummary> units = await Capacity(inst).GetUnitsAsync();
        Assert.That(units, Has.Count.EqualTo(1));
        UnitCapacitySummary s = units[0];
        Assert.That(s.UnitId, Is.EqualTo(unitId));
        Assert.That(s.Name, Is.EqualTo("Rollup Unit"));
        Assert.That(s.TotalBeds, Is.EqualTo(5));
        Assert.That(s.Occupied, Is.EqualTo(1));
        Assert.That(s.Reserved, Is.EqualTo(1));
        Assert.That(s.Dirty, Is.EqualTo(1));
        Assert.That(s.Blocked, Is.EqualTo(1));
        Assert.That(s.Available, Is.EqualTo(1));
        Assert.That(s.Boarders, Is.EqualTo(1));

        // Release the occupant → Occupied 0, Dirty 2 on the next rollup.
        await unit.ReleasePatientAsync(pid, $"ADT-{Guid.NewGuid()}");
        UnitCapacitySummary? after = await Capacity(inst).GetUnitAsync(unitId);
        Assert.That(after, Is.Not.Null);
        Assert.That(after!.Occupied, Is.EqualTo(0));
        Assert.That(after.Dirty, Is.EqualTo(2));
    }

    [Test]
    public async Task CapacityRollup_InstitutionTotals_SumAcrossUnits()
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        IInpatientUnitGrain unitA = Unit(inst, $"U-{Guid.NewGuid():N}");
        IInpatientUnitGrain unitB = Unit(inst, $"U-{Guid.NewGuid():N}");
        await unitA.ConfigureUnitAsync("Unit A", "MedSurg", null);
        await unitB.ConfigureUnitAsync("Unit B", "ICU", null);
        await unitA.AddBedAsync("A1", null, BedType.Regular);
        await unitA.AddBedAsync("A2", null, BedType.Regular);
        await unitB.AddBedAsync("I1", null, BedType.Icu);

        await unitA.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "A1"));

        (int total, int available, int occupied, int dirty, int blocked, int outOfService)
            = await Capacity(inst).GetInstitutionTotalsAsync();
        Assert.That(total, Is.EqualTo(3));
        Assert.That(available, Is.EqualTo(2));
        Assert.That(occupied, Is.EqualTo(1));
        Assert.That(dirty, Is.EqualTo(0));
        Assert.That(blocked, Is.EqualTo(0));
        Assert.That(outOfService, Is.EqualTo(0));
    }

    [Test]
    public async Task DirtyBedQueue_ListsDirtyAndCleaningBedsWithIsolation()
    {
        var (inst, unitId, unit) = await NewUnitAsync(3);
        string pid = $"PAT-{Guid.NewGuid()}";
        await unit.SetBedIsolationAsync("B1", BedIsolationType.Contact);
        await unit.AdmitPatientAsync(Admission(pid, "B1"));
        await unit.ReleasePatientAsync(pid, $"ADT-{Guid.NewGuid()}");   // B1 → Dirty
        await unit.MarkBedDirtyAsync("B2");
        await unit.StartCleaningAsync("B2", "EVS,ONE");                  // B2 → Cleaning

        List<(string UnitId, DirtyBedEntry Bed)> queue = await Capacity(inst).GetDirtyBedQueueAsync();

        Assert.That(queue, Has.Count.EqualTo(2));
        Assert.That(queue.All(q => q.UnitId == unitId), Is.True);
        DirtyBedEntry b1 = queue.Single(q => q.Bed.BedId == "B1").Bed;
        DirtyBedEntry b2 = queue.Single(q => q.Bed.BedId == "B2").Bed;
        Assert.That(b1.State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(b1.Isolation, Is.EqualTo(BedIsolationType.Contact), "EVS must see precautions before entering.");
        Assert.That(b2.State, Is.EqualTo(BedLifecycleState.Cleaning));

        // Turning the beds empties the queue.
        await unit.MarkBedCleanAsync("B1", null);
        await unit.MarkBedCleanAsync("B2", null);
        Assert.That(await Capacity(inst).GetDirtyBedQueueAsync(), Is.Empty);
    }

    // ─── Structure guards ────────────────────────────────────────────────────

    [Test]
    public async Task AddBed_DuplicateBedId_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        Assert.ThrowsAsync<InvalidOperationException>(() => unit.AddBedAsync("B1", null, BedType.Regular));
    }

    [Test]
    public async Task AddBed_UnknownRoom_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(0);
        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AddBedAsync("B1", "ROOM-DOES-NOT-EXIST", BedType.Regular));
    }

    [Test]
    public async Task AddBed_WithRoom_AssignsRoomId()
    {
        var (_, _, unit) = await NewUnitAsync(0);
        await unit.AddOrUpdateRoomAsync(new InpatientRoom { RoomId = "301", Name = "Room 301" });
        await unit.AddBedAsync("301-A", "301", BedType.Regular);

        InpatientBed bed = await BedAsync(unit, "301-A");
        Assert.That(bed.RoomId, Is.EqualTo("301"));
    }

    [Test]
    public async Task RemoveBed_OccupiedOrReserved_Throws()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1"));
        await unit.ReserveBedAsync("B2", $"PAT-{Guid.NewGuid()}", "EXPECTED,PATIENT", null);

        Assert.ThrowsAsync<InvalidOperationException>(() => unit.RemoveBedAsync("B1"));
        Assert.ThrowsAsync<InvalidOperationException>(() => unit.RemoveBedAsync("B2"));
    }

    [Test]
    public async Task RemoveBed_AvailableBed_Succeeds()
    {
        var (_, _, unit) = await NewUnitAsync(2);
        await unit.RemoveBedAsync("B2");
        InpatientUnitState state = await unit.GetAsync();
        Assert.That(state.Beds, Has.Count.EqualTo(1));
        Assert.That(state.Beds[0].BedId, Is.EqualTo("B1"));
    }

    [Test]
    public async Task DeactivateUnit_WithOccupantsOrBoarders_Throws()
    {
        var (_, _, occupiedUnit) = await NewUnitAsync(1);
        await occupiedUnit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1"));
        Assert.ThrowsAsync<InvalidOperationException>(() => occupiedUnit.DeactivateUnitAsync());

        var (_, _, boarderUnit) = await NewUnitAsync(1);
        await boarderUnit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", null));
        Assert.ThrowsAsync<InvalidOperationException>(() => boarderUnit.DeactivateUnitAsync());

        var (_, _, reservedUnit) = await NewUnitAsync(1);
        await reservedUnit.ReserveBedAsync("B1", $"PAT-{Guid.NewGuid()}", "EXPECTED,PATIENT", null);
        Assert.ThrowsAsync<InvalidOperationException>(() => reservedUnit.DeactivateUnitAsync());
    }

    [Test]
    public async Task DeactivateUnit_Empty_RemovesFromDirectory_AndRejectsAdmissions()
    {
        var (inst, unitId, unit) = await NewUnitAsync(1);
        await unit.DeactivateUnitAsync();

        List<UnitCapacitySummary> directory = await Capacity(inst).GetUnitsAsync();
        Assert.That(directory.Any(u => u.UnitId == unitId), Is.False);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "B1")));
    }

    [Test]
    public async Task SetChargeNurse_Persists()
    {
        var (_, _, unit) = await NewUnitAsync(1);
        await unit.SetChargeNurseAsync("RN-CHARGE", "CHARGE,NURSE RN");

        InpatientUnitState state = await unit.GetAsync();
        Assert.That(state.ChargeNurseId, Is.EqualTo("RN-CHARGE"));
        Assert.That(state.ChargeNurseName, Is.EqualTo("CHARGE,NURSE RN"));
    }
}
