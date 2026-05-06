// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implements the per-patient Medication Administration Record (MAR).
/// Key: "BCMA-MAR:{patientId}"
/// </summary>
public class PatientMARGrain : Grain, IPatientMARGrain
{
    private readonly IPersistentState<PatientMARState> _state;

    public PatientMARGrain(
        [PersistentState("patientMARState", "bcmaMarStore")]
        IPersistentState<PatientMARState> state)
    {
        _state = state;
    }

    public Task<List<MarEntry>> GetMARAsync() =>
        Task.FromResult(_state.State.Entries
            .OrderBy(PrioritySortOrder)
            .ThenBy(e => e.DrugName)
            .ToList());

    public Task<List<MarEntry>> GetActiveMARAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.IsActive)
            .OrderBy(PrioritySortOrder)
            .ThenBy(e => e.DrugName)
            .ToList());

    public Task<List<MarEntry>> GetDueNowAsync()
    {
        DateTime window = DateTime.UtcNow.AddMinutes(60);
        List<MarEntry> due = _state.State.Entries
            .Where(e => e.IsActive && e.ScheduledTimes.Any(t => t <= window))
            .OrderBy(e => e.ScheduledTimes.Where(t => t <= window).Min())
            .ToList();
        return Task.FromResult(due);
    }

    public async Task AddOrUpdateEntryAsync(MarEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.OrderId == entry.OrderId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateEntryAsync(string orderId)
    {
        MarEntry? entry = _state.State.Entries.FirstOrDefault(e => e.OrderId == orderId);
        if (entry is not null)
        {
            entry.IsActive = false;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordAdministrationAsync(
        string orderId,
        string bcmaId,
        string actionStatus,
        DateTime adminTime,
        string? adminByName)
    {
        MarEntry? entry = _state.State.Entries.FirstOrDefault(e => e.OrderId == orderId);
        if (entry is null) return;

        entry.LastAdminDateTime = adminTime;
        entry.LastAdminStatus = actionStatus;
        entry.LastAdminBy = adminByName;
        entry.TotalAdministrations++;

        if (!entry.BcmaAdministrationIds.Contains(bcmaId))
            entry.BcmaAdministrationIds.Add(bcmaId);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private static int PrioritySortOrder(MarEntry e) => e.Priority switch
    {
        "STAT" => 0,
        "ASAP" => 1,
        _ => 2
    };
}
