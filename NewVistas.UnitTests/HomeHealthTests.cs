// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

file class HBPCPatientGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("hbpcPatientStore");
    }
}

file class HBPCRegistryGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("hbpcRegistryStore");
    }
}

file class HHCVisitGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("hhcVisitStore");
    }
}

file class HHCVisitIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("hhcVisitIndexStore");
    }
}

file class HomeHealthIntegrationSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("hbpcPatientStore");
        siloBuilder.AddMemoryGrainStorage("hbpcRegistryStore");
        siloBuilder.AddMemoryGrainStorage("hhcVisitStore");
        siloBuilder.AddMemoryGrainStorage("hhcVisitIndexStore");
    }
}

// ── HBPCPatientGrain Tests ────────────────────────────────────────────────────

[TestFixture]
public class HBPCPatientGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task HBPCPatientGrain_CanEnrollPatient()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Smith, John", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "CHF", "Jane Smith", "123 Main St");

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("Smith, John"));
        Assert.That(state.LevelOfCare, Is.EqualTo(HBPCLevelOfCare.Basic));
        Assert.That(state.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Active));
        Assert.That(state.TotalVisitsThisYear, Is.EqualTo(0));
    }

    [Test]
    public async Task HBPCPatientGrain_CanUpdateLevelOfCare()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Doe, Jane", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "COPD", "Bob Doe", "456 Oak Ave");
        await grain.UpdateLevelOfCareAsync(HBPCLevelOfCare.Palliative);

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.LevelOfCare, Is.EqualTo(HBPCLevelOfCare.Palliative));
    }

    [Test]
    public async Task HBPCPatientGrain_CanAddGoalWithoutDuplicates()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Brown, Bob", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "Diabetes", "Alice Brown", "789 Pine Rd");
        await grain.AddGoalAsync("Improve ambulation");
        await grain.AddGoalAsync("Medication adherence");
        await grain.AddGoalAsync("Improve ambulation"); // duplicate

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Goals, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task HBPCPatientGrain_CanAddSecondaryDiagnosis()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Clark, Alice", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "HTN", "Tom Clark", "321 Elm St");
        await grain.AddSecondaryDiagnosisAsync("Type 2 Diabetes");
        await grain.AddSecondaryDiagnosisAsync("CKD Stage 3");

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.SecondaryDiagnoses, Has.Count.EqualTo(2));
        Assert.That(state.SecondaryDiagnoses, Contains.Item("Type 2 Diabetes"));
    }

    [Test]
    public async Task HBPCPatientGrain_CanSuspendAndReactivate()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Evans, Tom", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "CHF", "Mary Evans", "654 Maple Dr");
        await grain.SuspendEnrollmentAsync();

        HBPCPatientState suspended = await grain.GetPatientAsync();
        Assert.That(suspended.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Suspended));

        await grain.ReactivateEnrollmentAsync();
        HBPCPatientState reactivated = await grain.GetPatientAsync();
        Assert.That(reactivated.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Active));
    }

    [Test]
    public async Task HBPCPatientGrain_CanRecordVisit()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Ford, Ann", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "COPD", "Ed Ford", "789 Cedar Ln");
        DateTime visitDate = DateTime.UtcNow.AddDays(-2);
        DateTime nextVisit = DateTime.UtcNow.AddDays(14);
        await grain.RecordVisitAsync(visitDate, nextVisit);
        await grain.RecordVisitAsync(DateTime.UtcNow.AddDays(-1), null);

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.TotalVisitsThisYear, Is.EqualTo(2));
        Assert.That(state.LastVisitDate, Is.Not.Null);
    }

    [Test]
    public async Task HBPCPatientGrain_CanDischargePatient()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain grain = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

        await grain.EnrollPatientAsync(patientId, "Grant, Bob", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "HTN", "Sue Grant", "12 Birch Ave");
        await grain.DischargePatientAsync(HBPCDischargeReason.GoalsMet, "Goals achieved.");

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Discharged));
        Assert.That(state.DischargeReason, Is.EqualTo(HBPCDischargeReason.GoalsMet));
        Assert.That(state.DischargeDate, Is.Not.Null);
    }
}

// ── HBPCRegistryGrain Tests ───────────────────────────────────────────────────

