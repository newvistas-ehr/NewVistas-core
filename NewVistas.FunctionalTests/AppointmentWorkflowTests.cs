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
/// Functional tests for the scheduling conflict detection system.
/// Verifies daily capacity enforcement, time-slot overlap detection,
/// double-book override, and clinic-level overbooking bypass.
/// </summary>
[TestFixture]
public class AppointmentWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Test 1: Happy path ───────────────────────────────────────────────

    [Test]
    public async Task ScheduleAppointment_FirstBooking_Succeeds()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(9);

        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "TEST CLINIC", apptTime, 30, null, null, "Annual visit", "REGULAR");

        Assert.That(apptId, Is.Not.Null.And.StartsWith("APPT-"));

        var apptGrain = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(apptId);
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        Assert.That(state.Status, Is.EqualTo("Scheduled"));
        Assert.That(state.IsDoubleBook, Is.False);
    }

    // ─── Test 2: Multiple bookings within daily capacity ──────────────────

    [Test]
    public async Task ScheduleAppointment_WithinCapacity_Succeeds()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync("CAP CLINIC", null, null, null, null, 30, 3, false, null, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(8);

        string a1 = await workflow.ScheduleAppointmentAsync(clinicId, "CAP CLINIC", apptDate.AddHours(9),  30, null, null, null, "REGULAR");
        string a2 = await workflow.ScheduleAppointmentAsync(clinicId, "CAP CLINIC", apptDate.AddHours(10), 30, null, null, null, "REGULAR");
        string a3 = await workflow.ScheduleAppointmentAsync(clinicId, "CAP CLINIC", apptDate.AddHours(11), 30, null, null, null, "FOLLOW-UP");

        Assert.That(new[] { a1, a2, a3 }, Is.All.StartsWith("APPT-"));
    }

    // ─── Test 3: Daily capacity exceeded ─────────────────────────────────

    [Test]
    public async Task ScheduleAppointment_ExceedsDailyCapacity_ThrowsInvalidOperationException()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync("SMALL CLINIC", null, null, null, null, 30, 1, false, null, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(9);

        // First appointment fills the single slot for this day
        await workflow.ScheduleAppointmentAsync(clinicId, "SMALL CLINIC", apptDate.AddHours(9),  30, null, null, null, "REGULAR");

        // Second appointment on the same day must be rejected
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.ScheduleAppointmentAsync(clinicId, "SMALL CLINIC", apptDate.AddHours(10), 30, null, null, null, "REGULAR"));
        Assert.That(ex!.Message, Does.Contain("booked"));
    }

    // ─── Test 4: Time-slot overlap ────────────────────────────────────────

    [Test]
    public async Task ScheduleAppointment_TimeOverlap_ThrowsInvalidOperationException()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(10);

        // First: 09:00–09:30
        await workflow.ScheduleAppointmentAsync(clinicId, "OVERLAP CLINIC", apptDate.AddHours(9), 30, null, null, null, "REGULAR");

        // Second: 09:15–09:45 — overlaps with first
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.ScheduleAppointmentAsync(clinicId, "OVERLAP CLINIC", apptDate.AddHours(9).AddMinutes(15), 30, null, null, null, "REGULAR"));
        Assert.That(ex!.Message, Does.Contain("conflict"));
    }

    // ─── Test 5: Double-book override bypasses capacity check ─────────────

    [Test]
    public async Task ScheduleAppointment_AllowDoubleBook_OverridesCapacityCheck()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync("DB CLINIC", null, null, null, null, 30, 1, false, null, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(11);

        await workflow.ScheduleAppointmentAsync(clinicId, "DB CLINIC", apptDate.AddHours(9),  30, null, null, null, "REGULAR");

        // Second appointment on full-capacity day — allowed via override
        string a2 = await workflow.ScheduleAppointmentAsync(clinicId, "DB CLINIC", apptDate.AddHours(10), 30, null, null, null, "REGULAR",
            allowDoubleBook: true);
        Assert.That(a2, Is.Not.Null.And.StartsWith("APPT-"));

        var apptGrain = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(a2);
        AppointmentState state = await apptGrain.GetAppointmentAsync();
        Assert.That(state.IsDoubleBook, Is.True);
    }

    // ─── Test 6: Double-book override bypasses overlap check ──────────────

    [Test]
    public async Task ScheduleAppointment_AllowDoubleBook_OverridesOverlapCheck()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(12);

        // 09:00–09:30
        await workflow.ScheduleAppointmentAsync(clinicId, "DB-OVERLAP CLINIC", apptDate.AddHours(9), 30, null, null, null, "REGULAR");

        // 09:15–09:45 — overlapping, allowed via override
        string a2 = await workflow.ScheduleAppointmentAsync(clinicId, "DB-OVERLAP CLINIC", apptDate.AddHours(9).AddMinutes(15), 30, null, null, null, "URGENT",
            allowDoubleBook: true);
        Assert.That(a2, Is.Not.Null.And.StartsWith("APPT-"));

        var apptGrain = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(a2);
        Assert.That((await apptGrain.GetAppointmentAsync()).IsDoubleBook, Is.True);
    }

    // ─── Test 7: Clinic-level AllowOverbooking bypasses all conflict checks ─

    [Test]
    public async Task ScheduleAppointment_ClinicAllowsOverbooking_NeverConflicts()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync("OPEN CLINIC", null, null, null, null, 30, 1, allowOverbooking: true, null, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(13);

        // Three appointments at the exact same time in a max-1 clinic — all succeed
        string a1 = await workflow.ScheduleAppointmentAsync(clinicId, "OPEN CLINIC", apptDate.AddHours(9), 30, null, null, null, "REGULAR");
        string a2 = await workflow.ScheduleAppointmentAsync(clinicId, "OPEN CLINIC", apptDate.AddHours(9), 30, null, null, null, "REGULAR");
        string a3 = await workflow.ScheduleAppointmentAsync(clinicId, "OPEN CLINIC", apptDate.AddHours(9), 30, null, null, null, "REGULAR");

        Assert.That(new[] { a1, a2, a3 }, Is.All.StartsWith("APPT-"));
    }

    // ─── Test 8: Cancelled slots are released for new bookings ────────────

    [Test]
    public async Task ScheduleAppointment_CancelledSlot_ReleasedForNewBooking()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string clinicId  = $"CLINIC-{Guid.NewGuid():N}";
        var clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync("CANCEL CLINIC", null, null, null, null, 30, 1, false, null, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(14);
        DateTime apptTime = apptDate.AddHours(9);

        // Schedule then cancel
        string a1 = await workflow.ScheduleAppointmentAsync(clinicId, "CANCEL CLINIC", apptTime, 30, null, null, null, "REGULAR");
        await workflow.CancelAppointmentAsync(a1);

        // Same slot must now be bookable again (cancelled entry is not Active)
        string a2 = await workflow.ScheduleAppointmentAsync(clinicId, "CANCEL CLINIC", apptTime, 30, null, null, null, "REGULAR");
        Assert.That(a2, Is.Not.Null.And.StartsWith("APPT-"));
        Assert.That(a2, Is.Not.EqualTo(a1));
    }
}
