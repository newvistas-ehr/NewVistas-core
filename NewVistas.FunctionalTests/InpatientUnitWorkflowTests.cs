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
/// Workflow-level end-to-end tests for the unit-owns-beds inpatient model:
/// IPatientWorkflowGrain.RecordAdmissionAsync / RecordTransferAsync / RecordDischargeAsync
/// composing IInpatientUnitGrain (bed truth), IAdtGrain (movement history), the census
/// projection, and the ADR-002 relationship auto-establishment (attending physician on
/// admission; attending nurse via UnitCoverage).
/// </summary>
[TestFixture]
public class InpatientUnitWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IInpatientUnitGrain Unit(string institutionId, string unitId)
        => _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    private INewPersonGrain Staff(string userId)
        => _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");

    private IPersonGrain Person(string personId)
        => _cluster.GrainFactory.GetGrain<IPersonGrain>(personId);

    /// <summary>Fresh isolated unit with beds B1..Bn (no rooms).</summary>
    private async Task<(string Inst, string UnitId)> NewUnitAsync(int beds = 4, string name = "Test Unit")
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string unitId = $"U-{Guid.NewGuid():N}";
        IInpatientUnitGrain unit = Unit(inst, unitId);
        await unit.ConfigureUnitAsync(name, "MedSurg", "Internal Medicine");
        for (int i = 1; i <= beds; i++)
            await unit.AddBedAsync($"B{i}", null, BedType.Regular);
        return (inst, unitId);
    }

    private async Task<(IPatientWorkflowGrain Wf, string PatientId)> NewPatientAsync(string name = "STAY,PATIENT")
    {
        string pid = $"IPU-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        await wf.UpdateDemographicsAsync(name, "M", new DateTime(1975, 6, 1), null);
        return (wf, pid);
    }

    // A patient chart whose owner is also on staff → auto-flagged employee-patient
    // (sensitive), so plain access requires a treatment relationship or break-the-glass.
    // Mirrors PersonRelationshipCascadeTests.NewEmployeePatientAsync.
    private async Task<(IPatientWorkflowGrain Wf, string PatientId)> NewEmployeePatientAsync()
    {
        var (wf, pid) = await NewPatientAsync("NIGHTINGALE,NORA");
        string userId = $"STAFF-{Guid.NewGuid()}";
        await Staff(userId).UpdateProfileAsync("NIGHTINGALE,NORA", "Registered Nurse", "RN", "NURSING",
            "NURSE", "NURSE", "Medical-Surgical", "INST-500", "VA MEDICAL CENTER", "DIV-500", "MAIN DIVISION");
        string personId = await wf.CreateOrGetPersonForPatientAsync("500", PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        await Person(personId).LinkStaffAsync(userId, PersonLinkConfidence.ConfirmedByRegistration, "TEST");
        return (wf, pid);
    }

    private static InpatientBed GetBed(InpatientUnitState state, string bedId)
        => state.Beds.First(b => b.BedId == bedId);

    // ─── Admission ───────────────────────────────────────────────────────────

    [Test]
    public async Task Admission_OccupiesBed_RecordsMovement_AndShowsOnCensus()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, pid) = await NewPatientAsync("JONES,ROBERT");

        string movementId = await wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B1",
            "Internal Medicine", "DR-ADM", "Dr Admitting", "Pneumonia", null);
        Assert.That(movementId, Does.StartWith("ADT-"));

        // Bed truth: the admission itself occupied the bed — no separate board sync.
        InpatientBed bed = GetBed(await Unit(inst, unitId).GetAsync(), "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(bed.PatientId, Is.EqualTo(pid));
        Assert.That(bed.MovementId, Is.EqualTo(movementId));

        // ADT movement records institution/unit/bed.
        AdtState movement = await _cluster.GrainFactory.GetGrain<IAdtGrain>(movementId).GetMovementAsync();
        Assert.That(movement.TransactionType, Is.EqualTo("ADMISSION"));
        Assert.That(movement.InstitutionId, Is.EqualTo(inst));
        Assert.That(movement.WardLocationId, Is.EqualTo(unitId));
        Assert.That(movement.RoomBed, Is.EqualTo("B1"));

        // Census projection shows the patient in the bed.
        List<UnitCensusEntry> census = await wf.GetUnitCensusAsync(inst, unitId);
        UnitCensusEntry entry = census.Single(e => e.PatientId == pid);
        Assert.That(entry.BedId, Is.EqualTo("B1"));
        Assert.That(entry.PatientName, Is.EqualTo("JONES,ROBERT"));
        Assert.That(entry.MovementId, Is.EqualTo(movementId));
    }

    [Test]
    public async Task Admission_NullBedId_PatientAppearsAsBoarder()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, pid) = await NewPatientAsync("BOARDER,ED");

        await wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, null,
            "Internal Medicine", null, null, "Awaiting bed", null);

        List<UnitCensusEntry> census = await wf.GetUnitCensusAsync(inst, unitId);
        UnitCensusEntry entry = census.Single(e => e.PatientId == pid);
        Assert.That(entry.BedId, Is.Null, "A boarder has no bed but is still on the honest census.");

        InpatientUnitState state = await Unit(inst, unitId).GetAsync();
        Assert.That(state.Boarders.Any(b => b.PatientId == pid), Is.True);
        Assert.That(state.Beds.All(b => b.State == BedLifecycleState.Available), Is.True);
    }

    [Test]
    public async Task Admission_AssignBed_MovesBoarderIntoBed()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, pid) = await NewPatientAsync("BOARDER,PLACED");
        await wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, null, null, null, null, null, null);

        await Unit(inst, unitId).AssignBedAsync(pid, "B2", overrideReservation: false);

        InpatientUnitState state = await Unit(inst, unitId).GetAsync();
        Assert.That(state.Boarders, Is.Empty);
        InpatientBed bed = GetBed(state, "B2");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(bed.PatientId, Is.EqualTo(pid));

        List<UnitCensusEntry> census = await wf.GetUnitCensusAsync(inst, unitId);
        Assert.That(census.Single(e => e.PatientId == pid).BedId, Is.EqualTo("B2"));
    }

    [Test]
    public async Task Admission_NonexistentUnit_Throws()
    {
        var (wf, _) = await NewPatientAsync();
        string inst = $"INST-{Guid.NewGuid():N}";

        Assert.ThrowsAsync<InvalidOperationException>(() => wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, $"U-{Guid.NewGuid():N}", null,
            "Medicine", null, null, "dx", null));
    }

    [Test]
    public async Task Admission_ToOccupiedBed_Throws_AndLeavesNoMovement()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf1, _) = await NewPatientAsync("FIRST,PATIENT");
        await wf1.RecordAdmissionAsync(DateTime.UtcNow, inst, unitId, "B1",
            null, null, null, null, null);

        var (wf2, _) = await NewPatientAsync("SECOND,PATIENT");
        Assert.ThrowsAsync<InvalidOperationException>(() => wf2.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B1", null, null, null, null, null));

        // Clean failure: the rejected placement never wrote an ADT movement.
        List<AdtSummary> movements = await wf2.GetAdtMovementsAsync();
        Assert.That(movements, Is.Empty);
    }

    // ─── Discharge ───────────────────────────────────────────────────────────

    [Test]
    public async Task Discharge_ReleasesBedToDirty_AndCensusEmpties()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, pid) = await NewPatientAsync("LEAVING,PATIENT");
        string movementId = await wf.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-3), inst, unitId, "B1",
            "Internal Medicine", null, null, "CHF", null);

        await wf.RecordDischargeAsync(movementId, DateTime.UtcNow, "CHF improved", "REGULAR", null);

        InpatientBed bed = GetBed(await Unit(inst, unitId).GetAsync(), "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Dirty), "A vacated bed needs EVS turnover, never straight to Available.");
        Assert.That(bed.PatientId, Is.Null);

        List<UnitCensusEntry> census = await wf.GetUnitCensusAsync(inst, unitId);
        Assert.That(census.Any(e => e.PatientId == pid), Is.False);

        List<AdtSummary> movements = await wf.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("DISCHARGED"));
    }

    // ─── Transfer ────────────────────────────────────────────────────────────

    [Test]
    public async Task Transfer_CrossUnit_OldBedDirty_NewBedOccupied_CensusesCorrect()
    {
        var (instA, unitA) = await NewUnitAsync(name: "Unit A");
        var (instB, unitB) = await NewUnitAsync(name: "Unit B");
        var (wf, pid) = await NewPatientAsync("MOVING,PATIENT");
        string admitId = await wf.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), instA, unitA, "B1",
            "Internal Medicine", null, null, "Observation", null);

        string transferId = await wf.RecordTransferAsync(
            admitId, DateTime.UtcNow, instB, unitB, "B1",
            null, "Critical Care", "DR-ICU", "Dr Intensivist", "Deteriorating");
        Assert.That(transferId, Does.StartWith("ADT-").And.Not.EqualTo(admitId));

        InpatientBed oldBed = GetBed(await Unit(instA, unitA).GetAsync(), "B1");
        InpatientBed newBed = GetBed(await Unit(instB, unitB).GetAsync(), "B1");
        Assert.That(oldBed.State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(oldBed.PatientId, Is.Null);
        Assert.That(newBed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(newBed.PatientId, Is.EqualTo(pid));

        List<UnitCensusEntry> censusA = await wf.GetUnitCensusAsync(instA, unitA);
        List<UnitCensusEntry> censusB = await wf.GetUnitCensusAsync(instB, unitB);
        Assert.That(censusA.Any(e => e.PatientId == pid), Is.False, "Source census must not retain the patient.");
        Assert.That(censusB.Any(e => e.PatientId == pid), Is.True, "Destination census must show the patient.");

        List<AdtSummary> movements = await wf.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements[0].MovementType, Is.EqualTo("TRANSFER"));
    }

    [Test]
    public async Task Transfer_SameUnit_BedSwap_OldBedDirty()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, pid) = await NewPatientAsync("SWAPPING,PATIENT");
        string admitId = await wf.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), inst, unitId, "B1",
            "Internal Medicine", null, null, null, null);

        await wf.RecordTransferAsync(
            admitId, DateTime.UtcNow, inst, unitId, "B2",
            null, null, null, null, "Roommate conflict");

        InpatientUnitState state = await Unit(inst, unitId).GetAsync();
        Assert.That(GetBed(state, "B1").State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(GetBed(state, "B2").State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(GetBed(state, "B2").PatientId, Is.EqualTo(pid));

        // Exactly one census row — the swap is atomic within the unit.
        List<UnitCensusEntry> census = await wf.GetUnitCensusAsync(inst, unitId);
        Assert.That(census.Count(e => e.PatientId == pid), Is.EqualTo(1));
        Assert.That(census.Single(e => e.PatientId == pid).BedId, Is.EqualTo("B2"));
    }

    // ─── ADR-002 — relationship auto-establishment ───────────────────────────

    [Test]
    public async Task Admission_EstablishesAttendingRelationship_OnPac()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, _) = await NewEmployeePatientAsync();   // sensitive employee-patient chart
        string attendingId = $"DR-ATT-{Guid.NewGuid():N}";

        await wf.RecordAdmissionAsync(DateTime.UtcNow, inst, unitId, "B1",
            "Medicine", attendingId, "Dr Attending", "pneumonia", null);

        PatientAccessDecision d = await wf.AccessPatientAsync(attendingId, "Dr Attending",
            breakTheGlassAttested: false, justification: null);

        Assert.That(d.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.False);
    }

    [Test]
    public async Task Admission_WithAttendingNurse_EstablishesUnitCoverage_OnSensitiveChart()
    {
        var (inst, unitId) = await NewUnitAsync();
        var (wf, pid) = await NewEmployeePatientAsync();   // sensitive employee-patient chart

        // The covering nurse "ends up in the room": named as the bed's attending nurse at admit.
        await Unit(inst, unitId).AdmitPatientAsync(new UnitAdmissionRequest
        {
            PatientId = pid,
            PatientName = "NIGHTINGALE,NORA",
            MovementId = $"ADT-{Guid.NewGuid()}",
            BedId = "B1",
            AdmitDate = DateTime.UtcNow,
            AttendingNurseId = "RN-COVER",
            AttendingNurseName = "Covering Nurse RN"
        });

        PatientAccessDecision nurse = await wf.AccessPatientAsync("RN-COVER", "Covering Nurse RN",
            breakTheGlassAttested: false, justification: null);
        PatientAccessDecision other = await wf.AccessPatientAsync("RN-OTHER", "Other Nurse RN",
            breakTheGlassAttested: false, justification: null);

        Assert.That(nurse.Outcome, Is.EqualTo(PatientAccessOutcome.AllowedByRelationship));
        Assert.That(nurse.WasBreakTheGlass, Is.False);
        Assert.That(other.Outcome, Is.EqualTo(PatientAccessOutcome.RequiresBreakTheGlass));
    }

    // ─── Small-site collapse ─────────────────────────────────────────────────

    [Test]
    public async Task SmallSite_SingleUnitNoRooms_AdmitDischargeEvsTurn_EndToEnd()
    {
        // One unit "MAIN", 4 beds, no rooms — the whole hospital.
        string inst = $"INST-{Guid.NewGuid():N}";
        IInpatientUnitGrain main = Unit(inst, "MAIN");
        await main.ConfigureUnitAsync("Main Unit", null, null);
        for (int i = 1; i <= 4; i++)
            await main.AddBedAsync($"{i}", null, BedType.Regular);

        var (wf, pid) = await NewPatientAsync("SMALLSITE,PATIENT");

        // Admit into bed 2.
        string movementId = await wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, "MAIN", "2", null, null, null, "Cellulitis", null);
        InpatientUnitState state = await main.GetAsync();
        Assert.That(GetBed(state, "2").State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(GetBed(state, "2").RoomId, Is.Empty, "Bed-only mode — no room modeling.");

        // Discharge → Dirty; one-click EVS turn → Available.
        await wf.RecordDischargeAsync(movementId, DateTime.UtcNow, "Resolved", "HOME", null);
        Assert.That(GetBed(await main.GetAsync(), "2").State, Is.EqualTo(BedLifecycleState.Dirty));

        await main.MarkBedCleanAsync("2", "CLERK,ONLY");
        Assert.That(GetBed(await main.GetAsync(), "2").State, Is.EqualTo(BedLifecycleState.Available));

        // Capacity board reflects the fully turned unit.
        (int total, int available, int occupied) = await wf.GetBedCountsAsync(inst);
        Assert.That(total, Is.EqualTo(4));
        Assert.That(available, Is.EqualTo(4));
        Assert.That(occupied, Is.EqualTo(0));
    }
}