[TestFixture]
public class HBPCRegistryGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IHBPCRegistryGrain Registry()
        => _cluster.GrainFactory.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY");

    private HBPCRegistryEntry MakeEntry(string patientId, HBPCProgramStatus status = HBPCProgramStatus.Active,
        HBPCLevelOfCare level = HBPCLevelOfCare.Basic,
        DateTime? lastVisit = null, DateTime? nextVisit = null)
        => new()
        {
            PatientId = patientId,
            PatientName = $"Patient {patientId}",
            EnrollmentDate = DateTime.UtcNow.AddMonths(-6),
            LevelOfCare = level,
            ProgramStatus = status,
            PrimaryDiagnosis = "CHF",
            LastVisitDate = lastVisit,
            NextScheduledVisit = nextVisit,
            TotalVisitsThisYear = 3
        };

    [Test]
    public async Task HBPCRegistryGrain_CanUpsertAndGetAllPatients()
    {
        IHBPCRegistryGrain registry = Registry();
        string id1 = $"REG-{Guid.NewGuid()}";
        string id2 = $"REG-{Guid.NewGuid()}";

        await registry.UpsertPatientAsync(MakeEntry(id1));
        await registry.UpsertPatientAsync(MakeEntry(id2));

        List<HBPCRegistryEntry> all = await registry.GetAllPatientsAsync();
        Assert.That(all.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(all.Any(p => p.PatientId == id1), Is.True);
        Assert.That(all.Any(p => p.PatientId == id2), Is.True);
    }

    [Test]
    public async Task HBPCRegistryGrain_CanGetActivePatients()
    {
        IHBPCRegistryGrain registry = Registry();
        string activeId = $"REG-{Guid.NewGuid()}";
        string dischargedId = $"REG-{Guid.NewGuid()}";

        await registry.UpsertPatientAsync(MakeEntry(activeId, HBPCProgramStatus.Active));
        await registry.UpsertPatientAsync(MakeEntry(dischargedId, HBPCProgramStatus.Discharged));

        List<HBPCRegistryEntry> active = await registry.GetActivePatientsAsync();
        Assert.That(active.Any(p => p.PatientId == activeId), Is.True);
        Assert.That(active.Any(p => p.PatientId == dischargedId), Is.False);
    }

    [Test]
    public async Task HBPCRegistryGrain_CanGetPatientsByLevelOfCare()
    {
        IHBPCRegistryGrain registry = Registry();
        string palId = $"REG-{Guid.NewGuid()}";
        string basicId = $"REG-{Guid.NewGuid()}";

        await registry.UpsertPatientAsync(MakeEntry(palId, level: HBPCLevelOfCare.Palliative));
        await registry.UpsertPatientAsync(MakeEntry(basicId, level: HBPCLevelOfCare.Basic));

        List<HBPCRegistryEntry> palliative = await registry.GetPatientsByLevelOfCareAsync(HBPCLevelOfCare.Palliative);
        Assert.That(palliative.Any(p => p.PatientId == palId), Is.True);
        Assert.That(palliative.Any(p => p.PatientId == basicId), Is.False);
    }

    [Test]
    public async Task HBPCRegistryGrain_CanGetPatientsWithUpcomingVisits()
    {
        IHBPCRegistryGrain registry = Registry();
        string upcomingId = $"REG-{Guid.NewGuid()}";
        string farId = $"REG-{Guid.NewGuid()}";

        await registry.UpsertPatientAsync(MakeEntry(upcomingId, nextVisit: DateTime.UtcNow.AddDays(3)));
        await registry.UpsertPatientAsync(MakeEntry(farId, nextVisit: DateTime.UtcNow.AddDays(30)));

        List<HBPCRegistryEntry> upcoming = await registry.GetPatientsWithUpcomingVisitsAsync(7);
        Assert.That(upcoming.Any(p => p.PatientId == upcomingId), Is.True);
        Assert.That(upcoming.Any(p => p.PatientId == farId), Is.False);
    }

    [Test]
    public async Task HBPCRegistryGrain_CanGetPatientsWithNoRecentVisit()
    {
        IHBPCRegistryGrain registry = Registry();
        string staleId = $"REG-{Guid.NewGuid()}";
        string recentId = $"REG-{Guid.NewGuid()}";
        string neverVisitedId = $"REG-{Guid.NewGuid()}";

        await registry.UpsertPatientAsync(MakeEntry(staleId, lastVisit: DateTime.UtcNow.AddDays(-45)));
        await registry.UpsertPatientAsync(MakeEntry(recentId, lastVisit: DateTime.UtcNow.AddDays(-5)));
        await registry.UpsertPatientAsync(MakeEntry(neverVisitedId, lastVisit: null));

        List<HBPCRegistryEntry> noRecent = await registry.GetPatientsWithNoRecentVisitAsync(30);
        Assert.That(noRecent.Any(p => p.PatientId == staleId), Is.True);
        Assert.That(noRecent.Any(p => p.PatientId == neverVisitedId), Is.True);
        Assert.That(noRecent.Any(p => p.PatientId == recentId), Is.False);
    }

    [Test]
    public async Task HBPCRegistryGrain_UpsertUpdatesExistingEntry()
    {
        IHBPCRegistryGrain registry = Registry();
        string id = $"REG-{Guid.NewGuid()}";

        await registry.UpsertPatientAsync(MakeEntry(id, level: HBPCLevelOfCare.Basic));
        HBPCRegistryEntry updated = MakeEntry(id, level: HBPCLevelOfCare.Enhanced);
        await registry.UpsertPatientAsync(updated);

        List<HBPCRegistryEntry> all = await registry.GetAllPatientsAsync();
        List<HBPCRegistryEntry> byId = all.Where(p => p.PatientId == id).ToList();
        Assert.That(byId, Has.Count.EqualTo(1));
        Assert.That(byId[0].LevelOfCare, Is.EqualTo(HBPCLevelOfCare.Enhanced));
    }
}

