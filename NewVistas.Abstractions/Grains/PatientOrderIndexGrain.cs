// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient index of all order grain keys.
/// Maintains entries sorted by StartDate descending (most recent first).
/// Supports ORWORR AGET filter codes for status-based filtering without
/// activating individual order grains.
/// </summary>
public class PatientOrderIndexGrain : Grain, IPatientOrderIndexGrain
{
    private readonly IPersistentState<PatientOrderIndexState> _state;

    public PatientOrderIndexGrain(
        [PersistentState("patientOrderIndexState", "patientOrderIndexStore")]
        IPersistentState<PatientOrderIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdateOrderAsync(OrderIndexEntry entry)
    {
        // Remove existing entry with same key if present (status may have changed)
        _state.State.Entries.RemoveAll(e => e.OrderGrainKey == entry.OrderGrainKey);

        // Insert in sorted position (descending by StartDate)
        int insertIndex = _state.State.Entries.FindIndex(e => e.StartDate <= entry.StartDate);
        if (insertIndex < 0)
            _state.State.Entries.Add(entry);
        else
            _state.State.Entries.Insert(insertIndex, entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveOrderAsync(string orderGrainKey)
    {
        int removed = _state.State.Entries.RemoveAll(e => e.OrderGrainKey == orderGrainKey);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<OrderIndexEntry>> GetAllEntriesAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<OrderIndexEntry>> GetEntriesByDateRangeAsync(DateTime from, DateTime to)
    {
        List<OrderIndexEntry> result = _state.State.Entries
            .Where(e => e.StartDate >= from && e.StartDate <= to)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<OrderIndexEntry>> GetEntriesBeforeDateAsync(DateTime before, int maxCount)
    {
        List<OrderIndexEntry> result = _state.State.Entries
            .Where(e => e.StartDate < before)
            .Take(maxCount)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<OrderIndexEntry>> GetEntriesByFilterAsync(int filter)
    {
        // Replicates MatchesOrderFilter logic from PatientWorkflowGrain.AdtAdmin.cs
        // but operates on OrderIndexEntry metadata (no grain activation needed)
        List<OrderIndexEntry> result = _state.State.Entries
            .Where(e => MatchesFilter(e, filter))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);

    /// <summary>
    /// ORWORR AGET filter codes applied to index entry metadata.
    /// </summary>
    private static bool MatchesFilter(OrderIndexEntry entry, int filter)
    {
        return filter switch
        {
            1 => true, // All
            2 => entry.Status is "Pending" or "Active" or "Hold", // Current
            3 => entry.Status == "Discontinued", // Discontinued
            4 => entry.Status is "Completed" or "Expired", // Completed/Expired
            5 => entry.Status == "Active", // Expiring (active orders nearing stop date)
            7 => entry.Status == "Pending", // Pending
            11 => !entry.IsSigned, // Unsigned
            _ => true
        };
    }
}
