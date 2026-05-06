// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Provider Schedule Index Grain — a provider's appointment schedule.
/// Provides "Today's Schedule" and "Upcoming" views from the provider's perspective.
///
/// Key: "PROV-SCHED:{providerId}"
/// </summary>
public class ProviderScheduleIndexGrain : Grain, IProviderScheduleIndexGrain
{
    private readonly IPersistentState<ProviderScheduleIndexState> _state;

    public ProviderScheduleIndexGrain(
        [PersistentState("providerScheduleIndex", "providerScheduleIndexStore")] IPersistentState<ProviderScheduleIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProviderId))
        {
            _state.State.ProviderId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdateAsync(ProviderScheduleEntry entry)
    {
        ProviderScheduleEntry? existing = _state.State.Entries
            .FirstOrDefault(e => e.AppointmentId == entry.AppointmentId);

        if (existing != null)
        {
            existing.PatientId = entry.PatientId;
            existing.PatientName = entry.PatientName;
            existing.AppointmentDateTime = entry.AppointmentDateTime;
            existing.ClinicId = entry.ClinicId;
            existing.ClinicName = entry.ClinicName;
            existing.DurationMinutes = entry.DurationMinutes;
            existing.Status = entry.Status;
            existing.Purpose = entry.Purpose;
            existing.AppointmentType = entry.AppointmentType;
        }
        else
        {
            _state.State.Entries.Add(entry);
        }

        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string appointmentId)
    {
        _state.State.Entries.RemoveAll(e => e.AppointmentId == appointmentId);
        await _state.WriteStateAsync();
    }

    public Task<List<ProviderScheduleEntry>> GetByDateAsync(DateTime date)
        => Task.FromResult(_state.State.Entries
            .Where(e => e.AppointmentDateTime.Date == date.Date)
            .OrderBy(e => e.AppointmentDateTime)
            .ToList());

    public Task<List<ProviderScheduleEntry>> GetTodayAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.AppointmentDateTime.Date == DateTime.UtcNow.Date)
            .OrderBy(e => e.AppointmentDateTime)
            .ToList());

    public Task<List<ProviderScheduleEntry>> GetUpcomingAsync(int days = 7)
    {
        DateTime now = DateTime.UtcNow;
        DateTime cutoff = now.AddDays(days);
        return Task.FromResult(_state.State.Entries
            .Where(e => e.AppointmentDateTime >= now && e.AppointmentDateTime < cutoff)
            .OrderBy(e => e.AppointmentDateTime)
            .ToList());
    }

    public Task<List<ProviderScheduleEntry>> GetAllAsync(int max = 100)
        => Task.FromResult(_state.State.Entries
            .OrderByDescending(e => e.AppointmentDateTime)
            .Take(max)
            .ToList());

    public async Task UpdateStatusAsync(string appointmentId, string status)
    {
        ProviderScheduleEntry? entry = _state.State.Entries
            .FirstOrDefault(e => e.AppointmentId == appointmentId);

        if (entry != null)
        {
            entry.Status = status;
            await _state.WriteStateAsync();
        }
    }
}
