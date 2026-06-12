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
/// Generic per-patient, per-domain full-history index.
/// Grain Key: "{patientId}:{domain}" (see PatientHistoryDomains).
///
/// Cold storage for the IDs trimmed out of PatientState's recent windows —
/// activated only for full-history reads, migrations, and merges, so the hot
/// patient blob stays small without losing any history.
/// </summary>
public class PatientHistoryIndexGrain : Grain, IPatientHistoryIndexGrain
{
    private readonly IPersistentState<PatientHistoryIndexState> _state;

    public PatientHistoryIndexGrain(
        [PersistentState("patientHistoryIndexState", "patientHistoryIndexStore")]
        IPersistentState<PatientHistoryIndexState> state)
    {
        _state = state;
    }

    public async Task AddEntryAsync(HistoryRef entry)
    {
        if (ApplyEntry(entry))
            await _state.WriteStateAsync();
    }

    public async Task AddRangeAsync(List<HistoryRef> entries)
    {
        bool changed = false;
        foreach (HistoryRef entry in entries)
            changed |= ApplyEntry(entry);

        if (changed)
            await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string itemId)
    {
        int removed = _state.State.Entries.RemoveAll(e => e.ItemId == itemId);
        if (removed > 0)
            await _state.WriteStateAsync();
    }

    public Task<List<string>> GetAllIdsAsync() =>
        Task.FromResult(_state.State.Entries.Select(e => e.ItemId).ToList());

    public Task<List<string>> GetPageAsync(int offset, int maxResults)
    {
        // Newest first: dated entries by Date descending, then undated
        // (migrated legacy) entries in reverse insertion order.
        IEnumerable<string> ordered = _state.State.Entries
            .Select((e, i) => (Entry: e, Index: i))
            .OrderByDescending(x => x.Entry.Date.HasValue)
            .ThenByDescending(x => x.Entry.Date ?? DateTime.MinValue)
            .ThenByDescending(x => x.Index)
            .Select(x => x.Entry.ItemId);

        return Task.FromResult(ordered.Skip(offset).Take(maxResults).ToList());
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);

    /// <summary>
    /// Upserts an entry by ItemId without persisting. Returns true if state changed.
    /// </summary>
    private bool ApplyEntry(HistoryRef entry)
    {
        if (string.IsNullOrEmpty(entry.ItemId))
            return false;

        HistoryRef? existing = _state.State.Entries.FirstOrDefault(e => e.ItemId == entry.ItemId);
        if (existing is null)
        {
            _state.State.Entries.Add(entry);
            return true;
        }

        // Idempotent re-add: only refresh a missing date (migration backfill
        // followed by a dated live write must not lose the date).
        if (existing.Date is null && entry.Date is not null)
        {
            existing.Date = entry.Date;
            return true;
        }

        return false;
    }
}
