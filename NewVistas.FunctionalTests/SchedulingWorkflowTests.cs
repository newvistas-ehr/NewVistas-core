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
/// Functional tests for VistA Scheduling (SD) package —
/// File #44 (Hospital Location / Clinic) and File #44.4 (Patient Appointments).
/// </summary>
[TestFixture]
public class SchedulingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IClinicGrain GetClinicGrain(string clinicId) =>
        _cluster.GrainFactory.GetGrain<IClinicGrain>($"SD-CLINIC:{clinicId}");

    private IClinicIndexGrain GetClinicIndex() =>
        _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");

    private IAppointmentGrain GetAppointmentGrain(string appointmentId) =>
        _cluster.GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);

    private IPatientScheduleIndexGrain GetScheduleIndex(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientScheduleIndexGrain>($"SD-SCHED:{patientId}");

    // ─── Clinic Grain Tests ───────────────────────────────────────────────

    [Test]
    public async Task ClinicGrain_Create_PersistsName()
    {
        string clinicId = $"CLINIC-{Guid.NewGuid()}";
        IClinicGrain grain = GetClinicGrain(clinicId);

        await grain.CreateClinicAsync("PRIMARY CARE", "MAIN", "323", "555-1234",
            "Building 1 Rm 101", 30, 20, false, "C", null, null);

        ClinicState state = await grain.GetClinicAsync();
        Assert.That(state.Name, Is.EqualTo("PRIMARY CARE"));
        Assert.That(state.StopCode, Is.EqualTo("323"));
        Assert.That(state.AppointmentLength, Is.EqualTo(30));
    }

    [Test]
    public async Task ClinicGrain_Create_DefaultStatus_IsActive()
    {
        string clinicId = $"CLINIC-{Guid.NewGuid()}";
        IClinicGrain grain = GetClinicGrain(clinicId);

        await grain.CreateClinicAsync("CARDIOLOGY", "MAIN", "303", null, null, 45, 15, false, "C", null, null);

        ClinicState state = await grain.GetClinicAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.ClinicType, Is.EqualTo("C"));
    }

    [Test]
    public async Task ClinicGrain_Update_ChangesFields()
    {
        string clinicId = $"CLINIC-{Guid.NewGuid()}";
        IClinicGrain grain = GetClinicGrain(clinicId);
        await grain.CreateClinicAsync("ORTHO", "NORTH", "218", null, null, 45, 12, false, "C", null, null);

        await grain.UpdateClinicAsync(null, null, "555-9999", "Building 3 Rm 205", 60, null, null, null);

        ClinicState state = await grain.GetClinicAsync();
        Assert.That(state.PhoneNumber, Is.EqualTo("555-9999"));
        Assert.That(state.AppointmentLength, Is.EqualTo(60));
    }

    // ─── Clinic Index Grain Tests ─────────────────────────────────────────

    [Test]
    public async Task ClinicIndexGrain_SeedDemo_HasClinics()
    {
        // Use a unique index key to avoid cross-test contamination
        IClinicIndexGrain idx = _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-TEST-INDEX-1");
        await idx.SeedDemoClinicsAsync();

        List<ClinicEntry> clinics = await idx.GetAllClinicsAsync();
        Assert.That(clinics, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(clinics.Any(c => c.Name == "PRIMARY CARE"), Is.True);
    }

    [Test]
    public async Task ClinicIndexGrain_GetAll_AutoSeeds_WhenEmpty()
    {
        IClinicIndexGrain idx = _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-TEST-INDEX-2");

        // Just calling GetAll should auto-seed
        List<ClinicEntry> clinics = await idx.GetAllClinicsAsync();
        Assert.That(clinics, Has.Count.GreaterThan(0));
    }

    [Test]
    public async Task ClinicIndexGrain_Search_FiltersByName()
    {
        IClinicIndexGrain idx = _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-TEST-INDEX-3");
        await idx.SeedDemoClinicsAsync();

        List<ClinicEntry> results = await idx.SearchClinicsAsync("cardio");
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Name, Is.EqualTo("CARDIOLOGY"));
    }

    [Test]
    public async Task ClinicIndexGrain_AddOrUpdate_ReplacesEntry()
    {
        IClinicIndexGrain idx = _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-TEST-INDEX-4");

        var entry = new ClinicEntry { ClinicId = "TEST-001", Name = "TEST CLINIC", Status = "ACTIVE", AppointmentLength = 30 };
        await idx.AddOrUpdateClinicAsync(entry);

        var updated = new ClinicEntry { ClinicId = "TEST-001", Name = "TEST CLINIC UPDATED", Status = "ACTIVE", AppointmentLength = 45 };
        await idx.AddOrUpdateClinicAsync(updated);

        List<ClinicEntry> all = await idx.GetAllClinicsAsync();
        ClinicEntry? found = all.FirstOrDefault(c => c.ClinicId == "TEST-001");
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Name, Is.EqualTo("TEST CLINIC UPDATED"));
        Assert.That(found.AppointmentLength, Is.EqualTo(45));
    }

    // ─── Appointment Grain Tests ──────────────────────────────────────────

    [Test]
    public async Task AppointmentGrain_Schedule_PersistsState()
    {
        string apptId = $"APPT-{Guid.NewGuid()}";
        IAppointmentGrain grain = GetAppointmentGrain(apptId);

        await grain.ScheduleAppointmentAsync("PAT-001", "SD-CLINIC-001", "PRIMARY CARE",
            new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            30, "PROV-001", "Dr. Reynolds", "Annual check-up", "REGULAR", null);

        AppointmentState state = await grain.GetAppointmentAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.ClinicName, Is.EqualTo("PRIMARY CARE"));
        Assert.That(state.Status, Is.EqualTo("Scheduled"));
        Assert.That(state.DurationMinutes, Is.EqualTo(30));
    }

    [Test]
    public async Task AppointmentGrain_CheckIn_SetsStatus()
    {
        string apptId = $"APPT-{Guid.NewGuid()}";
        IAppointmentGrain grain = GetAppointmentGrain(apptId);
        await grain.ScheduleAppointmentAsync("PAT-002", "SD-CLINIC-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(1), 30, null, null, null, null, null);

        DateTime ciTime = DateTime.UtcNow;
        await grain.CheckInAsync(ciTime);

        AppointmentState state = await grain.GetAppointmentAsync();
        Assert.That(state.Status, Is.EqualTo("Checked In"));
        Assert.That(state.CheckInDateTime, Is.EqualTo(ciTime).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task AppointmentGrain_CheckOut_CompletesAppointment()
    {
        string apptId = $"APPT-{Guid.NewGuid()}";
        IAppointmentGrain grain = GetAppointmentGrain(apptId);
        await grain.ScheduleAppointmentAsync("PAT-003", "SD-CLINIC-003", "CARDIOLOGY",
            DateTime.UtcNow.AddDays(1), 45, null, null, null, null, null);
        await grain.CheckInAsync(DateTime.UtcNow);

        DateTime coTime = DateTime.UtcNow.AddHours(1);
        await grain.CheckOutAsync(coTime);
        await grain.CompleteAppointmentAsync();

        AppointmentState state = await grain.GetAppointmentAsync();
        Assert.That(state.Status, Is.EqualTo("Completed"));
        Assert.That(state.CheckOutDateTime, Is.EqualTo(coTime).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task AppointmentGrain_Cancel_SetsCancelled()
    {
        string apptId = $"APPT-{Guid.NewGuid()}";
        IAppointmentGrain grain = GetAppointmentGrain(apptId);
        await grain.ScheduleAppointmentAsync("PAT-004", "SD-CLINIC-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(7), 30, null, null, null, null, null);

        await grain.CancelAppointmentAsync("Patient request", null);

        AppointmentState state = await grain.GetAppointmentAsync();
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient request"));
    }

    [Test]
    public async Task AppointmentGrain_NoShow_SetsNoShowFlag()
    {
        string apptId = $"APPT-{Guid.NewGuid()}";
        IAppointmentGrain grain = GetAppointmentGrain(apptId);
        await grain.ScheduleAppointmentAsync("PAT-005", "SD-CLINIC-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(-1), 30, null, null, null, null, null);

        await grain.MarkAsNoShowAsync();

        AppointmentState state = await grain.GetAppointmentAsync();
        Assert.That(state.IsNoShow, Is.True);
        Assert.That(state.Status, Is.EqualTo("No-Show"));
    }

    [Test]
    public async Task AppointmentGrain_Update_ReschedulesDateTime()
    {
        string apptId = $"APPT-{Guid.NewGuid()}";
        IAppointmentGrain grain = GetAppointmentGrain(apptId);
        DateTime original = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        await grain.ScheduleAppointmentAsync("PAT-006", "SD-CLINIC-001", "PRIMARY CARE",
            original, 30, null, null, null, null, null);

        DateTime rescheduled = new DateTime(2026, 7, 15, 14, 0, 0, DateTimeKind.Utc);
        await grain.UpdateAppointmentAsync(rescheduled, null, null, null, "Patient request to reschedule", null, null);

        AppointmentState state = await grain.GetAppointmentAsync();
        Assert.That(state.AppointmentDateTime, Is.EqualTo(rescheduled));
    }

    // ─── Patient Schedule Index Grain Tests ──────────────────────────────

    [Test]
    public async Task PatientScheduleIndex_AddEntry_StoredAndRetrieved()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientScheduleIndexGrain idx = GetScheduleIndex(patientId);

        var entry = new AppointmentEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}",
            ClinicId = "SD-CLINIC-001",
            ClinicName = "PRIMARY CARE",
            AppointmentDateTime = DateTime.UtcNow.AddDays(14),
            DurationMinutes = 30,
            Status = "Scheduled"
        };
        await idx.AddOrUpdateAsync(entry);

        List<AppointmentEntry> entries = await idx.GetAppointmentsAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ClinicName, Is.EqualTo("PRIMARY CARE"));
    }

    [Test]
    public async Task PatientScheduleIndex_GetUpcoming_FiltersToFuture()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientScheduleIndexGrain idx = GetScheduleIndex(patientId);

        // Past appointment
        await idx.AddOrUpdateAsync(new AppointmentEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}",
            ClinicName = "PAST CLINIC",
            AppointmentDateTime = DateTime.UtcNow.AddDays(-7),
            Status = "Completed"
        });

        // Future appointment
        await idx.AddOrUpdateAsync(new AppointmentEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}",
            ClinicName = "FUTURE CLINIC",
            AppointmentDateTime = DateTime.UtcNow.AddDays(7),
            Status = "Scheduled"
        });

        List<AppointmentEntry> upcoming = await idx.GetUpcomingAsync();
        Assert.That(upcoming, Has.Count.EqualTo(1));
        Assert.That(upcoming[0].ClinicName, Is.EqualTo("FUTURE CLINIC"));
    }

    [Test]
    public async Task PatientScheduleIndex_AddOrUpdate_ReplacesExistingById()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientScheduleIndexGrain idx = GetScheduleIndex(patientId);

        string apptId = $"APPT-{Guid.NewGuid()}";
        await idx.AddOrUpdateAsync(new AppointmentEntry
        {
            AppointmentId = apptId,
            ClinicName = "PRIMARY CARE",
            AppointmentDateTime = DateTime.UtcNow.AddDays(7),
            Status = "Scheduled"
        });

        // Update status
        await idx.AddOrUpdateAsync(new AppointmentEntry
        {
            AppointmentId = apptId,
            ClinicName = "PRIMARY CARE",
            AppointmentDateTime = DateTime.UtcNow.AddDays(7),
            Status = "Checked In"
        });

        List<AppointmentEntry> entries = await idx.GetAppointmentsAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Status, Is.EqualTo("Checked In"));
    }
}
