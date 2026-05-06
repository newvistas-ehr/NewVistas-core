// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Provider Availability Grain — defines when a provider works at which clinic
/// and manages time blocks (vacation, sick leave, admin time, etc.).
///
/// Key: "PROV-AVAIL:{providerId}"
/// VistA File #44.005 (SD Clinic Availability), File #44.002 (Provider).
/// MUMPS references: SDCOU.m, SDBUILD.m
/// </summary>
public class ProviderAvailabilityGrain : Grain, IProviderAvailabilityGrain
{
    private readonly IPersistentState<ProviderAvailabilityState> _state;

    public ProviderAvailabilityGrain(
        [PersistentState("providerAvailability", "providerAvailabilityStore")] IPersistentState<ProviderAvailabilityState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProviderId))
        {
            _state.State.ProviderId = this.GetPrimaryKeyString();
        }

        // Clean up expired time blocks (older than 30 days)
        DateTime cutoff = DateTime.UtcNow.AddDays(-30);
        _state.State.TimeBlocks.RemoveAll(b => b.EndDateTime < cutoff);

        return base.OnActivateAsync(cancellationToken);
    }

    // ─── State retrieval ─────────────────────────────────────────────

    public Task<ProviderAvailabilityState> GetAvailabilityAsync()
        => Task.FromResult(_state.State);

    // ─── Provider status ─────────────────────────────────────────────

    public async Task UpdateProviderStatusAsync(string status, string? reason, string? modifiedBy)
    {
        _state.State.Status = status;
        _state.State.StatusReason = reason;
        _state.State.StatusChangedDate = DateTime.UtcNow;
        _state.State.StatusChangedBy = modifiedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── Recurring weekly patterns ───────────────────────────────────

    public async Task AddWeeklyPatternAsync(WeeklyAvailabilityPattern pattern)
    {
        if (string.IsNullOrEmpty(pattern.PatternId))
            pattern.PatternId = $"PAT-{Guid.NewGuid():N}";

        _state.State.WeeklyPatterns.Add(pattern);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateWeeklyPatternAsync(string patternId, WeeklyAvailabilityPattern pattern)
    {
        WeeklyAvailabilityPattern? existing = _state.State.WeeklyPatterns
            .FirstOrDefault(p => p.PatternId == patternId);

        if (existing == null)
            throw new InvalidOperationException($"Pattern {patternId} not found.");

        existing.ClinicId = pattern.ClinicId;
        existing.ClinicName = pattern.ClinicName;
        existing.DaysOfWeek = pattern.DaysOfWeek;
        existing.StartHour = pattern.StartHour;
        existing.StartMinute = pattern.StartMinute;
        existing.EndHour = pattern.EndHour;
        existing.EndMinute = pattern.EndMinute;
        existing.EffectiveFrom = pattern.EffectiveFrom;
        existing.EffectiveTo = pattern.EffectiveTo;
        existing.AppointmentLengthOverride = pattern.AppointmentLengthOverride;
        existing.MaxPatientsOverride = pattern.MaxPatientsOverride;
        existing.IsActive = pattern.IsActive;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveWeeklyPatternAsync(string patternId)
    {
        _state.State.WeeklyPatterns.RemoveAll(p => p.PatternId == patternId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── One-off time blocks ─────────────────────────────────────────

    public async Task<string> AddTimeBlockAsync(ProviderTimeBlock block)
    {
        if (string.IsNullOrEmpty(block.BlockId))
            block.BlockId = $"BLK-{Guid.NewGuid():N}";

        _state.State.TimeBlocks.Add(block);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return block.BlockId;
    }

    public async Task UpdateTimeBlockAsync(string blockId, ProviderTimeBlock block)
    {
        ProviderTimeBlock? existing = _state.State.TimeBlocks
            .FirstOrDefault(b => b.BlockId == blockId);

        if (existing == null)
            throw new InvalidOperationException($"Time block {blockId} not found.");

        existing.BlockType = block.BlockType;
        existing.StartDateTime = block.StartDateTime;
        existing.EndDateTime = block.EndDateTime;
        existing.ClinicId = block.ClinicId;
        existing.Reason = block.Reason;
        existing.IsRecurringDaily = block.IsRecurringDaily;
        existing.RecurringStartHour = block.RecurringStartHour;
        existing.RecurringStartMinute = block.RecurringStartMinute;
        existing.RecurringEndHour = block.RecurringEndHour;
        existing.RecurringEndMinute = block.RecurringEndMinute;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTimeBlockAsync(string blockId)
    {
        _state.State.TimeBlocks.RemoveAll(b => b.BlockId == blockId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<ProviderTimeBlock>> GetTimeBlocksForDateRangeAsync(DateTime start, DateTime end)
        => Task.FromResult(_state.State.TimeBlocks
            .Where(b => b.StartDateTime < end && b.EndDateTime > start)
            .ToList());

    // ─── Computed availability for scheduling ────────────────────────

    public Task<List<AvailabilityWindow>> GetEffectiveAvailabilityAsync(string clinicId, DateTime date)
    {
        // 1. If provider is not active, no availability
        if (_state.State.Status != "ACTIVE")
            return Task.FromResult(new List<AvailabilityWindow>());

        // 2. Find matching weekly patterns for this clinic and day of week
        List<WeeklyAvailabilityPattern> matchingPatterns = _state.State.WeeklyPatterns
            .Where(p => p.IsActive
                && p.ClinicId == clinicId
                && p.DaysOfWeek.Contains(date.DayOfWeek)
                && (p.EffectiveFrom == null || date.Date >= p.EffectiveFrom.Value.Date)
                && (p.EffectiveTo == null || date.Date <= p.EffectiveTo.Value.Date))
            .ToList();

        if (matchingPatterns.Count == 0)
            return Task.FromResult(new List<AvailabilityWindow>());

        // 3. Build raw availability windows from patterns
        List<AvailabilityWindow> windows = matchingPatterns.Select(p => new AvailabilityWindow
        {
            StartTime = date.Date.AddHours(p.StartHour).AddMinutes(p.StartMinute),
            EndTime = date.Date.AddHours(p.EndHour).AddMinutes(p.EndMinute),
            ClinicId = p.ClinicId,
            ClinicName = p.ClinicName,
            AppointmentLengthOverride = p.AppointmentLengthOverride,
            MaxPatientsOverride = p.MaxPatientsOverride
        }).ToList();

        // 4. Get time blocks that overlap this date for this clinic (or provider-wide)
        DateTime dayStart = date.Date;
        DateTime dayEnd = date.Date.AddDays(1);
        List<(DateTime BlockStart, DateTime BlockEnd)> blockPeriods = GetBlockPeriodsForDate(clinicId, dayStart, dayEnd, date);

        // 5. Subtract blocks from windows
        if (blockPeriods.Count > 0)
        {
            windows = SubtractBlocksFromWindows(windows, blockPeriods);
        }

        return Task.FromResult(windows);
    }

    public Task<List<ProviderClinicAvailabilitySummary>> GetAvailableClinicsForDateAsync(DateTime date)
    {
        if (_state.State.Status != "ACTIVE")
            return Task.FromResult(new List<ProviderClinicAvailabilitySummary>());

        // Group patterns by clinic
        IEnumerable<IGrouping<string, WeeklyAvailabilityPattern>> clinicGroups = _state.State.WeeklyPatterns
            .Where(p => p.IsActive
                && p.DaysOfWeek.Contains(date.DayOfWeek)
                && (p.EffectiveFrom == null || date.Date >= p.EffectiveFrom.Value.Date)
                && (p.EffectiveTo == null || date.Date <= p.EffectiveTo.Value.Date))
            .GroupBy(p => p.ClinicId);

        List<ProviderClinicAvailabilitySummary> summaries = new();

        foreach (IGrouping<string, WeeklyAvailabilityPattern> group in clinicGroups)
        {
            string clinicId = group.Key;
            List<AvailabilityWindow> windows = group.Select(p => new AvailabilityWindow
            {
                StartTime = date.Date.AddHours(p.StartHour).AddMinutes(p.StartMinute),
                EndTime = date.Date.AddHours(p.EndHour).AddMinutes(p.EndMinute),
                ClinicId = p.ClinicId,
                ClinicName = p.ClinicName,
                AppointmentLengthOverride = p.AppointmentLengthOverride,
                MaxPatientsOverride = p.MaxPatientsOverride
            }).ToList();

            DateTime dayStart = date.Date;
            DateTime dayEnd = date.Date.AddDays(1);
            List<(DateTime, DateTime)> blockPeriods = GetBlockPeriodsForDate(clinicId, dayStart, dayEnd, date);

            if (blockPeriods.Count > 0)
                windows = SubtractBlocksFromWindows(windows, blockPeriods);

            if (windows.Count > 0)
            {
                summaries.Add(new ProviderClinicAvailabilitySummary
                {
                    ClinicId = clinicId,
                    ClinicName = windows[0].ClinicName,
                    Windows = windows,
                    TotalAvailableMinutes = windows.Sum(w => (int)(w.EndTime - w.StartTime).TotalMinutes)
                });
            }
        }

        return Task.FromResult(summaries);
    }

    // ─── Scheduling tier configuration ───────────────────────────────

    public async Task SetClinicSchedulingTiersAsync(string clinicId, ClinicSchedulingTierConfig tierConfig)
    {
        _state.State.SchedulingTiers[clinicId] = tierConfig;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<ClinicSchedulingTierConfig?> GetClinicSchedulingTiersAsync(string clinicId)
    {
        _state.State.SchedulingTiers.TryGetValue(clinicId, out ClinicSchedulingTierConfig? config);
        return Task.FromResult(config);
    }

    // ─── Private helpers ─────────────────────────────────────────────

    /// <summary>
    /// Gets all block periods for a specific date, including recurring daily blocks.
    /// Filters to blocks that apply to the given clinic (or are provider-wide with null ClinicId).
    /// </summary>
    private List<(DateTime BlockStart, DateTime BlockEnd)> GetBlockPeriodsForDate(
        string clinicId, DateTime dayStart, DateTime dayEnd, DateTime date)
    {
        List<(DateTime, DateTime)> blockPeriods = new();

        foreach (ProviderTimeBlock block in _state.State.TimeBlocks)
        {
            // Skip blocks for a different clinic (null ClinicId = provider-wide, applies to all)
            if (block.ClinicId != null && block.ClinicId != clinicId)
                continue;

            if (block.IsRecurringDaily
                && block.RecurringStartHour.HasValue
                && block.RecurringEndHour.HasValue)
            {
                // Recurring daily block — check if the date falls within the block's date range
                if (date.Date >= block.StartDateTime.Date && date.Date <= block.EndDateTime.Date)
                {
                    DateTime blockStart = date.Date
                        .AddHours(block.RecurringStartHour.Value)
                        .AddMinutes(block.RecurringStartMinute ?? 0);
                    DateTime blockEnd = date.Date
                        .AddHours(block.RecurringEndHour.Value)
                        .AddMinutes(block.RecurringEndMinute ?? 0);
                    blockPeriods.Add((blockStart, blockEnd));
                }
            }
            else
            {
                // One-off block — check for overlap with this day
                if (block.StartDateTime < dayEnd && block.EndDateTime > dayStart)
                {
                    // Clamp to this day's boundaries
                    DateTime blockStart = block.StartDateTime < dayStart ? dayStart : block.StartDateTime;
                    DateTime blockEnd = block.EndDateTime > dayEnd ? dayEnd : block.EndDateTime;
                    blockPeriods.Add((blockStart, blockEnd));
                }
            }
        }

        return blockPeriods;
    }

    /// <summary>
    /// Subtracts blocked periods from availability windows.
    /// A block in the middle of a window splits it into two windows.
    /// A block overlapping the start or end truncates the window.
    /// A block covering the entire window removes it.
    /// </summary>
    private static List<AvailabilityWindow> SubtractBlocksFromWindows(
        List<AvailabilityWindow> windows,
        List<(DateTime BlockStart, DateTime BlockEnd)> blocks)
    {
        List<AvailabilityWindow> result = new(windows);

        foreach ((DateTime blockStart, DateTime blockEnd) in blocks)
        {
            List<AvailabilityWindow> nextResult = new();

            foreach (AvailabilityWindow window in result)
            {
                // No overlap — keep the window as-is
                if (blockStart >= window.EndTime || blockEnd <= window.StartTime)
                {
                    nextResult.Add(window);
                    continue;
                }

                // Block covers the entire window — remove it
                if (blockStart <= window.StartTime && blockEnd >= window.EndTime)
                    continue;

                // Block overlaps the start — truncate from the left
                if (blockStart <= window.StartTime && blockEnd < window.EndTime)
                {
                    nextResult.Add(new AvailabilityWindow
                    {
                        StartTime = blockEnd,
                        EndTime = window.EndTime,
                        ClinicId = window.ClinicId,
                        ClinicName = window.ClinicName,
                        AppointmentLengthOverride = window.AppointmentLengthOverride,
                        MaxPatientsOverride = window.MaxPatientsOverride
                    });
                    continue;
                }

                // Block overlaps the end — truncate from the right
                if (blockStart > window.StartTime && blockEnd >= window.EndTime)
                {
                    nextResult.Add(new AvailabilityWindow
                    {
                        StartTime = window.StartTime,
                        EndTime = blockStart,
                        ClinicId = window.ClinicId,
                        ClinicName = window.ClinicName,
                        AppointmentLengthOverride = window.AppointmentLengthOverride,
                        MaxPatientsOverride = window.MaxPatientsOverride
                    });
                    continue;
                }

                // Block is in the middle — split into two windows
                nextResult.Add(new AvailabilityWindow
                {
                    StartTime = window.StartTime,
                    EndTime = blockStart,
                    ClinicId = window.ClinicId,
                    ClinicName = window.ClinicName,
                    AppointmentLengthOverride = window.AppointmentLengthOverride,
                    MaxPatientsOverride = window.MaxPatientsOverride
                });
                nextResult.Add(new AvailabilityWindow
                {
                    StartTime = blockEnd,
                    EndTime = window.EndTime,
                    ClinicId = window.ClinicId,
                    ClinicName = window.ClinicName,
                    AppointmentLengthOverride = window.AppointmentLengthOverride,
                    MaxPatientsOverride = window.MaxPatientsOverride
                });
            }

            result = nextResult;
        }

        return result;
    }
}
