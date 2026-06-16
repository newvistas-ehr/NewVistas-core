// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Available Slot Query and Capacity/Overbooking Check
/// via IPatientWorkflowGrain — the same path used by Blazor and WPF apps.
///
/// VistA reference: SDBUILD.m availability grid, SD appointment grid logic.
/// </summary>
[TestFixture]
public class SchedulingSlotQueryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> SetupClinic(string clinicId, string name, int apptLength = 30, int maxPerDay = 20, bool allowOverbook = false)
    {
        IClinicGrain clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync(name, "MAIN", null, null, null,
            apptLength, maxPerDay, allowOverbook, "C", null, null);
        return clinicId;
    }

    // ─── Available Slots via Workflow ────────────────────────────────────────

    [Test]
    public async Task GetAvailableSlots_EmptyClinic_AllSlotsAvailable()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "EMPTY CLINIC", apptLength: 30);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        List<AvailableSlot> slots = await wf.GetAvailableSlotsAsync(clinicId, date);

        Assert.That(slots, Has.Count.EqualTo(18)); // 9 hours / 30 min = 18
        Assert.That(slots.All(s => s.IsAvailable), Is.True);
    }

    [Test]
    public async Task GetAvailableSlots_AfterBooking_SlotUnavailable()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "BOOKING CLINIC", apptLength: 30);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        // Book an appointment at 10:00
        await wf.ScheduleAppointmentAsync(
            clinicId, "BOOKING CLINIC", date.AddHours(10), 30,
            null, null, "Check-up", "REGULAR", false);

        List<AvailableSlot> slots = await wf.GetAvailableSlotsAsync(clinicId, date);

        AvailableSlot tenAm = slots.First(s => s.StartTime.Hour == 10 && s.StartTime.Minute == 0);
        Assert.That(tenAm.IsAvailable, Is.False);
        Assert.That(tenAm.BookedCount, Is.EqualTo(1));

        // Adjacent slots should still be available
        AvailableSlot nineThirty = slots.First(s => s.StartTime.Hour == 9 && s.StartTime.Minute == 30);
        Assert.That(nineThirty.IsAvailable, Is.True);

        AvailableSlot tenThirty = slots.First(s => s.StartTime.Hour == 10 && s.StartTime.Minute == 30);
        Assert.That(tenThirty.IsAvailable, Is.True);
    }

    [Test]
    public async Task GetAvailableSlots_45MinClinic_UsesClinicAppointmentLength()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "CARDIO CLINIC", apptLength: 45);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        List<AvailableSlot> slots = await wf.GetAvailableSlotsAsync(clinicId, date);

        // 9 hours = 540 min / 45 = 12 slots
        Assert.That(slots, Has.Count.EqualTo(12));
        Assert.That(slots[0].DurationMinutes, Is.EqualTo(45));
    }

    // ─── Clinic Daily Capacity ──────────────────────────────────────────────

    [Test]
    public async Task GetClinicDailyCapacity_EmptyDay_ShowsFullCapacity()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "CAPACITY CLINIC", maxPerDay: 10);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        ClinicDailyCapacity cap = await wf.GetClinicDailyCapacityAsync(clinicId, date);

        Assert.That(cap.ClinicName, Is.EqualTo("CAPACITY CLINIC"));
        Assert.That(cap.MaxPatientsPerDay, Is.EqualTo(10));
        Assert.That(cap.BookedCount, Is.EqualTo(0));
        Assert.That(cap.RemainingSlots, Is.EqualTo(10));
        Assert.That(cap.IsAtCapacity, Is.False);
        Assert.That(cap.AvailableSlots, Has.Count.GreaterThan(0));
    }

    [Test]
    public async Task GetClinicDailyCapacity_SomeBooked_ShowsCorrectRemaining()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "PARTIAL CLINIC", maxPerDay: 5);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        // Book 3 appointments
        for (int i = 0; i < 3; i++)
        {
            await wf.ScheduleAppointmentAsync(
                clinicId, "PARTIAL CLINIC", date.AddHours(9 + i), 30,
                null, null, "Visit", "REGULAR", false);
        }

        ClinicDailyCapacity cap = await wf.GetClinicDailyCapacityAsync(clinicId, date);

        Assert.That(cap.BookedCount, Is.EqualTo(3));
        Assert.That(cap.RemainingSlots, Is.EqualTo(2));
        Assert.That(cap.IsAtCapacity, Is.False);
    }

    [Test]
    public async Task GetClinicDailyCapacity_AtCapacity_ShowsAtCapacity()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "FULL CLINIC", maxPerDay: 3, allowOverbook: false);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        // Fill to capacity
        for (int i = 0; i < 3; i++)
        {
            await wf.ScheduleAppointmentAsync(
                clinicId, "FULL CLINIC", date.AddHours(9 + i), 30,
                null, null, "Visit", "REGULAR", false);
        }

        ClinicDailyCapacity cap = await wf.GetClinicDailyCapacityAsync(clinicId, date);

        Assert.That(cap.BookedCount, Is.EqualTo(3));
        Assert.That(cap.RemainingSlots, Is.EqualTo(0));
        Assert.That(cap.IsAtCapacity, Is.True);
    }

    // ─── Overbooking Enforcement ────────────────────────────────────────────

    [Test]
    public async Task Schedule_AtCapacity_OverbookDisabled_Throws()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "NO-OVERBOOK CLINIC", maxPerDay: 2, allowOverbook: false);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        // Fill to capacity
        await wf.ScheduleAppointmentAsync(clinicId, "NO-OVERBOOK CLINIC",
            date.AddHours(9), 30, null, null, "V1", "REGULAR", false);
        await wf.ScheduleAppointmentAsync(clinicId, "NO-OVERBOOK CLINIC",
            date.AddHours(10), 30, null, null, "V2", "REGULAR", false);

        // Third booking should fail
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            wf.ScheduleAppointmentAsync(clinicId, "NO-OVERBOOK CLINIC",
                date.AddHours(11), 30, null, null, "V3", "REGULAR", false));
    }

    [Test]
    public async Task Schedule_AtCapacity_DoubleBookOverride_Succeeds()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "OVERBOOK CLINIC", maxPerDay: 2, allowOverbook: false);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        await wf.ScheduleAppointmentAsync(clinicId, "OVERBOOK CLINIC",
            date.AddHours(9), 30, null, null, "V1", "REGULAR", false);
        await wf.ScheduleAppointmentAsync(clinicId, "OVERBOOK CLINIC",
            date.AddHours(10), 30, null, null, "V2", "REGULAR", false);

        // Third booking with double-book override
        string apptId = await wf.ScheduleAppointmentAsync(clinicId, "OVERBOOK CLINIC",
            date.AddHours(11), 30, null, null, "V3", "REGULAR", true);

        Assert.That(apptId, Does.StartWith("APPT-"));
    }

    [Test]
    public async Task GetClinicDailyCapacity_OverbookingAllowed_NotAtCapacity()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "ALLOW-OVERBOOK", maxPerDay: 2, allowOverbook: true);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        await wf.ScheduleAppointmentAsync(clinicId, "ALLOW-OVERBOOK",
            date.AddHours(9), 30, null, null, "V1", "REGULAR", false);
        await wf.ScheduleAppointmentAsync(clinicId, "ALLOW-OVERBOOK",
            date.AddHours(10), 30, null, null, "V2", "REGULAR", false);

        ClinicDailyCapacity cap = await wf.GetClinicDailyCapacityAsync(clinicId, date);

        Assert.That(cap.BookedCount, Is.EqualTo(2));
        Assert.That(cap.AllowOverbooking, Is.True);
        Assert.That(cap.IsAtCapacity, Is.False); // AllowOverbooking = never at capacity
    }

    // ─── Slot Availability After Cancel ──────────────────────────────────────

    [Test]
    public async Task GetAvailableSlots_AfterCancel_SlotBecomesAvailable()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "CANCEL CLINIC", apptLength: 30, maxPerDay: 20);

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        string apptId = await wf.ScheduleAppointmentAsync(
            clinicId, "CANCEL CLINIC", date.AddHours(14), 30,
            null, null, "Visit", "REGULAR", false);

        // Verify slot is booked
        List<AvailableSlot> slotsBefore = await wf.GetAvailableSlotsAsync(clinicId, date);
        Assert.That(slotsBefore.First(s => s.StartTime.Hour == 14).IsAvailable, Is.False);

        // Cancel
        await wf.CancelAppointmentAsync(apptId);

        // Verify slot is now available
        List<AvailableSlot> slotsAfter = await wf.GetAvailableSlotsAsync(clinicId, date);
        Assert.That(slotsAfter.First(s => s.StartTime.Hour == 14).IsAvailable, Is.True);
    }
}
