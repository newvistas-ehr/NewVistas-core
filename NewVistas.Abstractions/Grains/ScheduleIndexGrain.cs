// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Clinic Schedule Index Grain — per-clinic appointment register.
/// Provides conflict detection (overlap) and daily capacity checks.
///
/// Key pattern: "CLINIC-SCHED:{clinicId}"
/// Store: scheduleIndexStore
/// VistA SDCO.m / SDCOU.m — clinic slot management.
/// </summary>
public class ScheduleIndexGrain : Grain, IScheduleIndexGrain
{
    private readonly IPersistentState<ScheduleIndexState> _state;

    private static readonly HashSet<string> ActiveStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Scheduled", "Checked In" };

    public ScheduleIndexGrain(
        [PersistentState("scheduleIndex", "scheduleIndexStore")]
        IPersistentState<ScheduleIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ClinicId))
            _state.State.ClinicId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<List<ClinicScheduleEntry>> GetByDateAsync(DateTime date)
        => Task.FromResult(_state.State.Entries
            .Where(e => ActiveStatuses.Contains(e.Status) && e.AppointmentDateTime.Date == date.Date)
            .OrderBy(e => e.AppointmentDateTime)
            .ToList());

    public Task<int> GetCountByDateAsync(DateTime date)
        => Task.FromResult(_state.State.Entries
            .Count(e => ActiveStatuses.Contains(e.Status) && e.AppointmentDateTime.Date == date.Date));

    public Task<bool> HasOverlapAsync(DateTime proposedStart, int durationMinutes)
    {
        DateTime proposedEnd = proposedStart.AddMinutes(durationMinutes);
        bool overlaps = _state.State.Entries.Any(e =>
            ActiveStatuses.Contains(e.Status) &&
            e.AppointmentDateTime < proposedEnd &&
            e.AppointmentDateTime.AddMinutes(e.DurationMinutes) > proposedStart);
        return Task.FromResult(overlaps);
    }

    public async Task AddOrUpdateAsync(ClinicScheduleEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.AppointmentId == entry.AppointmentId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string appointmentId, string status)
    {
        ClinicScheduleEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.AppointmentId == appointmentId);
        if (entry != null)
        {
            entry.Status = status;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<AvailableSlot>> GetAvailableSlotsAsync(
        DateTime date, int startHour, int endHour, int slotDurationMinutes)
    {
        List<ClinicScheduleEntry> dayEntries = _state.State.Entries
            .Where(e => ActiveStatuses.Contains(e.Status) && e.AppointmentDateTime.Date == date.Date)
            .OrderBy(e => e.AppointmentDateTime)
            .ToList();

        List<AvailableSlot> slots = new();
        DateTime slotStart = date.Date.AddHours(startHour);
        DateTime dayEnd = date.Date.AddHours(endHour);

        while (slotStart.AddMinutes(slotDurationMinutes) <= dayEnd)
        {
            DateTime slotEnd = slotStart.AddMinutes(slotDurationMinutes);

            // Count how many active appointments overlap this slot
            int bookedCount = dayEntries.Count(e =>
                e.AppointmentDateTime < slotEnd &&
                e.AppointmentDateTime.AddMinutes(e.DurationMinutes) > slotStart);

            slots.Add(new AvailableSlot
            {
                StartTime = slotStart,
                EndTime = slotEnd,
                DurationMinutes = slotDurationMinutes,
                IsAvailable = bookedCount == 0,
                BookedCount = bookedCount,
            });

            slotStart = slotEnd;
        }

        return Task.FromResult(slots);
    }

    public Task<List<AvailableSlot>> GetAvailableSlotsAsync(
        DateTime date,
        int slotDurationMinutes,
        List<AvailabilityWindow> availabilityWindows,
        ClinicSchedulingTierConfig? tierConfig)
    {
        List<ClinicScheduleEntry> dayEntries = _state.State.Entries
            .Where(e => ActiveStatuses.Contains(e.Status) && e.AppointmentDateTime.Date == date.Date)
            .OrderBy(e => e.AppointmentDateTime)
            .ToList();

        List<AvailableSlot> allSlots = new();
        int patientSlotCounter = 0;

        foreach (AvailabilityWindow window in availabilityWindows.OrderBy(w => w.StartTime))
        {
            int windowSlotDuration = window.AppointmentLengthOverride ?? slotDurationMinutes;
            DateTime slotStart = window.StartTime;

            while (slotStart.AddMinutes(windowSlotDuration) <= window.EndTime)
            {
                DateTime slotEnd = slotStart.AddMinutes(windowSlotDuration);

                int bookedCount = dayEntries.Count(e =>
                    e.AppointmentDateTime < slotEnd &&
                    e.AppointmentDateTime.AddMinutes(e.DurationMinutes) > slotStart);

                // Determine scheduling tier
                string tier = "STAFF";
                if (tierConfig != null && tierConfig.PatientSelfSchedulingEnabled)
                {
                    if (patientSlotCounter < tierConfig.PatientSchedulableSlotCount)
                        tier = "PATIENT";
                    patientSlotCounter++;
                }

                allSlots.Add(new AvailableSlot
                {
                    StartTime = slotStart,
                    EndTime = slotEnd,
                    DurationMinutes = windowSlotDuration,
                    IsAvailable = bookedCount == 0,
                    BookedCount = bookedCount,
                    SchedulingTier = tier
                });

                slotStart = slotEnd;
            }
        }

        return Task.FromResult(allSlots);
    }
}