// ── HHCVisitGrain Tests ───────────────────────────────────────────────────────

[TestFixture]
public class HHCVisitGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task HHCVisitGrain_CanScheduleVisit()
    {
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain grain = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-001", "Smith, John", DateTime.UtcNow.AddDays(3),
            HHCVisitDiscipline.Nursing, HHCVisitType.Routine,
            "CLIN-001", "Jones, RN", "Routine nursing visit.");

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.VisitId, Is.EqualTo(visitId));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.Discipline, Is.EqualTo(HHCVisitDiscipline.Nursing));
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.Scheduled));
    }

    [Test]
    public async Task HHCVisitGrain_CanCompleteVisit()
    {
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain grain = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-002", "Brown, Jane", DateTime.UtcNow,
            HHCVisitDiscipline.PhysicalTherapy, HHCVisitType.Routine,
            "CLIN-002", "Lee, PT", "PT evaluation.");

        await grain.CompleteVisitAsync(
            60, "BP 130/80, HR 72", new List<string> { "Gait training", "Balance exercises" },
            "Good patient tolerance", "Ambulating with walker 50 feet",
            DateTime.UtcNow.AddDays(7), "Follow up with PT.");

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.Completed));
        Assert.That(state.DurationMinutes, Is.EqualTo(60));
        Assert.That(state.Interventions, Has.Count.EqualTo(2));
        Assert.That(state.VitalSigns, Is.EqualTo("BP 130/80, HR 72"));
    }

    [Test]
    public async Task HHCVisitGrain_CanCancelVisit()
    {
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain grain = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-003", "Clark, Bob", DateTime.UtcNow.AddDays(2),
            HHCVisitDiscipline.SocialWork, HHCVisitType.Admission,
            "CLIN-003", "Davis, MSW", "Initial SW assessment.");
        await grain.CancelVisitAsync("Patient hospitalized.");

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient hospitalized."));
    }

    [Test]
    public async Task HHCVisitGrain_CanMarkNoAnswer()
    {
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain grain = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-004", "Evans, Sue", DateTime.UtcNow,
            HHCVisitDiscipline.Nursing, HHCVisitType.Routine,
            "CLIN-001", "Jones, RN", "Follow-up.");
        await grain.MarkNoAnswerAsync();

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.NoAnswer));
    }

    [Test]
    public async Task HHCVisitGrain_CanMarkPatientRefused()
    {
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain grain = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-005", "Ford, Ken", DateTime.UtcNow.AddDays(1),
            HHCVisitDiscipline.Dietitian, HHCVisitType.Routine,
            "CLIN-004", "Green, RD", "Diet consult.");
        await grain.MarkPatientRefusedAsync("Patient did not want visit today.");

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.PatientRefused));
        Assert.That(state.Notes, Is.EqualTo("Patient did not want visit today."));
    }
}

