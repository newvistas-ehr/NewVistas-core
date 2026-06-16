// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for ProviderAvailabilityGrain — weekly patterns, time blocks,
/// effective availability computation, and scheduling tier configuration.
///
/// Enhancement: Provider-level availability does NOT exist in core VistA
/// (VistA is clinic-centric, File #44.005). These tests verify the provider-centric
/// model that extends beyond VistA's SD package.
/// </summary>
[TestFixture]
public class ProviderAvailabilityGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IProviderAvailabilityGrain GetAvailability(string providerId)
        => _cluster.GrainFactory.GetGrain<IProviderAvailabilityGrain>($"PROV-AVAIL:{providerId}");

    // ─── Default State ──────────────────────────────────────────────────────

    [Test]
    public async Task ProviderAvailability_DefaultState_IsActive()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        ProviderAvailabilityState state = await grain.GetAvailabilityAsync();

        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.WeeklyPatterns, Is.Empty);
        Assert.That(state.TimeBlocks, Is.Empty);
        Assert.That(state.SchedulingTiers, Is.Empty);
    }

    // ─── Provider Status ────────────────────────────────────────────────────

    [Test]
    public async Task ProviderAvailability_UpdateStatus_ReflectsChange()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.UpdateProviderStatusAsync("ON_LEAVE", "Medical leave", "Admin User");

        ProviderAvailabilityState state = await grain.GetAvailabilityAsync();
        Assert.That(state.Status, Is.EqualTo("ON_LEAVE"));
        Assert.That(state.StatusReason, Is.EqualTo("Medical leave"));
        Assert.That(state.StatusChangedBy, Is.EqualTo("Admin User"));
        Assert.That(state.StatusChangedDate, Is.Not.Null);
    }

    [Test]
    public async Task ProviderAvailability_InactiveProvider_NoEffectiveAvailability()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        // Add a pattern first
        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-1", ClinicName = "Primary Care",
            DaysOfWeek = new() { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            StartHour = 8, EndHour = 17
        });

        // Set provider as unavailable
        await grain.UpdateProviderStatusAsync("UNAVAILABLE", "Illness", "Admin");

        // Effective availability should be empty
        DateTime monday = GetNextDayOfWeek(DayOfWeek.Monday);
        List<AvailabilityWindow> windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-1", monday);
        Assert.That(windows, Is.Empty);
    }

    // ─── Weekly Patterns ────────────────────────────────────────────────────

    [Test]
    public async Task ProviderAvailability_AddWeeklyPattern_Persists()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-A", ClinicName = "Primary Care",
            DaysOfWeek = new() { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            StartHour = 8, StartMinute = 0, EndHour = 12, EndMinute = 0
        });

        ProviderAvailabilityState state = await grain.GetAvailabilityAsync();
        Assert.That(state.WeeklyPatterns, Has.Count.EqualTo(1));
        Assert.That(state.WeeklyPatterns[0].ClinicId, Is.EqualTo("CLINIC-A"));
        Assert.That(state.WeeklyPatterns[0].DaysOfWeek, Has.Count.EqualTo(3));
        Assert.That(state.WeeklyPatterns[0].PatternId, Is.Not.Empty);
    }

    [Test]
    public async Task ProviderAvailability_EffectiveAvailability_MatchesDayOfWeek()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-X", ClinicName = "Cardiology",
            DaysOfWeek = new() { DayOfWeek.Tuesday },
            StartHour = 9, EndHour = 15
        });

        // Tuesday should have availability
        DateTime tuesday = GetNextDayOfWeek(DayOfWeek.Tuesday);
        List<AvailabilityWindow> tuesdayWindows = await grain.GetEffectiveAvailabilityAsync("CLINIC-X", tuesday);
        Assert.That(tuesdayWindows, Has.Count.EqualTo(1));
        Assert.That(tuesdayWindows[0].StartTime, Is.EqualTo(tuesday.AddHours(9)));
        Assert.That(tuesdayWindows[0].EndTime, Is.EqualTo(tuesday.AddHours(15)));

        // Wednesday should have NO availability
        DateTime wednesday = GetNextDayOfWeek(DayOfWeek.Wednesday);
        List<AvailabilityWindow> wednesdayWindows = await grain.GetEffectiveAvailabilityAsync("CLINIC-X", wednesday);
        Assert.That(wednesdayWindows, Is.Empty);
    }

    [Test]
    public async Task ProviderAvailability_EffectiveAvailability_WrongClinic_ReturnsEmpty()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-ONLY", ClinicName = "Dermatology",
            DaysOfWeek = new() { DayOfWeek.Monday },
            StartHour = 8, EndHour = 17
        });

        DateTime monday = GetNextDayOfWeek(DayOfWeek.Monday);
        List<AvailabilityWindow> windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-OTHER", monday);
        Assert.That(windows, Is.Empty);
    }

    [Test]
    public async Task ProviderAvailability_RemovePattern_RemovesAvailability()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            PatternId = "PAT-TOREMOVE",
            ClinicId = "CLINIC-R", ClinicName = "Test Clinic",
            DaysOfWeek = new() { DayOfWeek.Monday },
            StartHour = 8, EndHour = 12
        });

        await grain.RemoveWeeklyPatternAsync("PAT-TOREMOVE");

        ProviderAvailabilityState state = await grain.GetAvailabilityAsync();
        Assert.That(state.WeeklyPatterns, Is.Empty);
    }

    // ─── Time Blocks ────────────────────────────────────────────────────────

    [Test]
    public async Task ProviderAvailability_TimeBlock_SubtractsFromWindow()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        DateTime monday = GetNextDayOfWeek(DayOfWeek.Monday);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-B1", ClinicName = "Test",
            DaysOfWeek = new() { DayOfWeek.Monday },
            StartHour = 8, EndHour = 17
        });

        // Add a lunch block 12:00-13:00 on that Monday
        await grain.AddTimeBlockAsync(new ProviderTimeBlock
        {
            BlockType = "LUNCH",
            StartDateTime = monday.AddHours(12),
            EndDateTime = monday.AddHours(13),
            Reason = "Lunch break"
        });

        List<AvailabilityWindow> windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-B1", monday);

        // Should split into two windows: 8-12 and 13-17
        Assert.That(windows, Has.Count.EqualTo(2));
        Assert.That(windows[0].StartTime, Is.EqualTo(monday.AddHours(8)));
        Assert.That(windows[0].EndTime, Is.EqualTo(monday.AddHours(12)));
        Assert.That(windows[1].StartTime, Is.EqualTo(monday.AddHours(13)));
        Assert.That(windows[1].EndTime, Is.EqualTo(monday.AddHours(17)));
    }

    [Test]
    public async Task ProviderAvailability_TimeBlock_CoversEntireWindow_RemovesIt()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        DateTime tuesday = GetNextDayOfWeek(DayOfWeek.Tuesday);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-B2", ClinicName = "Test",
            DaysOfWeek = new() { DayOfWeek.Tuesday },
            StartHour = 8, EndHour = 12
        });

        // Block covers entire morning
        await grain.AddTimeBlockAsync(new ProviderTimeBlock
        {
            BlockType = "VACATION",
            StartDateTime = tuesday,
            EndDateTime = tuesday.AddDays(1)
        });

        List<AvailabilityWindow> windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-B2", tuesday);
        Assert.That(windows, Is.Empty);
    }

    [Test]
    public async Task ProviderAvailability_RecurringDailyBlock_AppliesEachDay()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        DateTime monday = GetNextDayOfWeek(DayOfWeek.Monday);
        DateTime friday = monday.AddDays(4);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-B3", ClinicName = "Test",
            DaysOfWeek = new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                 DayOfWeek.Thursday, DayOfWeek.Friday },
            StartHour = 8, EndHour = 17
        });

        // Add recurring daily lunch 12:00-13:00 for the whole week
        await grain.AddTimeBlockAsync(new ProviderTimeBlock
        {
            BlockType = "LUNCH",
            StartDateTime = monday,
            EndDateTime = friday.AddDays(1),
            IsRecurringDaily = true,
            RecurringStartHour = 12, RecurringStartMinute = 0,
            RecurringEndHour = 13, RecurringEndMinute = 0
        });

        // Check Wednesday — should have lunch removed
        DateTime wednesday = monday.AddDays(2);
        List<AvailabilityWindow> windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-B3", wednesday);
        Assert.That(windows, Has.Count.EqualTo(2)); // 8-12, 13-17
    }

    [Test]
    public async Task ProviderAvailability_ClinicSpecificBlock_DoesNotAffectOtherClinics()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        DateTime monday = GetNextDayOfWeek(DayOfWeek.Monday);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-C1", ClinicName = "Clinic C1",
            DaysOfWeek = new() { DayOfWeek.Monday }, StartHour = 8, EndHour = 12
        });
        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-C2", ClinicName = "Clinic C2",
            DaysOfWeek = new() { DayOfWeek.Monday }, StartHour = 13, EndHour = 17
        });

        // Block only CLINIC-C1
        await grain.AddTimeBlockAsync(new ProviderTimeBlock
        {
            BlockType = "ADMIN_TIME",
            StartDateTime = monday.AddHours(8),
            EndDateTime = monday.AddHours(12),
            ClinicId = "CLINIC-C1"
        });

        // CLINIC-C1 should be empty
        List<AvailabilityWindow> c1Windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-C1", monday);
        Assert.That(c1Windows, Is.Empty);

        // CLINIC-C2 should be unaffected
        List<AvailabilityWindow> c2Windows = await grain.GetEffectiveAvailabilityAsync("CLINIC-C2", monday);
        Assert.That(c2Windows, Has.Count.EqualTo(1));
    }

    // ─── Scheduling Tiers ───────────────────────────────────────────────────

    [Test]
    public async Task ProviderAvailability_SchedulingTiers_SetAndRetrieve()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.SetClinicSchedulingTiersAsync("CLINIC-T", new ClinicSchedulingTierConfig
        {
            PatientSelfSchedulingEnabled = true,
            PatientSchedulableSlotCount = 4,
            MinDaysAheadForPatient = 1,
            MaxDaysAheadForPatient = 60,
            AllowedPatientAppointmentTypes = new() { "REGULAR", "FOLLOW-UP" }
        });

        ClinicSchedulingTierConfig? config = await grain.GetClinicSchedulingTiersAsync("CLINIC-T");
        Assert.That(config, Is.Not.Null);
        Assert.That(config!.PatientSelfSchedulingEnabled, Is.True);
        Assert.That(config.PatientSchedulableSlotCount, Is.EqualTo(4));
        Assert.That(config.AllowedPatientAppointmentTypes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ProviderAvailability_NoTierConfig_ReturnsNull()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        ClinicSchedulingTierConfig? config = await grain.GetClinicSchedulingTiersAsync("NONEXISTENT");
        Assert.That(config, Is.Null);
    }

    // ─── Available Clinics for Date ─────────────────────────────────────────

    [Test]
    public async Task ProviderAvailability_AvailableClinicsForDate_ListsMatchingClinics()
    {
        string providerId = $"PROV-{Guid.NewGuid()}";
        IProviderAvailabilityGrain grain = GetAvailability(providerId);

        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-D1", ClinicName = "Primary Care",
            DaysOfWeek = new() { DayOfWeek.Monday, DayOfWeek.Wednesday },
            StartHour = 8, EndHour = 12
        });
        await grain.AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = "CLINIC-D2", ClinicName = "Cardiology",
            DaysOfWeek = new() { DayOfWeek.Monday },
            StartHour = 13, EndHour = 17
        });

        DateTime monday = GetNextDayOfWeek(DayOfWeek.Monday);
        List<ProviderClinicAvailabilitySummary> summaries = await grain.GetAvailableClinicsForDateAsync(monday);

        Assert.That(summaries, Has.Count.EqualTo(2));
        Assert.That(summaries.Any(s => s.ClinicId == "CLINIC-D1"), Is.True);
        Assert.That(summaries.Any(s => s.ClinicId == "CLINIC-D2"), Is.True);
    }

    // ─── Slot Generation with Availability Windows ──────────────────────────

    [Test]
    public async Task ScheduleIndex_AvailabilityWindowSlots_GeneratesWithinWindowOnly()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = _cluster.GrainFactory.GetGrain<IScheduleIndexGrain>($"CLINIC-SCHED:{clinicId}");

        DateTime date = DateTime.UtcNow.Date.AddDays(14);
        List<AvailabilityWindow> windows = new()
        {
            new() { StartTime = date.AddHours(8), EndTime = date.AddHours(12), ClinicId = clinicId, ClinicName = "Test" },
            new() { StartTime = date.AddHours(13), EndTime = date.AddHours(17), ClinicId = clinicId, ClinicName = "Test" }
        };

        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 30, windows, null);

        // 4 hours + 4 hours = 8 hours = 16 x 30-minute slots
        Assert.That(slots, Has.Count.EqualTo(16));
        Assert.That(slots.All(s => s.IsAvailable), Is.True);
        // Verify gap: no slot starts between 12:00 and 13:00
        Assert.That(slots.Any(s => s.StartTime.Hour == 12 && s.StartTime.Minute >= 0 && s.StartTime.Hour < 13), Is.False);
    }

    [Test]
    public async Task ScheduleIndex_AvailabilityWindowSlots_WithTierConfig_TagsPatientSlots()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IScheduleIndexGrain index = _cluster.GrainFactory.GetGrain<IScheduleIndexGrain>($"CLINIC-SCHED:{clinicId}");

        DateTime date = DateTime.UtcNow.Date.AddDays(14);
        List<AvailabilityWindow> windows = new()
        {
            new() { StartTime = date.AddHours(8), EndTime = date.AddHours(12), ClinicId = clinicId, ClinicName = "Test" }
        };

        ClinicSchedulingTierConfig tierConfig = new()
        {
            PatientSelfSchedulingEnabled = true,
            PatientSchedulableSlotCount = 3
        };

        List<AvailableSlot> slots = await index.GetAvailableSlotsAsync(date, 30, windows, tierConfig);

        // 8 slots total (4 hours / 30 min)
        Assert.That(slots, Has.Count.EqualTo(8));
        // First 3 should be PATIENT tier
        Assert.That(slots.Take(3).All(s => s.SchedulingTier == "PATIENT"), Is.True);
        // Remaining should be STAFF tier
        Assert.That(slots.Skip(3).All(s => s.SchedulingTier == "STAFF"), Is.True);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DateTime GetNextDayOfWeek(DayOfWeek target)
    {
        DateTime date = DateTime.UtcNow.Date.AddDays(7);
        while (date.DayOfWeek != target) date = date.AddDays(1);
        return date;
    }
}
