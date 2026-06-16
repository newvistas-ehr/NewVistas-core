// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Index Grain — singleton cross-reference for patient search.
/// Grain key: "PATIENT-INDEX"
///
/// Single WRITER and source of truth. Search traffic is served by the
/// [StatelessWorker] PatientSearchGrain readers, which validate a silo-local
/// snapshot against GetVersionAsync on every call and catch up via
/// GetChangesSinceAsync (delta) or GetSnapshotAsync (full pull). The change
/// ring buffer is in-memory only — persisting ~1000 changes would bloat every
/// index write, and after a rare reactivation the empty ring just forces one
/// full snapshot pull per silo.
///
/// Search heuristic (shared via PatientIndexSearchHelper) mirrors ORWPT LOOKUP.
/// </summary>
public class PatientIndexGrain : Grain, IPatientIndexGrain
{
    private const int MaxRecentChanges = 1000;

    private readonly IPersistentState<PatientIndexState> _state;

    // Transient: rebuilt empty on activation (forces SnapshotRequired once).
    private readonly List<PatientIndexChange> _recentChanges = new();

    public PatientIndexGrain(
        [PersistentState("patientIndexState", "patientIndexStore")]
        IPersistentState<PatientIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PatientIndexEntry entry)
    {
        _state.State.Patients[entry.PatientId] = entry;
        _state.State.Version++;

        _recentChanges.Add(new PatientIndexChange
        {
            Version = _state.State.Version,
            Entry = entry,
            IsRemoval = false
        });
        if (_recentChanges.Count > MaxRecentChanges)
            _recentChanges.RemoveRange(0, _recentChanges.Count - MaxRecentChanges);

        await _state.WriteStateAsync();
    }

    public Task<long> GetVersionAsync() => Task.FromResult(_state.State.Version);

    public Task<PatientIndexSnapshot> GetSnapshotAsync() =>
        Task.FromResult(new PatientIndexSnapshot
        {
            Version = _state.State.Version,
            Entries = _state.State.Patients.Values.ToList()
        });

    public Task<PatientIndexDelta> GetChangesSinceAsync(long since)
    {
        if (since == _state.State.Version)
            return Task.FromResult(new PatientIndexDelta { Version = _state.State.Version });

        // Reader is behind the oldest buffered change (or the ring was reset
        // by reactivation) → it must take a full snapshot.
        if (since > _state.State.Version
            || _recentChanges.Count == 0
            || since < _recentChanges[0].Version - 1)
        {
            return Task.FromResult(new PatientIndexDelta
            {
                Version = _state.State.Version,
                SnapshotRequired = true
            });
        }

        return Task.FromResult(new PatientIndexDelta
        {
            Version = _state.State.Version,
            Changes = _recentChanges.Where(c => c.Version > since).ToList()
        });
    }

    public Task<PatientIndexEntry?> GetByPatientIdAsync(string patientId)
    {
        _state.State.Patients.TryGetValue(patientId, out PatientIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<PatientIndexEntry?> GetByDfnAsync(string dfn)
    {
        PatientIndexEntry? entry = _state.State.Patients.Values
            .FirstOrDefault(e => e.Dfn == dfn);
        return Task.FromResult(entry);
    }

    public Task<PatientIndexEntry?> GetByIcnAsync(string icn)
    {
        PatientIndexEntry? entry = _state.State.Patients.Values
            .FirstOrDefault(e => e.Icn == icn);
        return Task.FromResult(entry);
    }

    public Task<List<PatientIndexEntry>> SearchAsync(string searchTerm, int maxResults = 25)
        => Task.FromResult(PatientIndexSearchHelper.Search(_state.State.Patients.Values, searchTerm, maxResults));

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Patients.Count);

    public Task<List<string>> GetAllPatientIdsAsync(int? maxResults = null)
    {
        IEnumerable<string> ids = _state.State.Patients.Keys;
        if (maxResults.HasValue) ids = ids.Take(maxResults.Value);
        return Task.FromResult(ids.ToList());
    }
}