// ── HHCVisitIndexGrain Tests ──────────────────────────────────────────────────

[TestFixture]
public class HHCVisitIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IHHCVisitIndexGrain Index(string patientId)
        => _cluster.GrainFactory.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{patientId}");

    private HHCVisitIndexEntry MakeVisitEntry(string visitId, HHCVisitStatus status, DateTime visitDate,
        HHCVisitDiscipline discipline = HHCVisitDiscipline.Nursing)
        => new()
        {
            VisitId = visitId,
            PatientId = "PAT-IDX-001",
            PatientName = "Smith, John",
            VisitDate = visitDate,
            Discipline = discipline,
            VisitType = HHCVisitType.Routine,
            Status = status,
            ClinicianName = "Jones, RN",
            DurationMinutes = status == HHCVisitStatus.Completed ? 45 : 0
        };

    [Test]
    public async Task HHCVisitIndexGrain_CanUpsertAndGetAllVisits()
    {
        string patientId = $"IDX-PAT-{Guid.NewGuid()}";
        IHHCVisitIndexGrain index = Index(patientId);

        string v1 = $"HHC-VISIT:{Guid.NewGuid()}";
        string v2 = $"HHC-VISIT:{Guid.NewGuid()}";
        await index.UpsertVisitAsync(MakeVisitEntry(v1, HHCVisitStatus.Completed, DateTime.UtcNow.AddDays(-7)));
        await index.UpsertVisitAsync(MakeVisitEntry(v2, HHCVisitStatus.Scheduled, DateTime.UtcNow.AddDays(3)));

        List<HHCVisitIndexEntry> all = await index.GetAllVisitsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        // Ordered by date descending — upcoming visit first
        Assert.That(all[0].VisitId, Is.EqualTo(v2));
    }

    [Test]
    public async Task HHCVisitIndexGrain_CanGetVisitsByDiscipline()
    {
        string patientId = $"IDX-PAT-{Guid.NewGuid()}";
        IHHCVisitIndexGrain index = Index(patientId);

        string nursingId = $"HHC-VISIT:{Guid.NewGuid()}";
        string ptId = $"HHC-VISIT:{Guid.NewGuid()}";
        await index.UpsertVisitAsync(MakeVisitEntry(nursingId, HHCVisitStatus.Completed, DateTime.UtcNow.AddDays(-5), HHCVisitDiscipline.Nursing));
        await index.UpsertVisitAsync(MakeVisitEntry(ptId, HHCVisitStatus.Completed, DateTime.UtcNow.AddDays(-3), HHCVisitDiscipline.PhysicalTherapy));

        List<HHCVisitIndexEntry> nursing = await index.GetVisitsByDisciplineAsync(HHCVisitDiscipline.Nursing);
        Assert.That(nursing, Has.Count.EqualTo(1));
        Assert.That(nursing[0].VisitId, Is.EqualTo(nursingId));
    }

    [Test]
    public async Task HHCVisitIndexGrain_CanGetUpcomingVisits()
    {
        string patientId = $"IDX-PAT-{Guid.NewGuid()}";
        IHHCVisitIndexGrain index = Index(patientId);

        string pastId = $"HHC-VISIT:{Guid.NewGuid()}";
        string futureId = $"HHC-VISIT:{Guid.NewGuid()}";
        await index.UpsertVisitAsync(MakeVisitEntry(pastId, HHCVisitStatus.Completed, DateTime.UtcNow.AddDays(-5)));
        await index.UpsertVisitAsync(MakeVisitEntry(futureId, HHCVisitStatus.Scheduled, DateTime.UtcNow.AddDays(5)));

        List<HHCVisitIndexEntry> upcoming = await index.GetUpcomingVisitsAsync();
        Assert.That(upcoming.Any(v => v.VisitId == futureId), Is.True);
        Assert.That(upcoming.Any(v => v.VisitId == pastId), Is.False);
    }

    [Test]
    public async Task HHCVisitIndexGrain_CanGetCompletedVisits()
    {
        string patientId = $"IDX-PAT-{Guid.NewGuid()}";
        IHHCVisitIndexGrain index = Index(patientId);

        string completedId = $"HHC-VISIT:{Guid.NewGuid()}";
        string cancelledId = $"HHC-VISIT:{Guid.NewGuid()}";
        await index.UpsertVisitAsync(MakeVisitEntry(completedId, HHCVisitStatus.Completed, DateTime.UtcNow.AddDays(-2)));
        await index.UpsertVisitAsync(MakeVisitEntry(cancelledId, HHCVisitStatus.Cancelled, DateTime.UtcNow.AddDays(-1)));

        List<HHCVisitIndexEntry> completed = await index.GetCompletedVisitsAsync();
        Assert.That(completed.Any(v => v.VisitId == completedId), Is.True);
        Assert.That(completed.Any(v => v.VisitId == cancelledId), Is.False);
    }

    [Test]
    public async Task HHCVisitIndexGrain_UpsertUpdatesExistingEntry()
    {
        string patientId = $"IDX-PAT-{Guid.NewGuid()}";
        IHHCVisitIndexGrain index = Index(patientId);

        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        await index.UpsertVisitAsync(MakeVisitEntry(visitId, HHCVisitStatus.Scheduled, DateTime.UtcNow.AddDays(2)));

        HHCVisitIndexEntry completed = MakeVisitEntry(visitId, HHCVisitStatus.Completed, DateTime.UtcNow.AddDays(2));
        await index.UpsertVisitAsync(completed);

        List<HHCVisitIndexEntry> all = await index.GetAllVisitsAsync();
        List<HHCVisitIndexEntry> forVisit = all.Where(v => v.VisitId == visitId).ToList();
        Assert.That(forVisit, Has.Count.EqualTo(1));
        Assert.That(forVisit[0].Status, Is.EqualTo(HHCVisitStatus.Completed));
    }
}

