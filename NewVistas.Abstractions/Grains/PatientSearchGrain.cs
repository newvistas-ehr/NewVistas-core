// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Search Grain — StatelessWorker read layer over PATIENT-INDEX.
/// Key "PATIENT-SEARCH".
///
/// Per-call freshness protocol (version-exact, never time-based):
///   1. GetVersionAsync on PATIENT-INDEX (8-byte payload).
///   2. Holder empty → full GetSnapshotAsync pull, Swap.
///   3. Holder behind → GetChangesSinceAsync delta applied to a NEW dictionary
///      (snapshots are immutable), Swap; SnapshotRequired → full pull.
///   4. Search locally via the shared PatientIndexSearchHelper.
///
/// All activations on a silo share one immutable snapshot via the
/// IPatientIndexSnapshotService singleton, so memory stays at one index copy
/// per silo while search CPU scales with worker activations.
/// </summary>
[StatelessWorker]
public class PatientSearchGrain : Grain, IPatientSearchGrain
{
    private readonly IPatientIndexSnapshotService _snapshotService;

    public PatientSearchGrain(IPatientIndexSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    private IPatientIndexGrain Index()
        => GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    public async Task<List<PatientIndexEntry>> SearchAsync(string searchTerm, int maxResults = 25)
    {
        PatientIndexReadSnapshot snapshot = await EnsureFreshSnapshotAsync();
        return PatientIndexSearchHelper.Search(snapshot.ByPatientId.Values, searchTerm, maxResults);
    }

    private async Task<PatientIndexReadSnapshot> EnsureFreshSnapshotAsync()
    {
        long current = await Index().GetVersionAsync();

        PatientIndexReadSnapshot? held = _snapshotService.TryGet();
        if (held is not null && held.Version == current)
            return held;

        if (held is null)
            return await PullFullSnapshotAsync();

        PatientIndexDelta delta = await Index().GetChangesSinceAsync(held.Version);
        if (delta.SnapshotRequired)
            return await PullFullSnapshotAsync();

        if (delta.Changes.Count == 0)
            return held;

        // Apply the delta to a copy — held snapshots are immutable.
        var updated = new Dictionary<string, PatientIndexEntry>(held.ByPatientId.Count + delta.Changes.Count);
        foreach (KeyValuePair<string, PatientIndexEntry> pair in held.ByPatientId)
            updated[pair.Key] = pair.Value;

        foreach (PatientIndexChange change in delta.Changes)
        {
            if (change.IsRemoval)
                updated.Remove(change.Entry.PatientId);
            else
                updated[change.Entry.PatientId] = change.Entry;
        }

        var refreshed = new PatientIndexReadSnapshot(delta.Version, updated);
        _snapshotService.Swap(refreshed);
        return _snapshotService.TryGet() ?? refreshed;
    }

    private async Task<PatientIndexReadSnapshot> PullFullSnapshotAsync()
    {
        PatientIndexSnapshot full = await Index().GetSnapshotAsync();
        var byId = full.Entries.ToDictionary(e => e.PatientId);
        var snapshot = new PatientIndexReadSnapshot(full.Version, byId);
        _snapshotService.Swap(snapshot);
        return _snapshotService.TryGet() ?? snapshot;
    }
}
