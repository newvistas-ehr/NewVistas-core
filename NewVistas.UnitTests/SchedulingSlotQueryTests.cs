// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Available Slot Query and Capacity/Overbooking Check.
/// Tests the ScheduleIndexGrain's GetAvailableSlotsAsync at the grain level.
///
/// VistA reference: SDBUILD.m availability grid, SD appointment grid logic.
/// </summary>
[TestFixture]
public class SchedulingSlotQueryTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IScheduleIndexGrain GetScheduleIndex(string clinicId)
        => _cluster.GrainFactory.GetGrain<IScheduleIndexGrain>($"CLINIC-SCHED:{clinicId}");

    // ─── Available Slots — Empty Schedule ────────────────────────────────────

    [Test]
    public async Task GetAvailableSlots_EmptySchedule_AllSlotsAvailable()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 8, 17, 30);

        // 8am-5pm = 9 hours = 18 x 30-minute slots
        Assert.That(slots, Has.Count.EqualTo(18));
        Assert.That(slots.All(s => s.IsAvailable), Is.True);
        Assert.That(slots.All(s => s.BookedCount == 0), Is.True);
        Assert.That(slots[0].StartTime, Is.EqualTo(date.AddHours(8)));
        Assert.That(slots[0].EndTime, Is.EqualTo(date.AddHours(8).AddMinutes(30)));
        Assert.That(slots[^1].StartTime, Is.EqualTo(date.AddHours(16).AddMinutes(30)));
    }

    [Test]
    public async Task GetAvailableSlots_45MinSlots_CorrectCount()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 8, 17, 45);

        // 9 hours = 540 minutes / 45 = 12 slots
        Assert.That(slots, Has.Count.EqualTo(12));
    }

    [Test]
    public async Task GetAvailableSlots_60MinSlots_CorrectCount()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 8, 17, 60);

        // 9 hours = 9 x 60-minute slots
        Assert.That(slots, Has.Count.EqualTo(9));
    }

    // ─── Available Slots — With Booked Appointments ─────────────────────────

    [Test]
    public async Task GetAvailableSlots_OneAppointment_SlotMarkedBooked()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        // Book the 9:00 slot
        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}",
            PatientId = "P-001",
            AppointmentDateTime = date.AddHours(9),
            DurationMinutes = 30,
            Status = "Scheduled"
        });

        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 8, 17, 30);

        // The 9:00 slot should be booked
        AvailableSlot nineAm = slots.First(s => s.StartTime.Hour == 9 && s.StartTime.Minute == 0);
        Assert.That(nineAm.IsAvailable, Is.False);
        Assert.That(nineAm.BookedCount, Is.EqualTo(1));

        // Other slots should be available
        AvailableSlot eightAm = slots.First(s => s.StartTime.Hour == 8 && s.StartTime.Minute == 0);
        Assert.That(eightAm.IsAvailable, Is.True);
    }

    [Test]
    public async Task GetAvailableSlots_MultipleAppointments_CorrectBookedCounts()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        // Book 10:00 twice (double-book scenario)
        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}",
            PatientId = "P-001",
            AppointmentDateTime = date.AddHours(10),
            DurationMinutes = 30,
            Status = "Scheduled"
        });
        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}",
            PatientId = "P-002",
            AppointmentDateTime = date.AddHours(10),
            DurationMinutes = 30,
            Status = "Scheduled",
            IsDoubleBook = true
        });

        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 8, 17, 30);

        AvailableSlot tenAm = slots.First(s => s.StartTime.Hour == 10 && s.StartTime.Minute == 0);
        Assert.That(tenAm.IsAvailable, Is.False);
        Assert.That(tenAm.BookedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAvailableSlots_CancelledAppointment_SlotIsAvailable()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        string apptId = $"APPT-{Guid.NewGuid()}";

        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = apptId,
            PatientId = "P-001",
            AppointmentDateTime = date.AddHours(11),
            DurationMinutes = 30,
            Status = "Scheduled"
        });

        // Cancel it
        await index.UpdateStatusAsync(apptId, "Cancelled");

        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 8, 17, 30);

        AvailableSlot elevenAm = slots.First(s => s.StartTime.Hour == 11 && s.StartTime.Minute == 0);
        Assert.That(elevenAm.IsAvailable, Is.True);
        Assert.That(elevenAm.BookedCount, Is.EqualTo(0));
    }

    // ─── Capacity Check (existing — verify behavior) ────────────────────────

    [Test]
    public async Task GetCountByDate_ReturnsActiveOnly()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}", PatientId = "P-001",
            AppointmentDateTime = date.AddHours(9), DurationMinutes = 30, Status = "Scheduled"
        });
        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}", PatientId = "P-002",
            AppointmentDateTime = date.AddHours(10), DurationMinutes = 30, Status = "Cancelled"
        });
        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}", PatientId = "P-003",
            AppointmentDateTime = date.AddHours(11), DurationMinutes = 30, Status = "Checked In"
        });

        int count = await index.GetCountByDateAsync(date);
        Assert.That(count, Is.EqualTo(2)); // Only Scheduled + Checked In
    }

    [Test]
    public async Task HasOverlap_DetectsConflict()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = GetScheduleIndex(clinicId);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        await index.AddOrUpdateAsync(new ClinicScheduleEntry
        {
            AppointmentId = $"APPT-{Guid.NewGuid()}", PatientId = "P-001",
            AppointmentDateTime = date.AddHours(14), DurationMinutes = 30, Status = "Scheduled"
        });

        bool overlaps = await index.HasOverlapAsync(date.AddHours(14).AddMinutes(15), 30);
        Assert.That(overlaps, Is.True);

        bool noOverlap = await index.HasOverlapAsync(date.AddHours(14).AddMinutes(30), 30);
        Assert.That(noOverlap, Is.False);
    }
}
