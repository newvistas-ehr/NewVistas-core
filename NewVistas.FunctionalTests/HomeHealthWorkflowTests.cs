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
/// Functional tests for Home Health / Community Care — VistA File #750.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class HomeHealthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IHBPCPatientGrain GetHBPCPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IHBPCPatientGrain>($"HBPC-PATIENT:{patientId}");

    private IHBPCRegistryGrain GetRegistry()
        => _cluster.GrainFactory.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY");

    private IHHCVisitGrain GetVisit(string id)
        => _cluster.GrainFactory.GetGrain<IHHCVisitGrain>(id);

    private IHHCVisitIndexGrain GetVisitIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{patientId}");

    // ── HBPC Patient Tests ───────────────────────────────────────────────────

    [Test]
    public async Task EnrollPatient_SetsActiveStatus()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHBPCPatientGrain grain = GetHBPCPatient(patientId);

        await grain.EnrollPatientAsync(
            patientId, "DOE,JOHN", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced,
            "COPD with frequent exacerbations",
            "Mary Doe (wife)", "123 Main St, Anytown VA 22033");

        HBPCPatientState state = await grain.GetPatientAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Active));
        Assert.That(state.LevelOfCare, Is.EqualTo(HBPCLevelOfCare.Enhanced));
        Assert.That(state.PrimaryDiagnosis, Does.Contain("COPD"));
        Assert.That(state.PrimaryCaregiver, Is.EqualTo("Mary Doe (wife)"));
    }

    [Test]
    public async Task UpdateLevelOfCare_ChangesLevel()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHBPCPatientGrain grain = GetHBPCPatient(patientId);

        await grain.EnrollPatientAsync(
            patientId, "SMITH,JANE", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "Hypertension", "N/A", "456 Oak Ave");

        await grain.UpdateLevelOfCareAsync(HBPCLevelOfCare.Palliative);

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.LevelOfCare, Is.EqualTo(HBPCLevelOfCare.Palliative));
    }

    [Test]
    public async Task AddGoalAndCareTeamMember_AppendsCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHBPCPatientGrain grain = GetHBPCPatient(patientId);

        await grain.EnrollPatientAsync(
            patientId, "GREEN,BOB", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "CHF", "Son", "789 Pine Rd");

        await grain.AddGoalAsync("Optimize diuretic therapy");
        await grain.AddGoalAsync("Reduce ER visits");
        await grain.AddCareTeamMemberAsync("Dr. Adams, MD (Primary)");
        await grain.AddCareTeamMemberAsync("RN Jones (Home Health Nurse)");

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.Goals, Has.Count.EqualTo(2));
        Assert.That(state.CareTeamMembers, Has.Count.EqualTo(2));
        Assert.That(state.Goals, Contains.Item("Reduce ER visits"));
    }

    [Test]
    public async Task RecordVisit_UpdatesVisitDateAndCount()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHBPCPatientGrain grain = GetHBPCPatient(patientId);

        await grain.EnrollPatientAsync(
            patientId, "WHITE,TOM", DateTime.UtcNow,
            HBPCLevelOfCare.Basic, "Diabetes", "N/A", "321 Elm St");

        DateTime visitDate = DateTime.UtcNow;
        DateTime nextVisit = visitDate.AddDays(14);
        await grain.RecordVisitAsync(visitDate, nextVisit);

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.LastVisitDate, Is.Not.Null);
        Assert.That(state.NextScheduledVisit, Is.Not.Null);
        Assert.That(state.TotalVisitsThisYear, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task SuspendAndReactivate_TransitionsCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHBPCPatientGrain grain = GetHBPCPatient(patientId);

        await grain.EnrollPatientAsync(
            patientId, "KING,DAN", DateTime.UtcNow,
            HBPCLevelOfCare.Enhanced, "Stroke recovery", "Wife", "100 Maple Dr");

        await grain.SuspendEnrollmentAsync();
        HBPCPatientState stateSuspended = await grain.GetPatientAsync();
        Assert.That(stateSuspended.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Suspended));

        await grain.ReactivateEnrollmentAsync();
        HBPCPatientState stateReactivated = await grain.GetPatientAsync();
        Assert.That(stateReactivated.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Active));
    }

    [Test]
    public async Task DischargePatient_SetsDischargedStatus()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHBPCPatientGrain grain = GetHBPCPatient(patientId);

        await grain.EnrollPatientAsync(
            patientId, "GRAY,ALICE", DateTime.UtcNow.AddMonths(-6),
            HBPCLevelOfCare.Basic, "Hypertension", "N/A", "200 Cedar Ln");

        await grain.DischargePatientAsync(HBPCDischargeReason.GoalsMet, "All care goals achieved");

        HBPCPatientState state = await grain.GetPatientAsync();
        Assert.That(state.ProgramStatus, Is.EqualTo(HBPCProgramStatus.Discharged));
        Assert.That(state.DischargeReason, Is.EqualTo(HBPCDischargeReason.GoalsMet));
        Assert.That(state.DischargeNotes, Does.Contain("goals achieved"));
        Assert.That(state.DischargeDate, Is.Not.Null);
    }

    // ── Registry Tests ───────────────────────────────────────────────────────

    [Test]
    public async Task Registry_UpsertAndQueryActivePatients()
    {
        IHBPCRegistryGrain registry = GetRegistry();

        string patientId = $"PAT-REG-{Guid.NewGuid():N}";
        await registry.UpsertPatientAsync(new HBPCRegistryEntry
        {
            PatientId = patientId, PatientName = "TEST,ACTIVE",
            EnrollmentDate = DateTime.UtcNow,
            ProgramStatus = HBPCProgramStatus.Active,
            LevelOfCare = HBPCLevelOfCare.Enhanced,
            PrimaryDiagnosis = "CHF"
        });

        List<HBPCRegistryEntry> active = await registry.GetActivePatientsAsync();
        Assert.That(active.Any(p => p.PatientId == patientId), Is.True);
    }

    // ── HHC Visit Tests ──────────────────────────────────────────────────────

    [Test]
    public async Task ScheduleVisit_CreatesScheduledVisit()
    {
        string visitId = $"HHC-VISIT-{Guid.NewGuid():N}";
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IHHCVisitGrain grain = GetVisit(visitId);

        await grain.ScheduleVisitAsync(
            patientId, "DOE,JOHN",
            DateTime.UtcNow.AddDays(3),
            HHCVisitDiscipline.Nursing, HHCVisitType.Routine,
            "RN-001", "Nancy Nurse",
            "Routine wound care visit");

        HHCVisitState state = await grain.GetVisitAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Discipline, Is.EqualTo(HHCVisitDiscipline.Nursing));
        Assert.That(state.VisitType, Is.EqualTo(HHCVisitType.Routine));
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.Scheduled));
        Assert.That(state.ClinicianName, Is.EqualTo("Nancy Nurse"));
    }

    [Test]
    public async Task CompleteVisit_SetsCompletedStatus()
    {
        string visitId = $"HHC-VISIT-{Guid.NewGuid():N}";
        IHHCVisitGrain grain = GetVisit(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-VIS-1", "SMITH,JANE", DateTime.UtcNow,
            HHCVisitDiscipline.PhysicalTherapy, HHCVisitType.Routine,
            "PT-001", "Pete PT", "Gait training");

        await grain.CompleteVisitAsync(
            durationMinutes: 45,
            vitalSigns: "BP 130/80, HR 76, O2 97%",
            interventions: new List<string> { "Gait training with walker", "Balance exercises" },
            patientResponse: "Tolerated well",
            goalsProgress: "Improving steadily",
            nextVisitDate: DateTime.UtcNow.AddDays(7),
            notes: "Patient progressing well with walker");

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.Completed));
        Assert.That(state.DurationMinutes, Is.EqualTo(45));
        Assert.That(state.Interventions, Has.Count.EqualTo(2));
        Assert.That(state.VitalSigns, Does.Contain("BP 130/80"));
    }

    [Test]
    public async Task CancelVisit_SetsStatusCancelled()
    {
        string visitId = $"HHC-VISIT-{Guid.NewGuid():N}";
        IHHCVisitGrain grain = GetVisit(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-VIS-2", "GREEN,BOB", DateTime.UtcNow.AddDays(1),
            HHCVisitDiscipline.SocialWork, HHCVisitType.Routine,
            "SW-001", "Sarah SW", "Follow-up assessment");

        await grain.CancelVisitAsync("Patient hospitalized");

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.Cancelled));
        Assert.That(state.CancellationReason, Does.Contain("hospitalized"));
    }

    [Test]
    public async Task MarkNoAnswer_SetsNoAnswerStatus()
    {
        string visitId = $"HHC-VISIT-{Guid.NewGuid():N}";
        IHHCVisitGrain grain = GetVisit(visitId);

        await grain.ScheduleVisitAsync(
            "PAT-VIS-3", "WHITE,TOM", DateTime.UtcNow,
            HHCVisitDiscipline.Nursing, HHCVisitType.Routine,
            "RN-002", "Nurse Brown", "Medication management");

        await grain.MarkNoAnswerAsync();

        HHCVisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo(HHCVisitStatus.NoAnswer));
    }

    // ── Visit Index Tests ────────────────────────────────────────────────────

    [Test]
    public async Task VisitIndex_QueryByDiscipline()
    {
        string patientId = $"PAT-VIDX-{Guid.NewGuid():N}";
        IHHCVisitIndexGrain index = GetVisitIndex(patientId);

        await index.UpsertVisitAsync(new HHCVisitIndexEntry
        {
            VisitId = $"HHC-VISIT-{Guid.NewGuid():N}",
            PatientId = patientId, PatientName = "TEST,VISIT",
            VisitDate = DateTime.UtcNow,
            Discipline = HHCVisitDiscipline.OccupationalTherapy,
            VisitType = HHCVisitType.Routine,
            Status = HHCVisitStatus.Completed,
            ClinicianName = "OT Smith", DurationMinutes = 30
        });

        List<HHCVisitIndexEntry> results = await index.GetVisitsByDisciplineAsync(HHCVisitDiscipline.OccupationalTherapy);
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }
}