// ── HomeHealth Integration Tests ──────────────────────────────────────────────

[TestFixture]
public class HomeHealthIntegrationTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task HomeHealth_CanEnrollAndScheduleVisitWorkflow()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain patient = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");
        IHBPCRegistryGrain registry = _cluster.GrainFactory.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY-INT-1");
        IHHCVisitIndexGrain visitIndex = _cluster.GrainFactory.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{patientId}");

        // Enroll patient
        await patient.EnrollPatientAsync(patientId, "Hall, Mary", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "CHF", "Tom Hall", "555 Oak St");
        HBPCPatientState patientState = await patient.GetPatientAsync();
        await registry.UpsertPatientAsync(new HBPCRegistryEntry
        {
            PatientId = patientState.PatientId,
            PatientName = patientState.PatientName,
            EnrollmentDate = patientState.EnrollmentDate,
            LevelOfCare = patientState.LevelOfCare,
            ProgramStatus = patientState.ProgramStatus,
            PrimaryDiagnosis = patientState.PrimaryDiagnosis,
            LastVisitDate = patientState.LastVisitDate,
            NextScheduledVisit = patientState.NextScheduledVisit,
            TotalVisitsThisYear = patientState.TotalVisitsThisYear
        });

        // Schedule a visit
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain visit = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);
        await visit.ScheduleVisitAsync(patientId, "Hall, Mary", DateTime.UtcNow.AddDays(3),
            HHCVisitDiscipline.Nursing, HHCVisitType.Routine,
            "CLIN-001", "Jones, RN", "Initial nursing visit.");
        HHCVisitState visitState = await visit.GetVisitAsync();
        await visitIndex.UpsertVisitAsync(new HHCVisitIndexEntry
        {
            VisitId = visitState.VisitId,
            PatientId = visitState.PatientId,
            PatientName = visitState.PatientName,
            VisitDate = visitState.VisitDate,
            Discipline = visitState.Discipline,
            VisitType = visitState.VisitType,
            Status = visitState.Status,
            ClinicianName = visitState.ClinicianName,
            DurationMinutes = visitState.DurationMinutes
        });

        // Verify
        List<HHCVisitIndexEntry> upcoming = await visitIndex.GetUpcomingVisitsAsync();
        Assert.That(upcoming.Any(v => v.VisitId == visitId), Is.True);

        List<HBPCRegistryEntry> active = await registry.GetActivePatientsAsync();
        Assert.That(active.Any(p => p.PatientId == patientId), Is.True);
    }

    [Test]
    public async Task HomeHealth_CanCompleteVisitAndUpdatePatientRecord()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain patient = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");
        IHHCVisitIndexGrain visitIndex = _cluster.GrainFactory.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{patientId}");

        await patient.EnrollPatientAsync(patientId, "Irving, Carl", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "COPD", "Liz Irving", "88 Pine St");

        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        IHHCVisitGrain visit = _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(visitId);
        await visit.ScheduleVisitAsync(patientId, "Irving, Carl", DateTime.UtcNow,
            HHCVisitDiscipline.Nursing, HHCVisitType.Routine,
            "CLIN-001", "Jones, RN", "Follow-up.");
        await visit.CompleteVisitAsync(
            45, "SpO2 94%, RR 18", new List<string> { "Nebulizer treatment", "Breathing exercises" },
            "Patient comfortable", "SpO2 improving",
            DateTime.UtcNow.AddDays(14), "Continue current plan.");
        HHCVisitState visitState = await visit.GetVisitAsync();
        await visitIndex.UpsertVisitAsync(new HHCVisitIndexEntry
        {
            VisitId = visitState.VisitId,
            PatientId = visitState.PatientId,
            PatientName = visitState.PatientName,
            VisitDate = visitState.VisitDate,
            Discipline = visitState.Discipline,
            VisitType = visitState.VisitType,
            Status = visitState.Status,
            ClinicianName = visitState.ClinicianName,
            DurationMinutes = visitState.DurationMinutes
        });
        await patient.RecordVisitAsync(visitState.VisitDate, visitState.NextVisitDate);

        HBPCPatientState pState = await patient.GetPatientAsync();
        List<HHCVisitIndexEntry> completed = await visitIndex.GetCompletedVisitsAsync();

        Assert.That(pState.TotalVisitsThisYear, Is.EqualTo(1));
        Assert.That(pState.NextScheduledVisit, Is.Not.Null);
        Assert.That(completed.Any(v => v.VisitId == visitId), Is.True);
    }

    [Test]
    public async Task HomeHealth_RegistryReflectsMultiplePatients()
    {
        IHBPCRegistryGrain registry = _cluster.GrainFactory.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY-INT-2");

        string[] ids = Enumerable.Range(0, 4).Select(_ => $"PAT-{Guid.NewGuid()}").ToArray();
        await registry.UpsertPatientAsync(new HBPCRegistryEntry { PatientId = ids[0], PatientName = "A", EnrollmentDate = DateTime.UtcNow, LevelOfCare = HBPCLevelOfCare.Basic, ProgramStatus = HBPCProgramStatus.Active, PrimaryDiagnosis = "CHF" });
        await registry.UpsertPatientAsync(new HBPCRegistryEntry { PatientId = ids[1], PatientName = "B", EnrollmentDate = DateTime.UtcNow, LevelOfCare = HBPCLevelOfCare.Enhanced, ProgramStatus = HBPCProgramStatus.Active, PrimaryDiagnosis = "DM" });
        await registry.UpsertPatientAsync(new HBPCRegistryEntry { PatientId = ids[2], PatientName = "C", EnrollmentDate = DateTime.UtcNow, LevelOfCare = HBPCLevelOfCare.Palliative, ProgramStatus = HBPCProgramStatus.Active, PrimaryDiagnosis = "Cancer" });
        await registry.UpsertPatientAsync(new HBPCRegistryEntry { PatientId = ids[3], PatientName = "D", EnrollmentDate = DateTime.UtcNow, LevelOfCare = HBPCLevelOfCare.Basic, ProgramStatus = HBPCProgramStatus.Discharged, PrimaryDiagnosis = "HTN" });

        List<HBPCRegistryEntry> active = await registry.GetActivePatientsAsync();
        List<HBPCRegistryEntry> palliative = await registry.GetPatientsByLevelOfCareAsync(HBPCLevelOfCare.Palliative);

        Assert.That(active.Count(p => ids.Contains(p.PatientId)), Is.EqualTo(3));
        Assert.That(palliative.Any(p => p.PatientId == ids[2]), Is.True);
    }

    [Test]
    public async Task HomeHealth_DischargedPatientNotInActiveRegistry()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHBPCPatientGrain patient = _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");
        IHBPCRegistryGrain registry = _cluster.GrainFactory.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY-INT-3");

        await patient.EnrollPatientAsync(patientId, "Jones, Pat", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "HTN", "Sam Jones", "900 Birch Dr");
        await registry.UpsertPatientAsync(new HBPCRegistryEntry
        {
            PatientId = patientId, PatientName = "Jones, Pat",
            EnrollmentDate = DateTime.UtcNow, LevelOfCare = HBPCLevelOfCare.Enhanced,
            ProgramStatus = HBPCProgramStatus.Active, PrimaryDiagnosis = "HTN"
        });

        await patient.DischargePatientAsync(HBPCDischargeReason.PatientDeclined, "Patient declined services.");
        HBPCPatientState discharged = await patient.GetPatientAsync();
        await registry.UpsertPatientAsync(new HBPCRegistryEntry
        {
            PatientId = patientId, PatientName = "Jones, Pat",
            EnrollmentDate = DateTime.UtcNow, LevelOfCare = HBPCLevelOfCare.Enhanced,
            ProgramStatus = discharged.ProgramStatus, PrimaryDiagnosis = "HTN"
        });

        List<HBPCRegistryEntry> active = await registry.GetActivePatientsAsync();
        Assert.That(active.Any(p => p.PatientId == patientId), Is.False);
        Assert.That(discharged.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Discharged));
    }

    [Test]
    public async Task HomeHealth_MultiDisciplineVisitsTrackedSeparately()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IHHCVisitIndexGrain visitIndex = _cluster.GrainFactory.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{patientId}");

        DateTime now = DateTime.UtcNow;
        string nId = $"HHC-VISIT:{Guid.NewGuid()}";
        string ptId = $"HHC-VISIT:{Guid.NewGuid()}";
        string otId = $"HHC-VISIT:{Guid.NewGuid()}";

        await visitIndex.UpsertVisitAsync(new HHCVisitIndexEntry { VisitId = nId, PatientId = patientId, PatientName = "Test", VisitDate = now.AddDays(-3), Discipline = HHCVisitDiscipline.Nursing, VisitType = HHCVisitType.Routine, Status = HHCVisitStatus.Completed, ClinicianName = "Jones, RN" });
        await visitIndex.UpsertVisitAsync(new HHCVisitIndexEntry { VisitId = ptId, PatientId = patientId, PatientName = "Test", VisitDate = now.AddDays(-2), Discipline = HHCVisitDiscipline.PhysicalTherapy, VisitType = HHCVisitType.Routine, Status = HHCVisitStatus.Completed, ClinicianName = "Lee, PT" });
        await visitIndex.UpsertVisitAsync(new HHCVisitIndexEntry { VisitId = otId, PatientId = patientId, PatientName = "Test", VisitDate = now.AddDays(-1), Discipline = HHCVisitDiscipline.OccupationalTherapy, VisitType = HHCVisitType.Routine, Status = HHCVisitStatus.Completed, ClinicianName = "Kim, OT" });

        List<HHCVisitIndexEntry> nursing = await visitIndex.GetVisitsByDisciplineAsync(HHCVisitDiscipline.Nursing);
        List<HHCVisitIndexEntry> pt = await visitIndex.GetVisitsByDisciplineAsync(HHCVisitDiscipline.PhysicalTherapy);
        List<HHCVisitIndexEntry> all = await visitIndex.GetAllVisitsAsync();

        Assert.That(nursing, Has.Count.EqualTo(1));
        Assert.That(pt, Has.Count.EqualTo(1));
        Assert.That(all, Has.Count.EqualTo(3));
        // Most recent first
        Assert.That(all[0].Discipline, Is.EqualTo(HHCVisitDiscipline.OccupationalTherapy));
    }
}
