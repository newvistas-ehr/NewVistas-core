// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Emergency Department Integration Software (EDIS).
/// Tests ED visit registration, triage, bed assignment, tracking board,
/// and disposition workflows.
/// </summary>
[TestFixture]
public class EdisWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IEdVisitGrain NewVisit()
        => _cluster.GrainFactory.GetGrain<IEdVisitGrain>($"ED-VISIT:{Guid.NewGuid():N}");

    private IEdBoardGrain GetBoard()
        => _cluster.GrainFactory.GetGrain<IEdBoardGrain>("ED-BOARD:TEST");

    // ─── Registration ───────────────────────────────────────────────────────────

    [Test]
    public async Task RegisterArrival_SetsPatientData()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-001", "JONES,MICHAEL R", "Chest pain", "AMBULANCE", DateTime.UtcNow);
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.PatientName, Is.EqualTo("JONES,MICHAEL R"));
        Assert.That(state.ChiefComplaint, Is.EqualTo("Chest pain"));
        Assert.That(state.ArrivalMode, Is.EqualTo("AMBULANCE"));
        Assert.That(state.Status, Is.EqualTo("WAITING"));
    }

    [Test]
    public async Task RegisterArrival_SetsDefaultStatus()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-002", "SMITH,SARAH L", "Laceration", "WALK_IN", DateTime.UtcNow);
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("WAITING"));
    }

    // ─── Triage ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Triage_SetsAcuityAndNurse()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-003", "WILLIAMS,JAMES T", "SOB", "WALK_IN", DateTime.UtcNow);
        await visit.TriageAsync(3, "NURSE-001", "Triage Nurse");
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.AcuityLevel, Is.EqualTo(3));
        Assert.That(state.TriageNurseName, Is.EqualTo("Triage Nurse"));
        Assert.That(state.Status, Is.EqualTo("TRIAGED"));
        Assert.That(state.TriageDateTime, Is.Not.Null);
    }

    // ─── Bed Assignment ─────────────────────────────────────────────────────────

    [Test]
    public async Task AssignBed_SetsBedAndChangesStatus()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-004", "BROWN,PATRICIA A", "Fall", "AMBULANCE", DateTime.UtcNow);
        await visit.TriageAsync(3, "NURSE-001", "Triage Nurse");
        await visit.AssignBedAsync("ED-BAY-5");
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.AssignedBed, Is.EqualTo("ED-BAY-5"));
        Assert.That(state.Status, Is.EqualTo("IN_TREATMENT"));
        Assert.That(state.BedAssignedDateTime, Is.Not.Null);
    }

    // ─── Staff Assignment ───────────────────────────────────────────────────────

    [Test]
    public async Task AssignPhysician_SetsPhysicianInfo()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-005", "DAVIS,ROBERT K", "Abd pain", "WALK_IN", DateTime.UtcNow);
        await visit.AssignPhysicianAsync("PROV-ED-001", "Dr. Martinez");
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.AttendingPhysicianName, Is.EqualTo("Dr. Martinez"));
    }

    [Test]
    public async Task PhysicianSeen_RecordsTimestamp()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-006", "MILLER,JENNIFER M", "Headache", "AMBULANCE", DateTime.UtcNow);
        await visit.RecordPhysicianSeenAsync();
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.PhysicianSeenDateTime, Is.Not.Null);
    }

    // ─── Disposition ────────────────────────────────────────────────────────────

    [Test]
    public async Task Discharge_SetsDispositionAndStatus()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-007", "GARCIA,MARIA T", "Ankle sprain", "WALK_IN", DateTime.UtcNow);
        await visit.AssignBedAsync("FAST-TRACK-2");
        await visit.DischargeAsync("Ankle sprain, no fracture");
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("DISCHARGED"));
        Assert.That(state.Disposition, Is.EqualTo("DISCHARGE"));
        Assert.That(state.DischargeDiagnosis, Is.EqualTo("Ankle sprain, no fracture"));
    }

    [Test]
    public async Task Admit_SetsAdmitDestination()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-008", "WILSON,THOMAS E", "STEMI", "AMBULANCE", DateTime.UtcNow);
        await visit.AdmitAsync("WARD-CCU-1");
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("ADMITTED"));
        Assert.That(state.AdmitDestination, Is.EqualTo("WARD-CCU-1"));
    }

    [Test]
    public async Task Lwbs_SetsStatus()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-009", "TAYLOR,LISA M", "Sore throat", "WALK_IN", DateTime.UtcNow);
        await visit.RecordLwbsAsync();
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("LWBS"));
    }

    [Test]
    public async Task Ama_SetsStatus()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-010", "ANDERSON,KAREN J", "Back pain", "WALK_IN", DateTime.UtcNow);
        await visit.RecordAmaAsync();
        EdVisitState state = await visit.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("AMA"));
        Assert.That(state.Disposition, Is.EqualTo("AMA"));
    }

    // ─── Tracking Board ─────────────────────────────────────────────────────────

    [Test]
    public async Task Board_AddAndRetrieveVisits()
    {
        IEdBoardGrain board = GetBoard();
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "V1", PatientName = "TEST,ONE", ChiefComplaint = "Test",
            AcuityLevel = 3, Status = "WAITING", ArrivalDateTime = DateTime.UtcNow
        });
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "V2", PatientName = "TEST,TWO", ChiefComplaint = "Test 2",
            AcuityLevel = 2, Status = "IN_TREATMENT", AssignedBed = "BAY-1",
            ArrivalDateTime = DateTime.UtcNow
        });

        List<EdBoardEntry> visits = await board.GetActiveVisitsAsync();
        Assert.That(visits, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(visits[0].AcuityLevel, Is.LessThanOrEqualTo(visits[1].AcuityLevel));
    }

    [Test]
    public async Task Board_WaitingCount()
    {
        IEdBoardGrain board = _cluster.GrainFactory.GetGrain<IEdBoardGrain>("ED-BOARD:COUNT-TEST");
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "W1", PatientName = "WAIT,ONE", ChiefComplaint = "Test",
            AcuityLevel = 4, Status = "WAITING", ArrivalDateTime = DateTime.UtcNow
        });
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "W2", PatientName = "TREAT,ONE", ChiefComplaint = "Test",
            AcuityLevel = 3, Status = "IN_TREATMENT", AssignedBed = "BAY-2",
            ArrivalDateTime = DateTime.UtcNow
        });

        int waiting = await board.GetWaitingCountAsync();
        Assert.That(waiting, Is.EqualTo(1));
    }

    [Test]
    public async Task Board_RemoveVisitOnDisposition()
    {
        IEdBoardGrain board = _cluster.GrainFactory.GetGrain<IEdBoardGrain>("ED-BOARD:REMOVE-TEST");
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "R1", PatientName = "REMOVE,ME", ChiefComplaint = "Test",
            AcuityLevel = 5, Status = "WAITING", ArrivalDateTime = DateTime.UtcNow
        });
        await board.RemoveVisitAsync("R1");

        List<EdBoardEntry> visits = await board.GetActiveVisitsAsync();
        Assert.That(visits.Any(v => v.VisitId == "R1"), Is.False);
    }

    [Test]
    public async Task Board_OccupiedBedCount()
    {
        IEdBoardGrain board = _cluster.GrainFactory.GetGrain<IEdBoardGrain>("ED-BOARD:BED-TEST");
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "B1", PatientName = "BED,ONE", AcuityLevel = 3,
            Status = "IN_TREATMENT", AssignedBed = "BAY-1", ArrivalDateTime = DateTime.UtcNow
        });
        await board.AddOrUpdateVisitAsync(new EdBoardEntry
        {
            VisitId = "B2", PatientName = "BED,TWO", AcuityLevel = 4,
            Status = "WAITING", ArrivalDateTime = DateTime.UtcNow
        });

        int occupied = await board.GetOccupiedBedCountAsync();
        Assert.That(occupied, Is.EqualTo(1));
    }

    // ─── Full Workflow ──────────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_ArrivalToDischarge()
    {
        IEdVisitGrain visit = NewVisit();
        await visit.RegisterArrivalAsync("PAT-FULL", "WORKFLOW,FULL", "Chest pain", "AMBULANCE", DateTime.UtcNow);
        EdVisitState s1 = await visit.GetVisitAsync();
        Assert.That(s1.Status, Is.EqualTo("WAITING"));

        await visit.TriageAsync(2, "NURSE-001", "Triage Nurse");
        await visit.AssignBedAsync("TRAUMA-1");
        await visit.AssignPhysicianAsync("PROV-001", "Dr. Martinez");
        await visit.RecordPhysicianSeenAsync();

        EdVisitState s2 = await visit.GetVisitAsync();
        Assert.That(s2.Status, Is.EqualTo("IN_TREATMENT"));
        Assert.That(s2.AcuityLevel, Is.EqualTo(2));
        Assert.That(s2.AssignedBed, Is.EqualTo("TRAUMA-1"));
        Assert.That(s2.PhysicianSeenDateTime, Is.Not.Null);

        await visit.DischargeAsync("Acute coronary syndrome ruled out");
        EdVisitState s3 = await visit.GetVisitAsync();
        Assert.That(s3.Status, Is.EqualTo("DISCHARGED"));
        Assert.That(s3.DischargeDiagnosis, Is.EqualTo("Acute coronary syndrome ruled out"));
    }
}
